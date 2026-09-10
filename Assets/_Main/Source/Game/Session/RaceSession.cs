using System;
using System.Threading;
using CasualKit.Core;
using CasualKit.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MatchRacers
{
    public sealed class RaceSession : IGameSession, ITickable
    {
        private const float ResultPanelDelay = 2.5f;

        private static readonly string[] GenericHudWidgets =
        {
            "Level", "Score", "Combo", "Health", "Immortal", "PowerBar"
        };

        public static uint SeedOverride;

        private readonly GameContext m_Context;

        private RaceConfigSO m_Config;
        private RacePath m_Path;
        private TrackBuilder m_Track;
        private RaceSimulation m_Simulation;
        private PlayerAgent m_PlayerAgent;
        private RaceInputReader m_Input;
        private CarView[] m_Views;
        private RaceCameraRig m_CameraRig;
        private RaceDebugOverlay m_Overlay;
        private RaceHudView m_Hud;
        private RaceRecorder m_Recorder;
        private RaceFeedback m_Feedback;
        private RaceModeSelectView m_ModeSelect;
        private RaceAudioPlayer m_RaceAudio;
        private RaceEnvironment m_Environment;
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
        private bool m_RunEndedSent;
        private bool m_ResultsShown;
        private float m_ResultDelayRemaining;

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

            m_Path = m_Config.HasCustomPath
                ? new RacePath(m_Config.Waypoints)
                : RacePath.CreateStraight(m_Config.RaceLengthMeters, m_Config.RunoutMeters);
            m_Track = new TrackBuilder(m_Config, m_Path);
            m_Track.Build();

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

            m_Environment = new RaceEnvironment(m_Config.Environment);
            m_Environment.Apply();
            EnsureLight();

            SetupCamera();

            m_RaceAudio = new RaceAudioPlayer(m_Config.RaceAudio, m_Config.NitroLevels,
                m_Context.Services.Get<IAudioService>(),
                m_Views != null && m_Views.Length > 0 ? m_Views[0].Root : null);

            m_Overlay = RaceDebugOverlay.Create(m_Simulation, this, m_Recorder);
            m_Feedback = new RaceFeedback(
                m_Context.Services.Get<IAudioService>(), m_CameraRig, m_RaceAudio, m_Simulation.PlayerCarIndex);

            m_Context.GameLoop.Register(this);
            m_Context.Services.Get<IAudioService>().PlayMusic(EAudioName.MusicGameplay);

            m_IsLoaded = true;
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!m_IsLoaded || m_IsPaused || m_Simulation == null)
                return;

            if (m_AwaitingSelection)
            {
                SyncPresentation(deltaTime);
                return;
            }

            if (m_Performance != null)
                m_Performance.Tick(Time.unscaledDeltaTime);

            int key = m_Input.ReadPressedKey();
            if (key != 0)
                m_PlayerAgent.Request(key);

            m_Simulation.Advance(deltaTime);

            if (m_Recorder != null)
                m_Recorder.Sample();

            SyncPresentation(deltaTime);
            UpdateFinishFlow(deltaTime);
        }

        private void SyncPresentation(float deltaTime)
        {
            for (int i = 0; i < m_Views.Length; i++)
                m_Views[i].Sync(deltaTime);

            if (m_CameraRig != null)
                m_CameraRig.Sync(deltaTime);

            if (m_Hud != null)
                m_Hud.Tick(deltaTime);

            if (m_Feedback != null)
                m_Feedback.Tick(deltaTime);
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

            if (m_Performance != null)
                m_Performance.Reset();

            if (m_ModeSelect != null)
            {
                m_ModeSelect.Dispose();
                m_ModeSelect = null;
            }

            m_AwaitingSelection = false;
        }

        public void BindHud(GameObject hudRoot, Action openSettings)
        {
            if (hudRoot == null)
                return;

            GameplayCanvasUI hud = hudRoot.GetComponent<GameplayCanvasUI>();
            if (hud == null)
                hud = hudRoot.GetComponentInChildren<GameplayCanvasUI>();
            if (hud == null)
                return;

            hud.BindSettings(openSettings);
            HideGenericHudWidgets(hudRoot.transform);

            if (m_Hud != null)
                m_Hud.Dispose();

            m_Hud = new RaceHudView(hudRoot.transform, m_Simulation, m_Config);

            if (m_ModeSelect != null)
                m_ModeSelect.Dispose();

            m_ModeSelect = m_AwaitingSelection
                ? new RaceModeSelectView(hudRoot.transform, RaceConfigSO.CarCount, BeginRace)
                : null;
        }

        public void RestartWithSeed(uint seed)
        {
            SeedOverride = seed;
            ISceneService scenes = m_Context.Services.Get<ISceneService>();
            scenes.Reload(ESceneName.Gameplay, m_Context.CancellationToken).Forget();
        }

        private static void HideGenericHudWidgets(Transform hudRoot)
        {
            Transform[] all = hudRoot.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < all.Length; i++)
            {
                for (int w = 0; w < GenericHudWidgets.Length; w++)
                {
                    if (all[i].name != GenericHudWidgets[w])
                        continue;

                    all[i].gameObject.SetActive(false);
                    break;
                }
            }
        }

        private void UpdateFinishFlow(float deltaTime)
        {
            if (m_Simulation.State != ERaceState.Finished || m_RunEndedSent)
                return;

            if (!m_ResultsShown)
            {
                m_ResultsShown = true;
                m_ResultDelayRemaining = ResultPanelDelay;

                if (m_Hud != null)
                    m_Hud.ShowResults();

                if (m_Recorder != null)
                    m_Recorder.Flush();

                return;
            }

            m_ResultDelayRemaining -= deltaTime;
            if (m_ResultDelayRemaining <= 0f)
                SendRunEnded();
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

            if (m_ModeSelect != null)
            {
                m_ModeSelect.Dispose();
                m_ModeSelect = null;
            }

            if (m_Environment != null)
            {
                m_Environment.Dispose();
                m_Environment = null;
            }

            if (m_RaceAudio != null)
            {
                m_RaceAudio.Dispose();
                m_RaceAudio = null;
            }

            if (m_Feedback != null)
            {
                m_Feedback.Dispose();
                m_Feedback = null;
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

            if (m_Hud != null)
            {
                m_Hud.Dispose();
                m_Hud = null;
            }

            if (m_Overlay != null)
            {
                m_Overlay.Dispose();
                m_Overlay = null;
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

            m_BorrowedCamera = null;
            m_CameraRig = null;
            m_Views = null;
            m_Simulation = null;
            m_Config = null;

            m_Context.Services.Get<IAssetProvider>().ReleaseAsset(RaceAddressableKeys.RaceConfig);
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
                m_Views[i].Sync(1f);
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

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = m_Environment != null
                ? m_Environment.BackgroundColor
                : new Color(0.09f, 0.11f, 0.14f);
            camera.farClipPlane = 600f;

            m_CameraRig = new RaceCameraRig(camera, m_Simulation, m_Path, m_Config);
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

        private void SendRunEnded()
        {
            m_RunEndedSent = true;

            CarState player = m_Simulation.GetCar(m_Simulation.PlayerCarIndex);
            int finishOrder = player != null ? player.FinishOrder : RaceConfigSO.CarCount;
            bool won = finishOrder == 1;

            float finishTime = player != null ? player.FinishTime : m_Simulation.Time;

            if (won)
                EB.Analytics.Invoke(new MatchWinAnalytics(m_LevelIndex, finishTime));
            else
                EB.Analytics.Invoke(new MatchLoseAnalytics(m_LevelIndex, finishTime, finishOrder));

            EB.Presentation.Invoke(new RunEnded(won, finishOrder, 0));
        }
    }
}
