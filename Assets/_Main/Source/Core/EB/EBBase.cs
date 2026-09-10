using System;
using System.Collections.Generic;

namespace CasualKit.Core
{
    public class EBBase
    {
        private readonly Dictionary<Type, Delegate> m_EventTable = new();

        public void Add<T>(Action<T> listener)
        {
            if (listener == null)
                return;

            Type key = typeof(T);
            if (m_EventTable.TryGetValue(key, out Delegate existing))
                m_EventTable[key] = (Action<T>)existing + listener;
            else
                m_EventTable[key] = listener;
        }

        public void Remove<T>(Action<T> listener)
        {
            if (listener == null)
                return;

            Type key = typeof(T);
            if (!m_EventTable.TryGetValue(key, out Delegate current))
                return;

            Action<T> updated = (Action<T>)current - listener;
            if (updated == null)
                m_EventTable.Remove(key);
            else
                m_EventTable[key] = updated;
        }

        public void Invoke<T>(T evt)
        {
            if (!m_EventTable.TryGetValue(typeof(T), out Delegate listeners))
                return;

            Action<T> action = (Action<T>)listeners;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Delegate[] invocationList = action.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                try
                {
                    ((Action<T>)invocationList[i]).Invoke(evt);
                }
                catch (Exception exception)
                {
                    EditorLog.Error("Event listener failed for " + typeof(T).Name + ": " + exception);
                }
            }
#else
            action.Invoke(evt);
#endif
        }

        public void Clear()
        {
            m_EventTable.Clear();
        }
    }
}
