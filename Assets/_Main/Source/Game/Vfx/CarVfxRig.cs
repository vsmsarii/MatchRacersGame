using System;
using System.Collections.Generic;
using CasualKit.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MatchRacers
{
    public sealed class CarVfxRig : MonoBehaviour
    {
        private static readonly HashSet<string> s_MissingAddresses = new HashSet<string>();

        [Header("Idle Smoke")]
        [SerializeField] private Transform[] m_IdleSmoke = new Transform[0];

        [Header("Cruise Flame")]
        [SerializeField] private Transform[] m_CruiseFlame = new Transform[0];

        [Header("Nitro Flame")]
        [SerializeField] private Transform[] m_NitroFlame = new Transform[0];

        private sealed class Slot
        {
            public ECarVfx Effect;
            public string Address;
            public Transform Anchor;
            public GameObject Instance;
            public ParticleSystem[] Systems;
            public float Scale;
            public float LastTarget;
            public bool Overshooting;
            public bool Pending;
            public int Generation;
        }

        private readonly List<Slot> m_Slots = new List<Slot>();
        private CarVfxCatalogSO m_Catalog;
        private IGameObjectPool m_Pool;
        private bool[] m_Available;
        private bool m_WasVisible = true;

        public int SlotCount => m_Slots.Count;

        public Transform[] GetAnchors(ECarVfx effect)
        {
            switch (effect)
            {
                case ECarVfx.IdleSmoke: return m_IdleSmoke;
                case ECarVfx.CruiseFlame: return m_CruiseFlame;
                case ECarVfx.NitroFlame: return m_NitroFlame;
                default: return Array.Empty<Transform>();
            }
        }

        public void Initialize(CarVfxCatalogSO catalog, IGameObjectPool pool, bool[] available)
        {
            ReleaseAll();
            m_Slots.Clear();

            m_Catalog = catalog;
            m_Pool = pool;
            m_Available = available;
            m_WasVisible = true;

            AddSlots(ECarVfx.IdleSmoke);
            AddSlots(ECarVfx.CruiseFlame);
            AddSlots(ECarVfx.NitroFlame);
        }

        public void Tick(float deltaTime, bool moving, int activeBuffKey, bool visible)
        {
            if (m_Catalog == null || m_Pool == null)
                return;

            bool nitro = m_Catalog.IsNitro(activeBuffKey);

            for (int i = 0; i < m_Slots.Count; i++)
            {
                Slot slot = m_Slots[i];
                float target = IsAvailable(slot.Effect) && CarVfxCatalogSO.IsWanted(slot.Effect, moving, nitro)
                    ? m_Catalog.GetTargetScale(slot.Effect, activeBuffKey)
                    : 0f;

                if (!visible)
                {
                    Release(slot);
                    slot.Scale = 0f;
                    continue;
                }

                if (!m_WasVisible)
                {
                    slot.Scale = target;
                    slot.LastTarget = target;
                    slot.Overshooting = false;
                }
                else
                {
                    slot.Scale = StepScale(slot, target, deltaTime);
                }

                if (slot.Scale > 0f || target > 0f)
                    Acquire(slot);
                else
                    Release(slot);

                if (slot.Instance != null)
                    slot.Instance.transform.localScale = Vector3.one * slot.Scale;
            }

            m_WasVisible = visible;
        }

        public void ReleaseAll()
        {
            for (int i = 0; i < m_Slots.Count; i++)
            {
                Release(m_Slots[i]);
                m_Slots[i].Scale = 0f;
                m_Slots[i].LastTarget = 0f;
                m_Slots[i].Overshooting = false;
            }
        }

        private void OnDestroy()
        {
            ReleaseAll();
        }

        private bool IsAvailable(ECarVfx effect)
        {
            int index = (int)effect;
            return m_Available == null || (index >= 0 && index < m_Available.Length && m_Available[index]);
        }

        private void AddSlots(ECarVfx effect)
        {
            Transform[] anchors = GetAnchors(effect);
            if (anchors == null)
                return;

            string address = CarVfxCatalogSO.GetAddress(effect);
            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i] == null)
                    continue;

                m_Slots.Add(new Slot { Effect = effect, Address = address, Anchor = anchors[i] });
            }
        }

        private float StepScale(Slot slot, float target, float deltaTime)
        {
            if (!m_Catalog.TryGet(slot.Effect, out CarVfxEntry entry))
                return target;

            return StepScale(entry, slot.Scale, target, deltaTime, ref slot.LastTarget, ref slot.Overshooting);
        }

        public static float StepScale(CarVfxEntry entry, float scale, float target, float deltaTime,
            ref float lastTarget, ref bool overshooting)
        {
            if (!Mathf.Approximately(target, lastTarget))
            {
                overshooting = entry.Overshoot > 0f && target > lastTarget && target > 0f;
                lastTarget = target;
            }

            if (overshooting)
            {
                float peak = target + entry.Overshoot;
                float next = Mathf.MoveTowards(scale, peak, peak / entry.GrowSeconds * deltaTime);
                if (next >= peak)
                    overshooting = false;

                return next;
            }

            if (scale > target && target > 0f && entry.Overshoot > 0f && scale <= target + entry.Overshoot + 0.0001f)
                return Mathf.MoveTowards(scale, target, entry.Overshoot / entry.SettleSeconds * deltaTime);

            float seconds = target > scale ? entry.GrowSeconds : entry.ShrinkSeconds;
            float speed = Mathf.Max(target, scale, 0.0001f) / seconds;
            return Mathf.MoveTowards(scale, target, speed * deltaTime);
        }

        private void Acquire(Slot slot)
        {
            if (slot.Instance != null || slot.Pending)
                return;

            slot.Pending = true;
            slot.Generation++;
            SpawnAsync(slot, slot.Generation).Forget();
        }

        private async UniTaskVoid SpawnAsync(Slot slot, int generation)
        {
            GameObject instance = null;
            try
            {
                instance = await m_Pool.Get(slot.Address, slot.Anchor);
            }
            catch (Exception exception)
            {
                ReportMissing(slot.Address, exception.Message);
            }

            bool current = this != null && slot.Generation == generation && slot.Anchor != null;
            if (current)
                slot.Pending = false;

            if (instance == null)
            {
                if (current)
                    ReportMissing(slot.Address, "pool returned nothing");
                return;
            }

            if (!current)
            {
                m_Pool.Release(instance);
                return;
            }

            Transform view = instance.transform;
            view.SetParent(slot.Anchor, false);
            view.localPosition = Vector3.zero;
            view.localRotation = Quaternion.identity;
            view.localScale = Vector3.one * slot.Scale;

            slot.Instance = instance;
            slot.Systems = instance.GetComponentsInChildren<ParticleSystem>(true);

            bool hasEntry = m_Catalog.TryGet(slot.Effect, out CarVfxEntry entry);
            bool followCar = hasEntry && entry.FollowCar;
            bool prewarm = hasEntry && entry.Prewarm;

            for (int i = 0; i < slot.Systems.Length; i++)
            {
                ParticleSystem system = slot.Systems[i];
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);

                ParticleSystem.MainModule main = system.main;
                main.playOnAwake = false;

                if (main.loop)
                    main.prewarm = prewarm;

                if (followCar)
                {
                    main.simulationSpace = ParticleSystemSimulationSpace.Custom;
                    main.customSimulationSpace = transform;
                }

                system.Play(false);
            }
        }

        private void Release(Slot slot)
        {
            slot.Generation++;
            slot.Pending = false;

            if (slot.Instance == null)
                return;

            if (slot.Systems != null)
            {
                for (int i = 0; i < slot.Systems.Length; i++)
                {
                    if (slot.Systems[i] != null)
                        slot.Systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            if (m_Pool != null)
                m_Pool.Release(slot.Instance);

            slot.Instance = null;
            slot.Systems = null;
        }

        private static void ReportMissing(string address, string reason)
        {
            if (s_MissingAddresses.Add(address))
                EditorLog.Warning("Car VFX '" + address + "' could not be spawned (" + reason +
                                  "). Run MatchRacers/Addressables/Ensure Settings.");
        }
    }
}
