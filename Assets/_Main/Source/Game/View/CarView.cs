using System.Collections.Generic;
using UnityEngine;

namespace MatchRacers
{
    public sealed class CarView
    {
        private readonly Transform m_Root;
        private readonly CarState m_State;
        private readonly RacePath m_Path;
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

        public Transform Root => m_Root;
        public CarState State => m_State;

        public CarView(Transform root, CarState state, RacePath path, RaceConfigSO config)
        {
            m_Root = root;
            m_State = state;
            m_Path = path;
            m_Config = config;
            m_Wheels = CollectWheels(root);
            m_LateralOffset = config.GetLaneOffset(state.LaneIndex);

            m_TrailMaterial = RaceVisuals.CreateUnlitMaterial(Color.white);
            m_Trail = CreateTrail(root, config, m_TrailMaterial);

            if (state.IsPlayer)
                m_Marker = CreateMarker(root, config);

            m_Nitro = new NitroEffectController(root, state, config.NitroEffects, config.NitroLevels);
        }

        public void Sync(float deltaTime)
        {
            m_Path.Evaluate(m_State.Distance, out Vector3 position, out Vector3 forward);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            float targetOffset = m_Config.GetLaneOffset(m_State.LaneIndex);
            m_LateralOffset = Mathf.Lerp(m_LateralOffset, targetOffset, 1f - Mathf.Exp(-m_Config.LaneChangeSmoothing * deltaTime));

            m_Root.position = position + right * m_LateralOffset;
            m_Root.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, 0f, m_Bank);

            float targetBank = m_State.HasActiveBuff ? -(m_State.ActiveBuffKey - 1) * m_Config.BankPerBuffLevel : 0f;
            m_Bank = Mathf.Lerp(m_Bank, targetBank, 1f - Mathf.Exp(-8f * deltaTime));

            SpinWheels(deltaTime);
            SyncTrail();
            SyncMarker(deltaTime);

            if (m_Nitro != null)
                m_Nitro.Tick(deltaTime);
        }

        public void ClearEffects()
        {
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
                renderer.sharedMaterial = RaceVisuals.CreateUnlitMaterial(new Color(1f, 0.85f, 0.2f));
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            marker.transform.SetParent(root, false);
            marker.transform.localPosition = new Vector3(0f, config.PlayerMarkerHeight, 0f);
            marker.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
            return marker.transform;
        }

        private void SpinWheels(float deltaTime)
        {
            if (m_Wheels.Length == 0)
                return;

            m_WheelAngle += m_State.Speed / m_Config.WheelRadiusMeters * Mathf.Rad2Deg * deltaTime;
            if (m_WheelAngle > 360f)
                m_WheelAngle -= 360f;

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
