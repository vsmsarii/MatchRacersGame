using System.Collections.Generic;
using UnityEngine;

namespace MatchRacers
{
    public sealed class NitroEffectController
    {
        private readonly CarState m_State;
        private readonly NitroLevelTableSO m_Levels;
        private readonly NitroEffectCatalogSO m_Catalog;

        private readonly List<ParticleSystem> m_Sustain = new List<ParticleSystem>();
        private readonly List<ParticleSystem> m_Entry = new List<ParticleSystem>();
        private readonly List<ParticleSystem> m_Exit = new List<ParticleSystem>();
        private readonly List<float> m_SustainBaseRate = new List<float>();
        private readonly List<float> m_SustainBaseSize = new List<float>();

        private readonly Transform m_Root;
        private Light m_Glow;
        private int m_ActiveKey;
        private float m_Envelope;

        public NitroEffectController(Transform carRoot, CarState state,
            NitroEffectCatalogSO catalog, NitroLevelTableSO levels)
        {
            m_Root = carRoot;
            m_State = state;
            m_Catalog = catalog;
            m_Levels = levels;

            if (catalog == null)
                return;

            List<Transform> nozzles = FindNozzles(carRoot, catalog.ExhaustNodeFilter);

            Spawn(ENitroEffect.Sustain, nozzles, m_Sustain);
            Spawn(ENitroEffect.Entry, nozzles, m_Entry);
            Spawn(ENitroEffect.Exit, nozzles, m_Exit);

            for (int i = 0; i < m_Sustain.Count; i++)
            {
                ParticleSystem.EmissionModule emission = m_Sustain[i].emission;
                ParticleSystem.MainModule main = m_Sustain[i].main;
                m_SustainBaseRate.Add(emission.rateOverTime.constant);
                m_SustainBaseSize.Add(main.startSizeMultiplier);
                m_Sustain[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            CreateGlow(nozzles);
        }

        public void Tick(float deltaTime)
        {
            if (m_Levels == null)
                return;

            bool active = m_State.HasActiveBuff && !m_State.Finished;

            if (active && m_State.ActiveBuffKey != m_ActiveKey)
                Begin(m_State.ActiveBuffKey);
            else if (!active && m_ActiveKey != 0)
                End();

            float targetEnvelope = active ? 1f : 0f;
            float rate = active
                ? deltaTime / m_Levels.EntryRampSeconds
                : deltaTime / m_Levels.ExitFadeSeconds;

            m_Envelope = Mathf.MoveTowards(m_Envelope, targetEnvelope, rate);
            ApplyEnvelope(active ? m_State.ActiveBuffKey : Mathf.Max(1, m_ActiveKey));
        }

        public void ClearForRestart()
        {
            m_ActiveKey = 0;
            m_Envelope = 0f;

            StopAll(m_Sustain, true);
            StopAll(m_Entry, true);
            StopAll(m_Exit, true);

            if (m_Glow != null)
                m_Glow.intensity = 0f;
        }

        public void Dispose()
        {
            ClearForRestart();
        }

        private void Begin(int key)
        {
            m_ActiveKey = key;

            for (int i = 0; i < m_Entry.Count; i++)
            {
                ApplyColor(m_Entry[i], m_Levels.Get(key).Color);
                m_Entry[i].Play(true);
            }

            for (int i = 0; i < m_Sustain.Count; i++)
            {
                ApplyColor(m_Sustain[i], m_Levels.Get(key).Color);
                if (!m_Sustain[i].isPlaying)
                    m_Sustain[i].Play(true);
            }
        }

        private void End()
        {
            for (int i = 0; i < m_Exit.Count; i++)
            {
                ApplyColor(m_Exit[i], m_Levels.Get(m_ActiveKey).Color);
                m_Exit[i].Play(true);
            }

            StopAll(m_Sustain, false);
            m_ActiveKey = 0;
        }

        private void ApplyEnvelope(int key)
        {
            NitroLevelEntry level = m_Levels.Get(key);

            for (int i = 0; i < m_Sustain.Count; i++)
            {
                ParticleSystem.EmissionModule emission = m_Sustain[i].emission;
                emission.rateOverTime = m_SustainBaseRate[i] * level.EmissionScale * m_Envelope;

                ParticleSystem.MainModule main = m_Sustain[i].main;
                main.startSizeMultiplier = m_SustainBaseSize[i] * Mathf.Lerp(0.4f, level.SizeScale, m_Envelope);
            }

            if (m_Glow != null)
            {
                m_Glow.color = level.Color;
                m_Glow.intensity = level.LightIntensity * m_Envelope;
                m_Glow.enabled = m_Glow.intensity > 0.01f;
            }
        }

        private void Spawn(ENitroEffect effect, List<Transform> nozzles, List<ParticleSystem> target)
        {
            if (!m_Catalog.TryGet(effect, out NitroEffectEntry entry))
                return;

            for (int i = 0; i < nozzles.Count; i++)
            {
                GameObject instance = Object.Instantiate(entry.Prefab, nozzles[i]);
                instance.transform.localPosition = entry.LocalPosition;
                instance.transform.localRotation = Quaternion.Euler(entry.LocalEuler);
                instance.transform.localScale = Vector3.one * entry.LocalScale;

                ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
                for (int s = 0; s < systems.Length; s++)
                {
                    ParticleSystem.MainModule main = systems[s].main;
                    main.maxParticles = Mathf.Min(main.maxParticles, m_Catalog.MaxParticlesPerSystem);
                    main.playOnAwake = false;
                    systems[s].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    target.Add(systems[s]);
                }
            }
        }

        private void CreateGlow(List<Transform> nozzles)
        {
            if (nozzles.Count == 0)
                return;

            GameObject host = new GameObject("NitroGlow");
            host.transform.SetParent(nozzles[0], false);

            m_Glow = host.AddComponent<Light>();
            m_Glow.type = LightType.Point;
            m_Glow.range = 6f;
            m_Glow.intensity = 0f;
            m_Glow.shadows = LightShadows.None;
            m_Glow.enabled = false;
        }

        private static void ApplyColor(ParticleSystem system, Color color)
        {
            ParticleSystem.MainModule main = system.main;
            main.startColor = color;
        }

        private static void StopAll(List<ParticleSystem> systems, bool clear)
        {
            for (int i = 0; i < systems.Count; i++)
            {
                systems[i].Stop(true, clear
                    ? ParticleSystemStopBehavior.StopEmittingAndClear
                    : ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private static List<Transform> FindNozzles(Transform root, string filter)
        {
            List<Transform> nozzles = new List<Transform>();

            if (!string.IsNullOrEmpty(filter))
            {
                Transform[] all = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].name.Contains(filter) && all[i].gameObject.activeInHierarchy)
                        nozzles.Add(all[i]);
                }
            }

            if (nozzles.Count == 0)
            {
                GameObject fallback = new GameObject("NitroNozzle");
                fallback.transform.SetParent(root, false);
                fallback.transform.localPosition = new Vector3(0f, 0.45f, -1.15f);
                fallback.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                nozzles.Add(fallback.transform);
            }

            return nozzles;
        }
    }
}
