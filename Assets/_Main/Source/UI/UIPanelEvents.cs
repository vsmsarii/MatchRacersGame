using UnityEngine;

namespace CasualKit.UI
{
    public readonly struct OpenUIPanelEvent
    {
        public readonly EUIPanel Panel;
        public readonly int Layer;
        public readonly bool Additive;
        public readonly bool Locked;

        public OpenUIPanelEvent(EUIPanel panel, int layer = 0, bool additive = false, bool locked = false)
        {
            Panel = panel;
            Layer = layer < 0 ? 0 : layer;
            Additive = additive;
            Locked = locked;
        }
    }

    public readonly struct CloseUIPanelEvent
    {
        public readonly EUIPanel Panel;

        public CloseUIPanelEvent(EUIPanel panel)
        {
            Panel = panel;
        }
    }

    public readonly struct CloseAllUIPanelsEvent
    {
        public readonly int Layer;

        public CloseAllUIPanelsEvent(int layer = -1)
        {
            Layer = layer;
        }
    }

    public readonly struct UIPanelOpened
    {
        public readonly EUIPanel Panel;
        public readonly int Layer;
        public readonly GameObject Instance;

        public UIPanelOpened(EUIPanel panel, int layer, GameObject instance)
        {
            Panel = panel;
            Layer = layer;
            Instance = instance;
        }
    }

    public readonly struct LoadingProgressChanged
    {
        public readonly float Progress;

        public LoadingProgressChanged(float progress)
        {
            Progress = progress < 0f ? 0f : (progress > 1f ? 1f : progress);
        }
    }
}
