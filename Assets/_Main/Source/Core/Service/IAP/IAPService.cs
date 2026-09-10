using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;

namespace CasualKit.Core
{
    public sealed class IAPService : Service, IIAPService
    {
        private readonly IAssetProvider m_Assets;
        private readonly ISaveService m_Save;
        private readonly Dictionary<string, string> m_ProductIdToKey = new Dictionary<string, string>();

        private IAPCatalogSO m_Catalog;
        private StoreController m_Store;
        private UniTaskCompletionSource<bool> m_PurchaseTcs;
        private UniTaskCompletionSource<bool> m_FetchTcs;
        private bool m_Ready;
        private bool m_ProductsReady;
        private bool m_StoreAvailable;

        public IAPService(IAssetProvider assets, ISaveService save)
        {
            m_Assets = assets;
            m_Save = save;
        }

        public async UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (m_Ready)
                return;

            m_Catalog = await m_Assets.LoadAsset<IAPCatalogSO>(AddressableKeys.IapCatalog, cancellationToken);
            BuildProductMaps();

#if UNITY_EDITOR
            m_StoreAvailable = false;
#else
            await ConnectStoreAsync(cancellationToken);
#endif

            m_Ready = true;
        }

        private async UniTask ConnectStoreAsync(CancellationToken cancellationToken)
        {
            m_Store = UnityIAPServices.StoreController();
            if (m_Store == null)
            {
                m_StoreAvailable = false;
                return;
            }

            m_Store.OnPurchasePending += OnPurchasePending;
            m_Store.OnPurchaseFailed += OnPurchaseFailed;
            m_Store.OnPurchaseConfirmed += OnPurchaseConfirmed;
            m_Store.OnProductsFetched += OnProductsFetched;
            m_Store.OnProductsFetchFailed += OnProductsFetchFailed;
            m_Store.OnStoreDisconnected += OnStoreDisconnected;

            await m_Store.Connect();
            if (cancellationToken.IsCancellationRequested)
            {
                m_StoreAvailable = false;
                return;
            }

            m_StoreAvailable = true;
            UniTaskCompletionSource<bool> fetchTcs = new UniTaskCompletionSource<bool>();
            m_FetchTcs = fetchTcs;
            int added = FetchCatalogProducts();
            if (added <= 0)
            {
                m_FetchTcs = null;
                return;
            }

            (bool timedOut, bool fetched) = await fetchTcs.Task.TimeoutWithoutException(TimeSpan.FromSeconds(8));
            if (!timedOut)
                m_ProductsReady = fetched;
        }

        public async UniTask<bool> PurchaseAsync(string productKey, CancellationToken cancellationToken = default)
        {
            if (!m_Ready)
                await InitializeAsync(cancellationToken);

            if (m_Catalog == null || !m_Catalog.TryGet(productKey, out IAPCatalogEntry entry))
            {
                EditorLog.Error("IAP product missing: " + productKey);
                return false;
            }

            if (!m_StoreAvailable || !m_ProductsReady || m_Store == null)
            {
#if UNITY_EDITOR
                ApplyReward(entry);
                return true;
#else
                EditorLog.Error("IAP store not ready for: " + productKey);
                return false;
#endif
            }

            if (m_PurchaseTcs != null)
                return false;

            m_PurchaseTcs = new UniTaskCompletionSource<bool>();
            using (cancellationToken.Register(() => m_PurchaseTcs.TrySetResult(false)))
            {
                m_Store.PurchaseProduct(productKey);
                return await m_PurchaseTcs.Task;
            }
        }

        private void BuildProductMaps()
        {
            m_ProductIdToKey.Clear();
            if (m_Catalog == null || m_Catalog.Entries == null)
                return;

            for (int i = 0; i < m_Catalog.Entries.Length; i++)
            {
                IAPCatalogEntry entry = m_Catalog.Entries[i];
                if (string.IsNullOrEmpty(entry.Key))
                    continue;

                m_ProductIdToKey[entry.Key] = entry.Key;
                if (!string.IsNullOrEmpty(entry.GooglePlayId))
                    m_ProductIdToKey[entry.GooglePlayId] = entry.Key;
                if (!string.IsNullOrEmpty(entry.AppStoreId))
                    m_ProductIdToKey[entry.AppStoreId] = entry.Key;
            }
        }

        private int FetchCatalogProducts()
        {
            if (m_Store == null || m_Catalog == null || m_Catalog.Entries == null)
                return 0;

            CatalogProvider catalogProvider = new CatalogProvider();
            int added = 0;
            for (int i = 0; i < m_Catalog.Entries.Length; i++)
            {
                IAPCatalogEntry entry = m_Catalog.Entries[i];
                if (string.IsNullOrEmpty(entry.Key))
                    continue;

                StoreSpecificIds specific = null;
                if (!string.IsNullOrEmpty(entry.GooglePlayId) || !string.IsNullOrEmpty(entry.AppStoreId))
                {
                    specific = new StoreSpecificIds();
                    if (!string.IsNullOrEmpty(entry.GooglePlayId))
                        specific.Add(entry.GooglePlayId, GooglePlay.Name);
                    if (!string.IsNullOrEmpty(entry.AppStoreId))
                        specific.Add(entry.AppStoreId, AppleAppStore.Name);
                }

                catalogProvider.AddProduct(entry.Key, ProductType.Consumable, specific);
                added++;
            }

            if (added == 0)
                return 0;

            catalogProvider.FetchProducts(m_Store.FetchProductsWithNoRetries);
            return added;
        }

        private void OnProductsFetched(List<Product> products)
        {
            m_ProductsReady = products != null && products.Count > 0;
            CompleteFetch(m_ProductsReady);
        }

        private void OnProductsFetchFailed(ProductFetchFailed _)
        {
            m_ProductsReady = false;
            CompleteFetch(false);
        }

        private void OnStoreDisconnected(StoreConnectionFailureDescription _)
        {
            m_StoreAvailable = false;
        }

        private void OnPurchasePending(PendingOrder order)
        {
            Product product = order?.CartOrdered?.Items()?.FirstOrDefault()?.Product;
            ProductDefinition definition = product != null ? product.definition : null;
            string productId = definition != null ? definition.id : null;
            if (string.IsNullOrEmpty(productId) && definition != null)
                productId = definition.storeSpecificId;

            bool granted = false;
            if (!string.IsNullOrEmpty(productId)
                && TryResolveCatalogKey(productId, out string key)
                && m_Catalog != null
                && m_Catalog.TryGet(key, out IAPCatalogEntry entry))
            {
                ApplyReward(entry);
                granted = true;
            }

            if (m_Store != null && order != null)
                m_Store.ConfirmPurchase(order);

            CompletePurchase(granted);
        }

        private bool TryResolveCatalogKey(string productId, out string key)
        {
            if (m_ProductIdToKey.TryGetValue(productId, out key))
                return true;

            if (m_Catalog?.Entries == null)
            {
                key = null;
                return false;
            }

            for (int i = 0; i < m_Catalog.Entries.Length; i++)
            {
                IAPCatalogEntry entry = m_Catalog.Entries[i];
                if (entry.GooglePlayId == productId || entry.AppStoreId == productId || entry.Key == productId)
                {
                    key = entry.Key;
                    return true;
                }
            }

            key = null;
            return false;
        }

        private void OnPurchaseFailed(FailedOrder _)
        {
            CompletePurchase(false);
        }

        private void OnPurchaseConfirmed(Order order)
        {
            if (order is FailedOrder)
                CompletePurchase(false);
        }

        private void CompleteFetch(bool success)
        {
            if (m_FetchTcs == null)
                return;

            UniTaskCompletionSource<bool> tcs = m_FetchTcs;
            m_FetchTcs = null;
            tcs.TrySetResult(success);
        }

        private void CompletePurchase(bool success)
        {
            if (m_PurchaseTcs == null)
                return;

            UniTaskCompletionSource<bool> tcs = m_PurchaseTcs;
            m_PurchaseTcs = null;
            tcs.TrySetResult(success);
        }

        private void ApplyReward(IAPCatalogEntry entry)
        {
            if (m_Save == null)
                return;

            switch (entry.RewardType)
            {
                case EIAPRewardType.Hearts:
                    m_Save.GrantHearts(entry.RewardAmount);
                    break;
                case EIAPRewardType.SoftCurrency:
                    m_Save.AddSoftCurrency(entry.RewardAmount);
                    break;
            }

            m_Save.FlushPending();
        }

        protected override void OnDispose()
        {
            CompleteFetch(false);
            CompletePurchase(false);
            if (m_Store == null)
                return;

            m_Store.OnPurchasePending -= OnPurchasePending;
            m_Store.OnPurchaseFailed -= OnPurchaseFailed;
            m_Store.OnPurchaseConfirmed -= OnPurchaseConfirmed;
            m_Store.OnProductsFetched -= OnProductsFetched;
            m_Store.OnProductsFetchFailed -= OnProductsFetchFailed;
            m_Store.OnStoreDisconnected -= OnStoreDisconnected;
            m_Store = null;
        }
    }
}
