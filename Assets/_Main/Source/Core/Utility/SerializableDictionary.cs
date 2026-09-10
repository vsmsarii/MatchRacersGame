using System;
using System.Collections.Generic;
using UnityEngine;

namespace CasualKit.Utility
{
    [Serializable]
    public sealed class SerializableDictionary<TKey, TValue> : ISerializationCallbackReceiver
    {
        [Serializable]
        public struct Entry
        {
            public TKey Key;
            public TValue Value;
        }

        [SerializeField] private List<Entry> m_Entries = new();

        [NonSerialized] private Dictionary<TKey, TValue> m_Lookup;
        [NonSerialized] private bool m_IsBuilt;

        public int Count
        {
            get
            {
                EnsureBuilt();
                return m_Lookup.Count;
            }
        }

        public TValue this[TKey key]
        {
            get
            {
                EnsureBuilt();
                return m_Lookup[key];
            }
            set
            {
                EnsureBuilt();
                m_Lookup[key] = value;
                WriteEntry(key, value);
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            EnsureBuilt();
            return m_Lookup.TryGetValue(key, out value);
        }

        public bool ContainsKey(TKey key)
        {
            EnsureBuilt();
            return m_Lookup.ContainsKey(key);
        }

        public void Add(TKey key, TValue value)
        {
            EnsureBuilt();
            m_Lookup.Add(key, value);
            m_Entries.Add(new Entry { Key = key, Value = value });
        }

        public bool Remove(TKey key)
        {
            EnsureBuilt();
            if (!m_Lookup.Remove(key))
                return false;

            for (int i = m_Entries.Count - 1; i >= 0; i--)
            {
                if (EqualityComparer<TKey>.Default.Equals(m_Entries[i].Key, key))
                {
                    m_Entries.RemoveAt(i);
                    break;
                }
            }

            return true;
        }

        public void Clear()
        {
            m_Entries.Clear();
            if (m_Lookup != null)
                m_Lookup.Clear();
            else
                m_Lookup = new Dictionary<TKey, TValue>();

            m_IsBuilt = true;
        }

        public Dictionary<TKey, TValue>.KeyCollection Keys
        {
            get
            {
                EnsureBuilt();
                return m_Lookup.Keys;
            }
        }

        public Dictionary<TKey, TValue>.ValueCollection Values
        {
            get
            {
                EnsureBuilt();
                return m_Lookup.Values;
            }
        }

        public Dictionary<TKey, TValue>.Enumerator GetEnumerator()
        {
            EnsureBuilt();
            return m_Lookup.GetEnumerator();
        }

        public IReadOnlyDictionary<TKey, TValue> AsReadOnly()
        {
            EnsureBuilt();
            return m_Lookup;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            m_IsBuilt = false;
            m_Lookup = null;
        }

        private void EnsureBuilt()
        {
            if (m_IsBuilt)
                return;

            m_Lookup = new Dictionary<TKey, TValue>(m_Entries.Count);
            for (int i = 0; i < m_Entries.Count; i++)
            {
                Entry entry = m_Entries[i];
                m_Lookup[entry.Key] = entry.Value;
            }

            m_IsBuilt = true;
        }

        private void WriteEntry(TKey key, TValue value)
        {
            for (int i = 0; i < m_Entries.Count; i++)
            {
                if (!EqualityComparer<TKey>.Default.Equals(m_Entries[i].Key, key))
                    continue;

                m_Entries[i] = new Entry { Key = key, Value = value };
                return;
            }

            m_Entries.Add(new Entry { Key = key, Value = value });
        }
    }
}
