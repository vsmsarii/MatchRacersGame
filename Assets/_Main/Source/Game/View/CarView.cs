using System.Collections.Generic;
using UnityEngine;

namespace MatchRacers
{
    public sealed class CarView
    {
        private readonly Transform m_Root;
        private readonly CarState m_State;
        private RacePath m_Path;
        private readonly RaceConfigSO m_Config;
        private readonly Transform[] m_Wheels;
        private readonly TrailRenderer m_Trail;
        private readonly Material m_TrailMaterial;
        private readonly Transform m_Marker;
        private readonly NitroEffectController m_Nitro;

        private float m_LateralOffset;
        private float m_WheelAngle;
        private float m_Bank;
        private float m_MarkerPhase;
        private bool m_Coasting;
        private float m_CoastElapsed;
        private float m_CoastStop;
        private float m_VisualDistance;
        private int m_LastBuffKey;
        private float m_LaunchTimer;
        private float m_LaunchOffset;
        private bool m_Launching;

        public Transform Root => m_Root;
        public CarState State => m_State;
        public float VisualDistance => m_VisualDistance;

        public CarView(Transform root, CarState state, RacePath path, RaceConfigSO config)
        {
            m_Root = root;
            m_State = state;
            m_Path = path;
            m_Config = config;
            m_Wheels = CollectWheels(root);
            m_LateralOffset = config.GetLaneOffset(state.LaneIndex);

            m_TrailMaterial = RaceVisuals.CreateUnlitMaterial(config.UnlitShader, Color.white);
            m_Trail = CreateTrail(root, config, m_TrailMaterial);

            if (state.IsPlayer)
                m_Marker = CreateMarker(root, config);

            m_Nitro = new NitroEffectController(root, state, config.NitroLevels);
        }

        public void Sync(float deltaTime, float interpolationAlpha, float renderTime)
        {
            float distance = ResolveDistance(deltaTime, interpolationAlpha, renderTime);
            m_VisualDistance = distance;

            float rendered = distance + UpdateLaunchOffset(deltaTime);
            m_Path.Evaluate(rendered, out Vector3 position, out Vector3 forward);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            float targetOffset = m_Config.GetLaneOffset(m_State.LaneIndex);
            m_LateralOffset = Mathf.Lerp(m_LateralOffset, targetOffset, 1f - Mathf.Exp(-m_Config.LaneChangeSmoothing * deltaTime));

            m_Root.position = position + right * m_LateralOffset;
            m_Root.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, 0f, m_Bank);

            float targetBank = m_State.HasActiveBuff ? -(m_State.ActiveBuffKey - 1) * m_Config.BankPerBuffLevel : 0f;
            m_Bank = Mathf.Lerp(m_Bank, targetBank, 1f - Mathf.Exp(-8f * deltaTime));

            SpinWheels(rendered);
            SyncTrail();
            SyncMarker(deltaTime);

            if (m_Nitro != null)
                m_Nitro.Tick(deltaTime);
        }

        private float UpdateLaunchOffset(float deltaTime)
        {
            NitroLevelTableSO levels = m_Config.NitroLevels;
            int key = m_State.ActiveBuffKey;

            if (levels == null || levels.LaunchDipMeters <= 0f || m_State.Finished)
            {
                m_LastBuffKey = key;
                m_Launching = false;
                m_LaunchOffset = 0f;
                return 0f;
            }

            if (key > m_LastBuffKey)
            {
                m_Launching = true;
                m_LaunchTimer = 0f;
            }

            m_LastBuffKey = key;

            if (!m_Launching)
                return 0f;

            m_LaunchTimer += deltaTime;

            if (m_LaunchTimer < levels.LaunchDipSeconds)
            {
                m_LaunchOffset = -levels.LaunchDipMeters * Mathf.SmoothStep(0f, 1f, m_LaunchTimer / levels.LaunchDipSeconds);
                return m_LaunchOffset;
            }

            float recovered = (m_LaunchTimer - levels.LaunchDipSeconds) / levels.LaunchRecoverSeconds;
            if (recovered >= 1f)
            {
                m_Launching = false;
                m_LaunchOffset = 0f;
                return 0f;
            }

            m_LaunchOffset = -levels.LaunchDipMeters * (1f - Mathf.SmoothStep(0f, 1f, recovered));
            return m_LaunchOffset;
        }

        private float ResolveDistance(float deltaTime, float interpolationAlpha, float renderTime)
        {
            if (!m_State.Finished)
            {
                m_Coasting = false;
                return m_State.GetRenderDistance(interpolationAlpha);
            }

            if (!m_Coasting)
            {
                m_Coasting = true;
                m_CoastElapsed = renderTime - m_State.FinishTime;
                m_CoastStop = FinishCoast.GetStopDistance(m_Config, m_State.FinishSpeed, m_State.CarIndex,
                    m_State.FinishTime, m_Path.IsClosed);
            }
            else
            {
                m_CoastElapsed += deltaTime;
            }

            return m_Config.RaceLengthMeters + FinishCoast.Evaluate(m_State.FinishSpeed, m_CoastStop, m_CoastElapsed);
        }

        public void SetPath(RacePath path)
        {
            m_Path = path;
            m_Coasting = false;
        }

        public void ClearEffects()
        {
            m_Launching = false;
            m_LaunchOffset = 0f;
            m_LaunchTimer = 0f;
            m_LastBuffKey = 0;

            if (m_Trail != null)
                m_Trail.Clear();

            if (m_Nitro != null)
                m_Nitro.ClearForRestart();
        }

        public void Dispose()
        {
            if (m_Nitro != null)
                m_Nitro.Dispose();

            if (m_TrailMaterial != null)
                Object.Destroy(m_TrailMaterial);
        }

        private void SyncTrail()
        {
            if (m_Trail == null)
                return;

            if (!m_State.HasActiveBuff)
            {
                m_Trail.emitting = false;
                return;
            }

            Color color;
            float width;

            if (m_Config.NitroLevels != null)
            {
                NitroLevelEntry level = m_Config.NitroLevels.Get(m_State.ActiveBuffKey);
                color = level.Color;
                width = level.TrailWidth;
            }
            else
            {
                color = RaceVisuals.GetBuffColor(m_State.ActiveBuffKey);
                width = RaceVisuals.GetBuffWidth(m_State.ActiveBuffKey);
            }

            m_TrailMaterial.color = color;
            if (m_TrailMaterial.HasProperty("_BaseColor"))
                m_TrailMaterial.SetColor("_BaseColor", color);

            m_Trail.startColor = color;
            m_Trail.endColor = new Color(color.r, color.g, color.b, 0f);
            m_Trail.startWidth = width;
            m_Trail.endWidth = 0f;
            m_Trail.emitting = true;
        }

        private void SyncMarker(float deltaTime)
        {
            if (m_Marker == null)
                return;

            m_MarkerPhase += deltaTime * 3.2f;
            float bob = Mathf.Sin(m_MarkerPhase) * 0.12f;
            m_Marker.localPosition = new Vector3(0f, m_Config.PlayerMarkerHeight + bob, 0f);
            m_Marker.localRotation = Quaternion.Euler(0f, m_MarkerPhase * 40f, 45f);
        }

        private static TrailRenderer CreateTrail(Transform root, RaceConfigSO config, Material material)
        {
            GameObject host = new GameObject("BuffTrail");
            host.transform.SetParent(root, false);
            host.transform.localPosition = new Vector3(0f, 0.45f, -1.1f);

            TrailRenderer trail = host.AddComponent<TrailRenderer>();
            trail.time = config.BuffTrailSeconds;
            trail.material = material;
            trail.numCapVertices = 2;
            trail.minVertexDistance = 0.15f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
            return trail;
        }

        private static Transform CreateMarker(Transform root, RaceConfigSO config)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "PlayerMarker";

            Collider collider = marker.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = RaceVisuals.CreateUnlitMaterial(config.UnlitShader, new Color(1f, 0.85f, 0.2f));
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            marker.transform.SetParent(root, false);
            marker.transform.localPosition = new Vector3(0f, config.PlayerMarkerHeight, 0f);
            marker.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
            return marker.transform;
        }

        private void SpinWheels(float distance)
        {
            if (m_Wheels.Length == 0)
                return;

            m_WheelAngle = Mathf.Repeat(distance / m_Config.WheelRadiusMeters * Mathf.Rad2Deg, 360f);

            for (int i = 0; i < m_Wheels.Length; i++)
                m_Wheels[i].localRotation = Quaternion.Euler(m_WheelAngle, 0f, 0f);
        }

        private static Transform[] CollectWheels(Transform root)
        {
            List<Transform> wheels = new List<Transform>();
            Transform[] all = root.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name.Contains("_Wheel_"))
                    wheels.Add(all[i]);
            }

            return wheels.ToArray();
        }
    }
}
