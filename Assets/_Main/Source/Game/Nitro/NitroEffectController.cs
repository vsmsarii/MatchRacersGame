using System.Collections.Generic;
using UnityEngine;

namespace MatchRacers
{
    public sealed class NitroEffectController
    {
        private const string ExhaustNodeFilter = "_Pipe_";
        private const float GlowRange = 6f;

        private readonly CarState m_State;
        private readonly NitroLevelTableSO m_Levels;

        private Light m_Glow;
        private int m_ActiveKey;
        private float m_Envelope;

        public NitroEffectController(Transform carRoot, CarState state, NitroLevelTableSO levels)
        {
            m_State = state;
            m_Levels = levels;

            CreateGlow(FindNozzles(carRoot, ExhaustNodeFilter));
        }

        public void Tick(float deltaTime)
        {
            if (m_Levels == null || m_Glow == null)
                return;

            bool active = m_State.HasActiveBuff && !m_State.Finished;
            if (active)
                m_ActiveKey = m_State.ActiveBuffKey;

            float rate = active
                ? deltaTime / m_Levels.EntryRampSeconds
                : deltaTime / m_Levels.ExitFadeSeconds;

            m_Envelope = Mathf.MoveTowards(m_Envelope, active ? 1f : 0f, rate);

            NitroLevelEntry level = m_Levels.Get(active ? m_State.ActiveBuffKey : Mathf.Max(1, m_ActiveKey));
            m_Glow.color = level.Color;
            m_Glow.intensity = level.LightIntensity * m_Envelope;
            m_Glow.enabled = m_Glow.intensity > 0.01f;

            if (!active && m_Envelope <= 0f)
                m_ActiveKey = 0;
        }

        public void ClearForRestart()
        {
            m_ActiveKey = 0;
            m_Envelope = 0f;

            if (m_Glow == null)
                return;

            m_Glow.intensity = 0f;
            m_Glow.enabled = false;
        }

        public void Dispose()
        {
            ClearForRestart();
        }

        private void CreateGlow(List<Transform> nozzles)
        {
            if (nozzles.Count == 0)
                return;

            GameObject host = new GameObject("NitroGlow");
            host.transform.SetParent(nozzles[0], false);

            m_Glow = host.AddComponent<Light>();
            m_Glow.type = LightType.Point;
            m_Glow.range = GlowRange;
            m_Glow.intensity = 0f;
            m_Glow.shadows = LightShadows.None;
            m_Glow.enabled = false;
        }

        private static List<Transform> FindNozzles(Transform root, string filter)
        {
            List<Transform> nozzles = new List<Transform>();
            Transform[] all = root.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != root && all[i].name.Contains(filter))
                    nozzles.Add(all[i]);
            }

            return nozzles;
        }
    }
}
