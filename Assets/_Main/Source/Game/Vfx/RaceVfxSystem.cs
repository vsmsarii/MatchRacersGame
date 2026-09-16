using System;
using System.Threading;
using CasualKit.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MatchRacers
{
    public sealed class RaceVfxSystem
    {
        private const float TeleportMeters = 25f;

        private readonly CarVfxCatalogSO m_Catalog;
        private readonly IGameObjectPool m_Pool;
        private readonly Camera m_Camera;
        private readonly CarView[] m_Views;
        private readonly CarVfxRig[] m_Rigs;
        private readonly float[] m_LastDistance;
        private readonly Plane[] m_Planes = new Plane[6];
        private readonly bool[] m_Available = new bool[(int)ECarVfx.NitroFlame + 1];
        private readonly int[] m_Demand = new int[(int)ECarVfx.NitroFlame + 1];

        public int RigCount { get; }

        public RaceVfxSystem(CarVfxCatalogSO catalog, IGameObjectPool pool, IAssetProvider assets, Camera camera,
            CarView[] views)
        {
            m_Catalog = catalog;
            m_Pool = pool;
            m_Camera = camera;
            m_Views = views ?? Array.Empty<CarView>();
            m_Rigs = new CarVfxRig[m_Views.Length];
            m_LastDistance = new float[m_Views.Length];

            ResolveAvailability(assets);

            for (int i = 0; i < m_Views.Length; i++)
            {
                if (m_Views[i] == null || m_Views[i].Root == null)
                    continue;

                m_LastDistance[i] = m_Views[i].VisualDistance;

                CarVfxRig rig = m_Views[i].Root.GetComponent<CarVfxRig>();
                if (rig == null)
                    rig = m_Views[i].Root.GetComponentInChildren<CarVfxRig>(true);

                if (rig == null || catalog == null || pool == null)
                    continue;

                rig.Initialize(catalog, pool, m_Available);
                m_Rigs[i] = rig;
                RigCount++;

                for (int effect = 1; effect < m_Demand.Length; effect++)
                {
                    Transform[] anchors = rig.GetAnchors((ECarVfx)effect);
                    if (anchors != null)
                        m_Demand[effect] += anchors.Length;
                }
            }
        }

        private void ResolveAvailability(IAssetProvider assets)
        {
            if (m_Catalog == null)
                return;

            string missing = string.Empty;
            for (int i = 0; i < m_Catalog.EntryCount; i++)
            {
                CarVfxEntry entry = m_Catalog.GetEntry(i);
                int index = (int)entry.Effect;
                if (index <= 0 || index >= m_Available.Length || entry.Prefab == null)
                    continue;

                string address = CarVfxCatalogSO.GetAddress(entry.Effect);
                m_Available[index] = assets != null && assets.HasKey(address);

                if (!m_Available[index])
                    missing += (missing.Length > 0 ? ", " : string.Empty) + address;
            }

            if (missing.Length > 0)
                EditorLog.Warning("Car VFX not addressable: " + missing +
                                  ". Run MatchRacers/Addressables/Ensure Settings; these effects stay off until then.");
        }

        public static bool IsMoving(float previousDistance, float distance, float deltaTime, float threshold)
        {
            if (deltaTime <= 0f)
                return false;

            float travelled = Mathf.Abs(distance - previousDistance);
            return travelled < TeleportMeters && travelled / deltaTime > threshold;
        }

        public async UniTask WarmupAsync(CancellationToken cancellationToken)
        {
            if (m_Catalog == null || m_Pool == null || RigCount == 0)
                return;

            for (int i = 0; i < m_Catalog.EntryCount; i++)
            {
                CarVfxEntry entry = m_Catalog.GetEntry(i);
                int index = (int)entry.Effect;
                bool available = index > 0 && index < m_Available.Length && m_Available[index];
                if (!available)
                    continue;

                int count = Mathf.Max(entry.Warmup, m_Demand[index]);
                if (count <= 0)
                    continue;

                try
                {
                    await m_Pool.Warmup(CarVfxCatalogSO.GetAddress(entry.Effect), count, cancellationToken);
                }
                catch (Exception exception)
                {
                    EditorLog.Warning("Car VFX warmup failed for " + entry.Effect + " (" + exception.Message +
                                      "). Run MatchRacers/Addressables/Ensure Settings.");
                }

                if (cancellationToken.IsCancellationRequested)
                    return;
            }
        }

        public void Tick(float deltaTime)
        {
            if (RigCount == 0)
                return;

            bool cull = m_Camera != null;
            if (cull)
                GeometryUtility.CalculateFrustumPlanes(m_Camera, m_Planes);

            float threshold = m_Catalog.MovingSpeedThreshold;
            Vector3 size = Vector3.one * (m_Catalog.CullRadius * 2f);

            for (int i = 0; i < m_Rigs.Length; i++)
            {
                CarVfxRig rig = m_Rigs[i];
                if (rig == null)
                    continue;

                CarView view = m_Views[i];
                float distance = view.VisualDistance;
                bool moving = IsMoving(m_LastDistance[i], distance, deltaTime, threshold);
                m_LastDistance[i] = distance;

                bool visible = !cull || GeometryUtility.TestPlanesAABB(m_Planes, new Bounds(view.Root.position, size));
                CarState state = view.State;
                int activeKey = state.HasActiveBuff && !state.Finished ? state.ActiveBuffKey : 0;

                rig.Tick(deltaTime, moving, activeKey, visible);
            }
        }

        public void ResetAll()
        {
            for (int i = 0; i < m_Rigs.Length; i++)
            {
                if (m_Rigs[i] != null)
                    m_Rigs[i].ReleaseAll();

                if (m_Views[i] != null)
                    m_LastDistance[i] = m_Views[i].VisualDistance;
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < m_Rigs.Length; i++)
            {
                if (m_Rigs[i] != null)
                    m_Rigs[i].ReleaseAll();
            }
        }
    }
}
