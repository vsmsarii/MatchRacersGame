using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace CasualKit.Core
{
    public sealed class AssetProvider : Service, IAssetProvider
    {
        private readonly Dictionary<string, AsyncOperationHandle> m_AssetHandles = new();
        private readonly Dictionary<GameObject, AsyncOperationHandle<GameObject>> m_InstanceHandles = new();

        public async UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            AsyncOperationHandle handle = Addressables.InitializeAsync();
            await handle.Task.AsUniTask().AttachExternalCancellation(cancellationToken);
        }

        public async UniTask<T> LoadAsset<T>(string key, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            if (m_AssetHandles.TryGetValue(key, out AsyncOperationHandle existing))
                return existing.Result as T;

            AsyncOperationHandle<T> handle = default;
            try
            {
                handle = Addressables.LoadAssetAsync<T>(key);
                (bool canceled, T asset) = await handle.Task.AsUniTask().AttachExternalCancellation(cancellationToken).SuppressCancellationThrow();
                if (canceled || asset == null)
                {
                    if (handle.IsValid())
                        Addressables.Release(handle);
                    return null;
                }

                m_AssetHandles[key] = handle;
                return asset;
            }
            catch (Exception exception)
            {
                EditorLog.Error("Asset load failed for key " + key + ": " + exception.Message);
                if (handle.IsValid())
                    Addressables.Release(handle);
                return null;
            }
        }

        public void ReleaseAsset(string key)
        {
            if (!m_AssetHandles.TryGetValue(key, out AsyncOperationHandle handle))
                return;

            Addressables.Release(handle);
            m_AssetHandles.Remove(key);
        }

        public bool HasKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            foreach (IResourceLocator locator in Addressables.ResourceLocators)
            {
                if (locator.Locate(key, null, out IList<IResourceLocation> locations) && locations != null && locations.Count > 0)
                    return true;
            }

            return false;
        }

        public async UniTask<GameObject> Instantiate(string key, Transform parent = null, CancellationToken cancellationToken = default)
        {
            AsyncOperationHandle<GameObject> handle = default;
            try
            {
                handle = Addressables.InstantiateAsync(key, parent);
                (bool canceled, GameObject instance) = await handle.Task.AsUniTask().AttachExternalCancellation(cancellationToken).SuppressCancellationThrow();
                if (canceled || instance == null)
                {
                    if (handle.IsValid())
                        Addressables.Release(handle);
                    return null;
                }

                m_InstanceHandles[instance] = handle;
                return instance;
            }
            catch (Exception exception)
            {
                EditorLog.Error("Instantiate failed for key " + key + ": " + exception.Message);
                if (handle.IsValid())
                    Addressables.Release(handle);
                return null;
            }
        }

        public void ReleaseInstance(GameObject instance)
        {
            if (instance == null)
                return;

            if (m_InstanceHandles.TryGetValue(instance, out AsyncOperationHandle<GameObject> handle))
            {
                Addressables.ReleaseInstance(handle);
                m_InstanceHandles.Remove(instance);
                return;
            }

            Addressables.ReleaseInstance(instance);
        }

        protected override void OnDispose()
        {
            foreach (KeyValuePair<GameObject, AsyncOperationHandle<GameObject>> pair in m_InstanceHandles)
                Addressables.ReleaseInstance(pair.Value);

            m_InstanceHandles.Clear();

            foreach (KeyValuePair<string, AsyncOperationHandle> pair in m_AssetHandles)
                Addressables.Release(pair.Value);

            m_AssetHandles.Clear();
        }
    }
}
