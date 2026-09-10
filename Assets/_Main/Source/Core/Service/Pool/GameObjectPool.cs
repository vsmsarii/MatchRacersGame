using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CasualKit.Core
{
    public sealed class GameObjectPool : Service, IGameObjectPool
    {
        private readonly IAssetProvider m_Assets;
        private readonly Dictionary<string, Stack<GameObject>> m_Inactive = new();
        private readonly Dictionary<GameObject, string> m_KeyByInstance = new();
        private Transform m_Root;

        public GameObjectPool(IAssetProvider assets)
        {
            m_Assets = assets;
        }

        protected override void OnInitialize()
        {
            GameObject root = new GameObject("[GameObjectPool]");
            UnityEngine.Object.DontDestroyOnLoad(root);
            m_Root = root.transform;
        }

        public async UniTask<GameObject> Get(string key, Transform parent = null, CancellationToken cancellationToken = default)
        {
            if (TryPop(key, out GameObject instance))
            {
                instance.transform.SetParent(parent, false);
                instance.SetActive(true);
                return instance;
            }

            instance = await m_Assets.Instantiate(key, parent, cancellationToken);
            if (instance == null)
                return null;

            m_KeyByInstance[instance] = key;
            return instance;
        }

        public async UniTask<T> Get<T>(string key, Transform parent = null, CancellationToken cancellationToken = default) where T : Component
        {
            GameObject instance = await Get(key, parent, cancellationToken);
            if (instance == null)
                return null;

            T component = instance.GetComponent<T>();
            if (component == null)
            {
                Release(instance);
                return null;
            }

            return component;
        }

        public async UniTask Warmup(string key, int count, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key) || count <= 0)
                return;

            Stack<GameObject> stack = GetStack(key);
            for (int i = stack.Count; i < count; i++)
            {
                GameObject instance = await m_Assets.Instantiate(key, m_Root, cancellationToken);
                if (instance == null || cancellationToken.IsCancellationRequested)
                    return;

                m_KeyByInstance[instance] = key;
                instance.SetActive(false);
                stack.Push(instance);
            }
        }

        public void Release(GameObject instance)
        {
            if (instance == null)
                return;

            if (!m_KeyByInstance.TryGetValue(instance, out string key))
            {
                m_Assets.ReleaseInstance(instance);
                return;
            }

            if (IsPooled(instance))
                return;

            instance.SetActive(false);
            instance.transform.SetParent(m_Root, false);
            GetStack(key).Push(instance);
        }

        protected override void OnDispose()
        {
            foreach (GameObject instance in m_KeyByInstance.Keys)
            {
                if (instance != null)
                    m_Assets.ReleaseInstance(instance);
            }

            m_KeyByInstance.Clear();
            m_Inactive.Clear();

            if (m_Root != null)
                UnityEngine.Object.Destroy(m_Root.gameObject);

            m_Root = null;
        }

        private bool TryPop(string key, out GameObject instance)
        {
            instance = null;
            if (!m_Inactive.TryGetValue(key, out Stack<GameObject> stack))
                return false;

            while (stack.Count > 0)
            {
                instance = stack.Pop();
                if (instance != null)
                    return true;
            }

            return false;
        }

        private bool IsPooled(GameObject instance)
        {
            return !instance.activeSelf && instance.transform.parent == m_Root;
        }

        private Stack<GameObject> GetStack(string key)
        {
            if (!m_Inactive.TryGetValue(key, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>();
                m_Inactive[key] = stack;
            }

            return stack;
        }
    }
}
