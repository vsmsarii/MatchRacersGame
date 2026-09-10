using System;
using System.Collections.Generic;

namespace CasualKit.Core
{
    public sealed class GameLoop
    {
        private readonly List<ITickable> m_Tickable = new();
        private readonly List<IFixedTickable> m_FixedTickable = new();
        private readonly List<ILateTickable> m_LateTickable = new();

        private readonly List<ITickable> m_TickBuffer = new();
        private readonly List<IFixedTickable> m_FixedBuffer = new();
        private readonly List<ILateTickable> m_LateBuffer = new();

        public void Register(object obj)
        {
            if (obj is ITickable tickable && !m_Tickable.Contains(tickable))
                m_Tickable.Add(tickable);

            if (obj is IFixedTickable fixedTickable && !m_FixedTickable.Contains(fixedTickable))
                m_FixedTickable.Add(fixedTickable);

            if (obj is ILateTickable lateTickable && !m_LateTickable.Contains(lateTickable))
                m_LateTickable.Add(lateTickable);
        }

        public void Unregister(object obj)
        {
            if (obj is ITickable tickable)
                m_Tickable.Remove(tickable);

            if (obj is IFixedTickable fixedTickable)
                m_FixedTickable.Remove(fixedTickable);

            if (obj is ILateTickable lateTickable)
                m_LateTickable.Remove(lateTickable);
        }

        public void Tick(float deltaTime)
        {
            m_TickBuffer.Clear();
            m_TickBuffer.AddRange(m_Tickable);

            for (int i = 0; i < m_TickBuffer.Count; i++)
            {
                try
                {
                    m_TickBuffer[i].Tick(deltaTime);
                }
                catch (Exception exception)
                {
                    EditorLog.Error("Tick failed in " + m_TickBuffer[i].GetType().Name + ": " + exception);
                }
            }
        }

        public void FixedTick(float deltaTime)
        {
            m_FixedBuffer.Clear();
            m_FixedBuffer.AddRange(m_FixedTickable);

            for (int i = 0; i < m_FixedBuffer.Count; i++)
            {
                try
                {
                    m_FixedBuffer[i].FixedTick(deltaTime);
                }
                catch (Exception exception)
                {
                    EditorLog.Error("FixedTick failed in " + m_FixedBuffer[i].GetType().Name + ": " + exception);
                }
            }
        }

        public void LateTick(float deltaTime)
        {
            m_LateBuffer.Clear();
            m_LateBuffer.AddRange(m_LateTickable);

            for (int i = 0; i < m_LateBuffer.Count; i++)
            {
                try
                {
                    m_LateBuffer[i].LateTick(deltaTime);
                }
                catch (Exception exception)
                {
                    EditorLog.Error("LateTick failed in " + m_LateBuffer[i].GetType().Name + ": " + exception);
                }
            }
        }
    }
}
