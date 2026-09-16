using System;
using System.Threading;
using CasualKit.Core;
using CasualKit.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace MatchRacers
{
    public sealed class RaceSession : IGameSession, ITickable
    {
        public static uint SeedOverride;

        private static TrackLayoutSO s_SelectedTrack;

        private readonly GameContext m_Context;

        private RaceConfigSO m_Config;
        private RacePath m_Path;
        private TrackBuilder m_Track;
        private RaceSimulation m_Simulation;
        private PlayerAgent m_PlayerAgent;
        private RaceInputReader m_Input;
        private CarView[] m_Views;
        private RaceCameraRig m_CameraRig;
        private RaceDebugPanel m_DebugPanel;
        private RaceHudView m_Hud;
        private RaceRecorder m_Recorder;
        private RaceFeedback m_Feedback;
        private RaceVfxSystem m_Vfx;
        private RaceModeSelectView m_ModeSelect;
        private RaceTrackSelectView m_TrackSelect;
        private RaceEnvironment m_Environment;
        private RaceEnvironmentSO m_EnvironmentSettings;
        private RacePerformanceProbe m_Performance;
        private ERaceMode m_Mode = ERaceMode.Free;
        private int m_TargetPosition;
        private bool m_AwaitingSelection = true;

        private GameObject m_CarRoot;
        private GameObject m_CameraObject;
        private Camera m_BorrowedCamera;
        private GameObject m_LightObject;

        private int m_LevelIndex;
        private uint m_Seed;
        private bool m_IsLoaded;
        private bool m_IsPaused;
        private bool m_AnalyticsSent;
        private bool m_ResultsShown;
        private bool m_PhysicsSuspended;
        private SimulationMode m_PreviousSimulationMode;

        public int LevelIndex => m_LevelIndex;
        public bool IsLoaded => m_IsLoaded;
        public bool ReturnToMenu => true;
        public uint Seed => m_Seed;
        public RaceSimulation Simulation => m_Simulation;
        public ERaceMode Mode => m_Mode;
        public int TargetPosition => m_TargetPosition;

        public RaceSession(GameContext context)
        {
            m_Context = context;
        }

        public async UniTask<bool> LoadAsync(int levelIndex, CancellationToken cancellationToken)
        {
            m_LevelIndex = levelIndex < 0 ? 0 : levelIndex;

            IAssetProvider assets = m_Context.Services.Get<IAssetProvider>();
            m_Config = await assets.LoadAsset<RaceConfigSO>(RaceAddressableKeys.RaceConfig, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return false;

            if (m_Config == null || m_Config.BuffTable == null || m_Config.CarCatalog == null)
            {
                EditorLog.Error("RaceConfig, BuffTable or CarCatalog missing. Run MatchRacers/Addressables/Ensure Settings.");
                return false;
            }

            m_Seed = SeedOverride != 0u ? SeedOverride : (uint)Environment.TickCount;
            SeedOverride = 0u;

            SuspendPhysics();

            BuildTrack(ResolveInitialTrack());

            m_PlayerAgent = new PlayerAgent(0);
            m_Input = new RaceInputReader();

            IRaceAgent[] agents = new IRaceAgent[RaceConfigSO.CarCount];
            agents[0] = m_PlayerAgent;

            for (int i = 1; i < agents.Length; i++)
            {
                agents[i] = m_Config.TryGetRivalProfile(i - 1, out AiProfileSO profile)
                    ? new AiAgent(i, profile, m_Config.BuffTable)
                    : (IRaceAgent)new NullAgent(i);
            }

            m_Simulation = new RaceSimulation(m_Config, agents);
            m_Performance = new RacePerformanceProbe();
            m_Recorder = new RaceRecorder(m_Simulation, m_Config, "live", 10f) { Performance = m_Performance };
            m_Simulation.Reset(m_Seed);

            SpawnCars();

            ApplyEnvironment(ResolveEnvironment());
            EnsureLight();

            SetupCamera();

            m_Vfx = new RaceVfxSystem(m_Config.CarVfx, m_Context.Services.Get<IGameObjectPool>(), assets,
                m_CameraRig != null ? m_CameraRig.Camera : null, m_Views);
            await m_Vfx.WarmupAsync(cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return false;

            m_Feedback = new RaceFeedback(m_Context.Services.Get<IAudioService>(), m_CameraRig,
                m_Config.NitroLevels, m_Config.EngineAudio, m_Simulation.PlayerCarIndex, m_Views);

            m_Context.GameLoop.Register(this);
            EB.Presentation.Add<UIPanelOpened>(OnPanelOpened);
            EB.Gameplay.Add<PlayerBuffRequested>(OnPlayerBuffRequested);
            EB.Gameplay.Add<RaceRestartRequested>(OnRaceRestartRequested);
            PlayTrackMusic(false);

            m_IsLoaded = true;
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!m_IsLoaded || m_Simulation == null)
                return;

            using (RacePerformanceProbe.TrackRenderMarker.Auto())
            {
                if (m_Track != null)
                    m_Track.Render();
            }

            if (m_IsPaused)
                return;

            HandleDebugKeys();

            if (m_AwaitingSelection)
            {
                SyncPresentation(deltaTime);
                return;
            }

            if (m_Performance != null)
                m_Performance.Tick(Time.unscaledDeltaTime);

            int key = m_Input.ReadPressedKey();
            if (key != 0)
                EB.Gameplay.Invoke(new PlayerBuffRequested(key));

            using (RacePerformanceProbe.SimMarker.Auto())
                m_Simulation.Advance(deltaTime);

            using (RacePerformanceProbe.RecorderMarker.Auto())
            {
                if (m_Recorder != null)
                    m_Recorder.Sample();
            }

            SyncPresentation(deltaTime);
            UpdateFinishFlow(deltaTime);
        }

        private void SyncPresentation(float deltaTime)
        {
            float alpha = m_Simulation.InterpolationAlpha;
            float renderTime = m_Simulation.RenderTime;

            using (RacePerformanceProbe.ViewsMarker.Auto())
            {
                for (int i = 0; i < m_Views.Length; i++)
                    m_Views[i].Sync(deltaTime, alpha, renderTime);
            }

            using (RacePerformanceProbe.CameraMarker.Auto())
            {
                if (m_CameraRig != null)
                    m_CameraRig.Sync(deltaTime);
            }

            using (RacePerformanceProbe.VfxMarker.Auto())
            {
                if (m_Vfx != null)
                    m_Vfx.Tick(deltaTime);
            }

            using (RacePerformanceProbe.FeedbackMarker.Auto())
            {
                if (m_Feedback != null)
                    m_Feedback.Tick(deltaTime, m_Simulation, m_AwaitingSelection);
            }

            using (RacePerformanceProbe.HudMarker.Auto())
            {
                if (m_Hud != null)
                    m_Hud.Tick(deltaTime);
            }

            using (RacePerformanceProbe.DebugPanelMarker.Auto())
            {
                if (m_DebugPanel != null)
                    m_DebugPanel.Tick(Time.unscaledDeltaTime);
            }
        }

        private void HandleDebugKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.f1Key.wasPressedThisFrame && m_DebugPanel != null)
                m_DebugPanel.Toggle();

            if (keyboard.f2Key.wasPressedThisFrame)
                RestartWithSeed(m_Simulation.Seed);

            if (keyboard.f3Key.wasPressedThisFrame)
                RestartWithSeed(0u);

            if (keyboard.f4Key.wasPressedThisFrame && m_Recorder != null)
            {
                string notice = "telemetry -> " + m_Recorder.Flush();
                if (m_DebugPanel != null)
                    m_DebugPanel.ShowNotice(notice);
            }
        }

        public void BeginRace(ERaceMode mode, int targetPosition)
        {
            if (!m_AwaitingSelection)
                return;

            m_Mode = mode;
            m_TargetPosition = mode == ERaceMode.TargetOrder ? targetPosition : 0;

            m_Simulation.Configure(mode, targetPosition <= 0 ? 1 : targetPosition);
            m_Simulation.Reset(m_Seed);

            if (m_Views != null)
            {
                for (int i = 0; i < m_Views.Length; i++)
                    m_Views[i].ClearEffects();
            }

            if (m_Vfx != null)
                m_Vfx.ResetAll();

            if (m_Performance != null)
                m_Performance.Reset();

            if (m_ModeSelect != null)
                m_ModeSelect.Hide();

            m_ModeSelect = null;
            UIPanels.Close(EUIPanel.RaceModeSelection);
            CloseTrackSelection();

            m_AwaitingSelection = false;
            PlayTrackMusic(true);
        }

        private void OnRaceRestartRequested(RaceRestartRequested evt)
        {
            if (!m_ResultsShown || m_AwaitingSelection || m_Simulation == null)
                return;

            PrepareRematch();
            m_Simulation.Reset(m_Seed);

            if (m_Views != null)
            {
                for (int i = 0; i < m_Views.Length; i++)
                    m_Views[i].ClearEffects();
            }

            if (m_Vfx != null)
                m_Vfx.ResetAll();

            m_AwaitingSelection = true;
            PlayTrackMusic(false);
            OpenModeSelection();
        }

        private void PrepareRematch()
        {
            uint seed = (uint)Environment.TickCount;
            m_Seed = seed == 0u ? 1u : seed;

            m_AnalyticsSent = false;
            m_ResultsShown = false;

            if (m_Recorder != null)
                m_Recorder.Dispose();

            m_Recorder = new RaceRecorder(m_Simulation, m_Config, "live", 10f) { Performance = m_Performance };

            if (m_DebugPanel != null)
                m_DebugPanel.SetRecorder(m_Recorder);

            if (m_Hud != null)
                m_Hud.Bind(m_Simulation, m_Config);

            if (m_CameraRig != null)
                m_CameraRig.Snap();
        }

        private void OnPlayerBuffRequested(PlayerBuffRequested evt)
        {
            if (m_AwaitingSelection || m_IsPaused || m_PlayerAgent == null)
                return;

            m_PlayerAgent.Request(evt.Key);
        }

        public void BindHud(GameObject hudRoot, Action openSettings)
        {
            if (hudRoot == null)
                return;

            GameplayCanvasUI canvas = hudRoot.GetComponentInChildren<GameplayCanvasUI>(true);
            if (canvas != null)
                canvas.BindSettings(openSettings);

            BindRaceUi(hudRoot);
            OpenRacePanels();
        }

        private void OpenRacePanels()
        {
            EB.Presentation.Invoke(new OpenUIPanelEvent(EUIPanel.RaceInGameDebug, 0, additive: true));

            if (m_AwaitingSelection)
                OpenModeSelection();
        }

        private void OpenModeSelection()
        {
            EB.Presentation.Invoke(new OpenUIPanelEvent(EUIPanel.RaceModeSelection, 1, additive: true));

            TrackCatalogSO catalog = m_Config != null ? m_Config.TrackCatalog : null;
            if (catalog != null && catalog.Count > 0)
                EB.Presentation.Invoke(new OpenUIPanelEvent(EUIPanel.RaceTrackSelection, 1, additive: true));
        }

        private void CloseTrackSelection()
        {
            if (m_TrackSelect != null)
                m_TrackSelect.Hide();

            m_TrackSelect = null;
            UIPanels.Close(EUIPanel.RaceTrackSelection);
        }

        private void OnPanelOpened(UIPanelOpened opened)
        {
            if (opened.Instance == null || m_Simulation == null)
                return;

            if (opened.Panel == EUIPanel.RaceInGameDebug)
            {
                m_DebugPanel = opened.Instance.GetComponentInChildren<RaceDebugPanel>(true);
                if (m_DebugPanel != null)
                    m_DebugPanel.Bind(m_Simulation, m_Recorder);
                else
                    EditorLog.Warning("RaceInGameDebug panel has no RaceDebugPanel component; F1 has nothing to show.");
                return;
            }

            if (opened.Panel == EUIPanel.RaceTrackSelection)
            {
                OnTrackSelectionOpened(opened.Instance);
                return;
            }

            if (opened.Panel != EUIPanel.RaceModeSelection)
                return;

            m_ModeSelect = opened.Instance.GetComponentInChildren<RaceModeSelectView>(true);
            if (m_ModeSelect == null)
            {
                EditorLog.Error("RaceModeSelection panel has no RaceModeSelectView component; starting in Free mode.");
                UIPanels.Close(EUIPanel.RaceModeSelection);
                BeginRace(ERaceMode.Free, 0);
                return;
            }

            if (m_AwaitingSelection)
                m_ModeSelect.Show(BeginRace);
            else
                UIPanels.Close(EUIPanel.RaceModeSelection);
        }

        private void BindRaceUi(GameObject hudRoot)
        {
            if (m_Hud != null)
                m_Hud.Unbind();

            m_Hud = hudRoot.GetComponentInChildren<RaceHudView>(true);

            if (m_Hud != null)
                m_Hud.Bind(m_Simulation, m_Config);
            else
                EditorLog.Error("Gameplay panel has no RaceHudView component; the race HUD will not update.");
        }

        private void UnbindRaceUi()
        {
            if (m_Hud != null)
                m_Hud.Unbind();

            if (m_ModeSelect != null)
                m_ModeSelect.Hide();

            if (m_DebugPanel != null)
                m_DebugPanel.Unbind();

            CloseTrackSelection();
            UIPanels.Close(EUIPanel.RaceModeSelection);
            UIPanels.Close(EUIPanel.RaceInGameDebug);

            m_Hud = null;
            m_ModeSelect = null;
            m_DebugPanel = null;
        }

        public void RestartWithSeed(uint seed)
        {
            SeedOverride = seed;
            ISceneService scenes = m_Context.Services.Get<ISceneService>();
            scenes.Reload(ESceneName.Gameplay, m_Context.CancellationToken).Forget();
        }

        public void SelectTrack(int index)
        {
            TrackCatalogSO catalog = m_Config != null ? m_Config.TrackCatalog : null;
            if (!m_AwaitingSelection || catalog == null || m_Simulation == null)
                return;

            TrackLayoutSO layout = catalog.GetTrack(index);
            if (layout == null || layout == m_Config.TrackLayout)
                return;

            s_SelectedTrack = layout;
            BuildTrack(layout);
            m_Simulation.Reset(m_Seed);

            RaceEnvironmentSO environment = ResolveEnvironment();
            if (environment != m_EnvironmentSettings)
            {
                ApplyEnvironment(environment);
                if (m_CameraRig != null)
                    ApplyCameraLook(m_CameraRig.Camera);
            }

            PlayTrackMusic(false);

            if (m_Views != null)
            {
                for (int i = 0; i < m_Views.Length; i++)
                {
                    m_Views[i].SetPath(m_Path);
                    m_Views[i].ClearEffects();
                }
            }

            if (m_CameraRig != null)
                m_CameraRig.SetPath(m_Path);

            if (m_Vfx != null)
                m_Vfx.ResetAll();

            if (m_TrackSelect != null)
                m_TrackSelect.SetSelected(index);
        }

        private void OnTrackSelectionOpened(GameObject instance)
        {
            m_TrackSelect = instance.GetComponentInChildren<RaceTrackSelectView>(true);
            TrackCatalogSO catalog = m_Config.TrackCatalog;

            if (m_TrackSelect == null)
                EditorLog.Warning("RaceTrackSelection panel has no RaceTrackSelectView component; the track stays as it is.");

            if (m_TrackSelect == null || catalog == null || catalog.Count == 0 || !m_AwaitingSelection)
            {
                m_TrackSelect = null;
                UIPanels.Close(EUIPanel.RaceTrackSelection);
                return;
            }

            m_TrackSelect.Show(catalog, catalog.IndexOf(m_Config.TrackLayout), SelectTrack);
        }

        private void PlayTrackMusic(bool racing)
        {
            if (!m_Context.Services.TryGet(out IAudioService audio))
                return;

            TrackLayoutSO track = m_Config != null ? m_Config.TrackLayout : null;
            if (track != null && track.TryGetMusic(racing, out AudioClip clip, out float volume))
                audio.PlayMusic(clip, volume);
            else
                audio.StopMusic();
        }

        private TrackLayoutSO ResolveInitialTrack()
        {
            TrackCatalogSO catalog = m_Config.TrackCatalog;
            if (s_SelectedTrack != null && (catalog == null || catalog.IndexOf(s_SelectedTrack) >= 0))
                return s_SelectedTrack;

            if (m_Config.DefaultTrackLayout != null || catalog == null || catalog.Count == 0)
                return m_Config.DefaultTrackLayout;

            return catalog.GetTrack(0);
        }

        private void BuildTrack(TrackLayoutSO layout)
        {
            if (m_Track != null)
            {
                m_Track.Dispose();
                Resources.UnloadUnusedAssets();
            }

            m_Config.UseTrack(layout);
            m_Path = RacePath.FromConfig(m_Config);
            m_Track = new TrackBuilder(m_Config, m_Path, m_Config.TrackLayout);
            m_Track.Build();
        }

        private RaceEnvironmentSO ResolveEnvironment()
        {
            TrackLayoutSO layout = m_Config.TrackLayout;
            return layout != null && layout.Environment != null ? layout.Environment : m_Config.Environment;
        }

        private void ApplyEnvironment(RaceEnvironmentSO settings)
        {
            if (m_Environment != null)
                m_Environment.Dispose();

            m_EnvironmentSettings = settings;
            m_Environment = new RaceEnvironment(settings);
            m_Environment.Apply();
        }

        private void ApplyCameraLook(Camera camera)
        {
            if (camera == null)
                return;

            bool sky = m_Environment != null && m_Environment.HasSky;
            camera.clearFlags = sky ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            camera.backgroundColor = m_Environment != null
                ? m_Environment.BackgroundColor
                : new Color(0.09f, 0.11f, 0.14f);
            camera.allowHDR = true;
            camera.allowMSAA = true;

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            if (data != null)
                data.renderPostProcessing = true;
        }

        private void SuspendPhysics()
        {
            if (m_PhysicsSuspended)
                return;

            m_PreviousSimulationMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            m_PhysicsSuspended = true;
        }

        private void RestorePhysics()
        {
            if (!m_PhysicsSuspended)
                return;

            Physics.simulationMode = m_PreviousSimulationMode;
            m_PhysicsSuspended = false;
        }

        private void UpdateFinishFlow(float deltaTime)
        {
            if (m_Simulation.State != ERaceState.Finished || m_AnalyticsSent)
                return;

            if (m_ResultsShown)
                return;

            m_ResultsShown = true;

            if (m_Hud != null)
                m_Hud.ShowResults();

            if (m_Recorder != null)
                m_Recorder.Flush();

            SendMatchAnalytics();
        }

        public void Pause()
        {
            m_IsPaused = true;
        }

        public void Resume()
        {
            m_IsPaused = false;
        }

        public void Teardown()
        {
            m_IsPaused = false;
            m_IsLoaded = false;

            m_Context.GameLoop.Unregister(this);
            EB.Presentation.Remove<UIPanelOpened>(OnPanelOpened);
            EB.Gameplay.Remove<PlayerBuffRequested>(OnPlayerBuffRequested);
            EB.Gameplay.Remove<RaceRestartRequested>(OnRaceRestartRequested);

            UnbindRaceUi();

            if (m_Environment != null)
            {
                m_Environment.Dispose();
                m_Environment = null;
            }

            if (m_Feedback != null)
            {
                m_Feedback.Dispose();
                m_Feedback = null;
            }

            if (m_Vfx != null)
            {
                m_Vfx.Dispose();
                m_Vfx = null;
            }

            if (m_Views != null)
            {
                for (int i = 0; i < m_Views.Length; i++)
                {
                    if (m_Views[i] != null)
                        m_Views[i].Dispose();
                }
            }

            if (m_Recorder != null)
            {
                m_Recorder.Dispose();
                m_Recorder = null;
            }

            if (m_Performance != null)
            {
                m_Performance.Dispose();
                m_Performance = null;
            }

            if (m_Track != null)
            {
                m_Track.Dispose();
                m_Track = null;
            }

            if (m_CarRoot != null)
            {
                UnityEngine.Object.Destroy(m_CarRoot);
                m_CarRoot = null;
            }

            if (m_CameraObject != null)
            {
                UnityEngine.Object.Destroy(m_CameraObject);
                m_CameraObject = null;
            }

            if (m_LightObject != null)
            {
                UnityEngine.Object.Destroy(m_LightObject);
                m_LightObject = null;
            }

            RestorePhysics();

            m_BorrowedCamera = null;
            m_CameraRig = null;
            m_Views = null;
            m_Simulation = null;

            if (m_Config != null)
                m_Config.UseTrack(null);

            m_Config = null;

            if (m_Context.Services.TryGet(out IAssetProvider assets))
                assets.ReleaseAsset(RaceAddressableKeys.RaceConfig);
        }

        private void SpawnCars()
        {
            m_CarRoot = new GameObject("RaceCars");
            m_Views = new CarView[RaceConfigSO.CarCount];

            for (int i = 0; i < RaceConfigSO.CarCount; i++)
            {
                GameObject prefab = m_Config.CarCatalog.GetPrefab(i);
                GameObject instance = prefab != null
                    ? UnityEngine.Object.Instantiate(prefab, m_CarRoot.transform)
                    : new GameObject("Car_" + i);

                instance.name = "Car_" + i;
                m_Views[i] = new CarView(instance.transform, m_Simulation.GetCar(i), m_Path, m_Config);
                m_Views[i].Sync(1f, 1f, 0f);
            }
        }

        private void SetupCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                m_CameraObject = new GameObject("RaceCamera");
                camera = m_CameraObject.AddComponent<Camera>();
                m_CameraObject.AddComponent<AudioListener>();
            }
            else
            {
                m_BorrowedCamera = camera;
            }

            camera.farClipPlane = 600f;
            ApplyCameraLook(camera);

            m_CameraRig = new RaceCameraRig(camera, m_Simulation, m_Path, m_Config, m_Views);
            m_CameraRig.Sync(1f);
        }

        private void EnsureLight()
        {
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional && lights[i].isActiveAndEnabled)
                    return;
            }

            m_LightObject = new GameObject("RaceSunLight");
            Light sun = m_LightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.color = new Color(1f, 0.97f, 0.9f);
            m_LightObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        }

        private void SendMatchAnalytics()
        {
            m_AnalyticsSent = true;

            CarState player = m_Simulation.GetCar(m_Simulation.PlayerCarIndex);
            int finishOrder = player != null ? player.FinishOrder : RaceConfigSO.CarCount;
            bool won = finishOrder == 1;

            float finishTime = player != null ? player.FinishTime : m_Simulation.Time;

            if (won)
                EB.Analytics.Invoke(new MatchWinAnalytics(m_LevelIndex, finishTime));
            else
                EB.Analytics.Invoke(new MatchLoseAnalytics(m_LevelIndex, finishTime, finishOrder));
        }
    }
}
