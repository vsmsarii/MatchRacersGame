using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using CasualKit.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace CasualKit.UI
{
    public sealed class UIRoot : MonoBehaviour
    {
        private const int MaxDisabledCount = 3;
        private const int LayerSortingBase = 100;
        private const int LayerSortingStride = 100;

        private IAssetProvider m_AssetProvider;
        private UIPanelCatalogSO m_Catalog;
        private bool m_IsInitialized;
        private readonly Dictionary<EUIPanel, PanelHandle> m_ActivePanels = new();
        private readonly List<PanelHandle> m_DisabledPanels = new();
        [SerializeField] private Transform[] m_Layers;
        private readonly HashSet<EUIPanel> m_Opening = new();

        public void Initialize(IAssetProvider assetProvider, UIPanelCatalogSO catalog)
        {
            if (m_IsInitialized)
                return;

            DontDestroyOnLoad(gameObject);

            m_AssetProvider = assetProvider;
            m_Catalog = catalog;

            EB.Presentation.Add<OpenUIPanelEvent>(OnOpenPanel);
            EB.Presentation.Add<CloseUIPanelEvent>(OnClosePanel);
            EB.Presentation.Add<CloseAllUIPanelsEvent>(OnCloseAllPanels);

            m_IsInitialized = true;
        }

        private void OnDestroy()
        {
            if (!m_IsInitialized)
                return;

            EB.Presentation.Remove<OpenUIPanelEvent>(OnOpenPanel);
            EB.Presentation.Remove<CloseUIPanelEvent>(OnClosePanel);
            EB.Presentation.Remove<CloseAllUIPanelsEvent>(OnCloseAllPanels);

            DestroyAllPanels();
            m_IsInitialized = false;
        }

        private void OnOpenPanel(OpenUIPanelEvent payload) => OpenPanelAsync(payload).Forget();

        private void OnClosePanel(CloseUIPanelEvent payload)
        {
            if (!m_ActivePanels.TryGetValue(payload.Panel, out PanelHandle handle))
                return;

            CloseActivePanel(handle, forceClose: true);
        }

        private void OnCloseAllPanels(CloseAllUIPanelsEvent payload)
        {
            if (payload.Layer < 0)
            {
                DestroyAllPanels();
                return;
            }

            DestroyPanelsOnLayer(payload.Layer);
        }

        private async UniTaskVoid OpenPanelAsync(OpenUIPanelEvent payload)
        {
            if (payload.Panel == EUIPanel.None || m_Catalog == null)
                return;

            if (m_ActivePanels.TryGetValue(payload.Panel, out PanelHandle active))
            {
                ActivateHandle(active, payload);
                return;
            }

            if (m_Opening.Contains(payload.Panel))
                return;

            if (TryReopen(payload))
                return;

            if (!m_Catalog.TryGetPrefab(payload.Panel, out AssetReferenceGameObject prefab))
                return;

            m_Opening.Add(payload.Panel);
            await OpenPanelCore(payload, prefab);
            m_Opening.Remove(payload.Panel);
        }

        private async UniTask OpenPanelCore(OpenUIPanelEvent payload, AssetReferenceGameObject prefab)
        {
            GameObject prefabAsset = await m_AssetProvider.LoadAsset<GameObject>(prefab.RuntimeKey.ToString());
            if (prefabAsset == null || !m_IsInitialized)
                return;

            if (!payload.Additive)
                CloseNonLockedActivePanelsExcept(payload.Panel, payload.Layer);

            Transform layerRoot = GetOrCreateLayer(payload.Layer);
            if (layerRoot == null)
                return;

            DestroyOldestPanelIfFull();

            GameObject instance = Instantiate(prefabAsset, layerRoot, false);
            PanelHandle handle = new PanelHandle
            {
                Panel = payload.Panel,
                Instance = instance,
                Locked = payload.Locked,
                Layer = payload.Layer,
            };
            ApplyLayer(handle);
            m_ActivePanels[payload.Panel] = handle;
            PublishOpened(handle);
        }

        private bool TryReopen(OpenUIPanelEvent payload)
        {
            for (int index = 0; index < m_DisabledPanels.Count; index++)
            {
                PanelHandle handle = m_DisabledPanels[index];
                if (handle.Panel != payload.Panel)
                    continue;

                m_DisabledPanels.RemoveAt(index);
                ActivateHandle(handle, payload);
                return true;
            }

            return false;
        }

        private void ActivateHandle(PanelHandle handle, OpenUIPanelEvent payload)
        {
            if (!payload.Additive)
                CloseNonLockedActivePanelsExcept(payload.Panel, payload.Layer);

            handle.Locked = payload.Locked;
            handle.Layer = payload.Layer;
            handle.Instance.SetActive(true);
            ApplyLayer(handle);
            m_ActivePanels[payload.Panel] = handle;
            PublishOpened(handle);
        }

        private void ApplyLayer(PanelHandle handle)
        {
            Transform layer = GetOrCreateLayer(handle.Layer);
            handle.Instance.transform.SetParent(layer, false);
            handle.Instance.transform.SetAsLastSibling();

            Canvas canvas = handle.Instance.GetComponent<Canvas>();
            if (canvas == null)
                return;

            canvas.overrideSorting = true;
            canvas.sortingOrder = LayerSortingBase + handle.Layer * LayerSortingStride + handle.Instance.transform.GetSiblingIndex();
        }

        private Transform GetOrCreateLayer(int layer)
        {
            if (layer < 0 || layer >= m_Layers.Length)
                return null;

            return m_Layers[layer];
        }

        private void CloseNonLockedActivePanelsExcept(EUIPanel except, int layer)
        {
            EUIPanel[] panels = new EUIPanel[m_ActivePanels.Count];
            m_ActivePanels.Keys.CopyTo(panels, 0);

            for (int index = 0; index < panels.Length; index++)
            {
                EUIPanel panel = panels[index];
                if (panel == except)
                    continue;

                if (!m_ActivePanels.TryGetValue(panel, out PanelHandle handle))
                    continue;

                if (handle.Layer != layer)
                    continue;

                CloseActivePanel(handle, forceClose: false);
            }
        }

        private void CloseActivePanel(PanelHandle handle, bool forceClose)
        {
            if (!forceClose && handle.Locked)
                return;

            if (!m_ActivePanels.Remove(handle.Panel))
                return;

            handle.Locked = false;
            DisablePanel(handle);
        }

        private void DisablePanel(PanelHandle handle)
        {
            DestroyOldestPanelIfFull();
            handle.Instance.SetActive(false);
            m_DisabledPanels.Add(handle);
        }

        private void DestroyOldestPanelIfFull()
        {
            while (m_DisabledPanels.Count >= MaxDisabledCount)
            {
                Destroy(m_DisabledPanels[0].Instance);
                m_DisabledPanels.RemoveAt(0);
            }
        }

        private void DestroyPanelsOnLayer(int layer)
        {
            EUIPanel[] panels = new EUIPanel[m_ActivePanels.Count];
            m_ActivePanels.Keys.CopyTo(panels, 0);

            for (int index = 0; index < panels.Length; index++)
            {
                if (!m_ActivePanels.TryGetValue(panels[index], out PanelHandle handle))
                    continue;

                if (handle.Layer != layer)
                    continue;

                m_ActivePanels.Remove(handle.Panel);
                Destroy(handle.Instance);
            }

            for (int index = m_DisabledPanels.Count - 1; index >= 0; index--)
            {
                if (m_DisabledPanels[index].Layer != layer)
                    continue;

                Destroy(m_DisabledPanels[index].Instance);
                m_DisabledPanels.RemoveAt(index);
            }
        }

        private void DestroyAllPanels()
        {
            foreach (PanelHandle handle in m_ActivePanels.Values)
                Destroy(handle.Instance);

            for (int index = 0; index < m_DisabledPanels.Count; index++)
                Destroy(m_DisabledPanels[index].Instance);

            m_ActivePanels.Clear();
            m_DisabledPanels.Clear();
            m_Opening.Clear();
        }

        private static void PublishOpened(PanelHandle handle)
        {
            EB.Presentation.Invoke(new UIPanelOpened(handle.Panel, handle.Layer, handle.Instance));
        }

        private sealed class PanelHandle
        {
            public EUIPanel Panel;
            public GameObject Instance;
            public bool Locked;
            public int Layer;
        }
    }
}
