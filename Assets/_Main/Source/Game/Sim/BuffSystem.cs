using UnityEngine;

namespace MatchRacers
{
    public sealed class BuffSystem
    {
        private readonly BuffTableSO m_Table;
        private readonly int m_WindowSteps;
        private readonly int m_CooldownSteps;
        private readonly int m_LaunchSteps;
        private readonly float m_LaunchDip;
        private readonly int m_StepHz;

        public int WindowSteps => m_WindowSteps;
        public int CooldownSteps => m_CooldownSteps;
        public int LaunchSteps => m_LaunchSteps;
        public float EnergyMax => m_Table.EnergyMax;

        public BuffSystem(BuffTableSO table, int stepHz)
        {
            m_Table = table;
            m_StepHz = stepHz;
            m_WindowSteps = table.GetWindowSteps(stepHz);
            m_CooldownSteps = Mathf.RoundToInt(table.GlobalCooldownSeconds * stepHz);
            if (m_CooldownSteps < 0)
                m_CooldownSteps = 0;

            m_LaunchSteps = table.GetLaunchSteps(stepHz);
            m_LaunchDip = table.LaunchDipMultiplier;
        }

        public void Regenerate(CarState car, float deltaTime)
        {
            car.Energy = Mathf.Min(m_Table.EnergyMax, car.Energy + m_Table.EnergyRegenPerSecond * deltaTime);
        }

        public void TickCooldown(CarState car)
        {
            if (car.CooldownStepsRemaining > 0)
                car.CooldownStepsRemaining--;

            for (int key = BuffTableSO.MinKey; key <= BuffTableSO.MaxKey; key++)
            {
                if (car.KeyCooldownSteps[key] > 0)
                    car.KeyCooldownSteps[key]--;
            }
        }

        public EBuffRejectReason TryActivate(CarState car, int key, ERaceState state)
        {
            if (!BuffTableSO.IsValidKey(key))
                return EBuffRejectReason.InvalidKey;

            if (state != ERaceState.Racing)
                return EBuffRejectReason.NotRacing;

            if (car.Finished)
                return EBuffRejectReason.AlreadyFinished;

            if (car.HasActiveBuff)
                return EBuffRejectReason.BuffActive;

            if (car.CooldownStepsRemaining > 0)
                return EBuffRejectReason.Cooldown;

            if (car.KeyCooldownSteps[key] > 0)
                return EBuffRejectReason.KeyCooldown;

            if (!m_Table.TryGetEnergyCost(key, out float energyCost))
                return EBuffRejectReason.InvalidKey;

            if (car.Energy < energyCost)
                return EBuffRejectReason.InsufficientEnergy;

            car.Energy -= energyCost;
            car.KeyCooldownSteps[key] = m_Table.GetKeyCooldownSteps(key, m_StepHz);
            car.SpentEnergy += energyCost;
            car.ActiveBuffKey = key;
            car.BuffStepsRemaining = m_WindowSteps;
            car.LaunchStepsRemaining = m_LaunchSteps;
            car.AcceptedBuffCount++;
            return EBuffRejectReason.None;
        }

        public float GetSpeedMultiplier(CarState car)
        {
            if (!car.HasActiveBuff)
                return 1f;

            return car.IsLaunching ? m_LaunchDip : BuffTableSO.GetSpeedMultiplier(car.ActiveBuffKey);
        }

        public bool ConsumeWindowStep(CarState car, out int expiredKey)
        {
            expiredKey = 0;
            if (!car.HasActiveBuff)
                return false;

            if (car.LaunchStepsRemaining > 0)
            {
                car.LaunchStepsRemaining--;
                return false;
            }

            car.BuffStepsRemaining--;
            if (car.BuffStepsRemaining > 0)
                return false;

            expiredKey = car.ActiveBuffKey;
            car.ActiveBuffKey = 0;
            car.CooldownStepsRemaining = m_CooldownSteps;
            return true;
        }

        public float GetRemainingWindowSeconds(CarState car, float deltaTime)
        {
            return car.BuffStepsRemaining * deltaTime;
        }

        public float GetRemainingCooldownSeconds(CarState car, float deltaTime)
        {
            return car.CooldownStepsRemaining * deltaTime;
        }

        public bool IsKeyReady(CarState car, int key)
        {
            return BuffTableSO.IsValidKey(key) && car.KeyCooldownSteps[key] <= 0;
        }

        public float GetRemainingKeyCooldownSeconds(CarState car, int key, float deltaTime)
        {
            return BuffTableSO.IsValidKey(key) ? car.KeyCooldownSteps[key] * deltaTime : 0f;
        }

        public bool CanUse(CarState car, int key)
        {
            return IsKeyReady(car, key)
                   && m_Table.TryGetEnergyCost(key, out float energyCost)
                   && car.Energy >= energyCost;
        }
    }
}
