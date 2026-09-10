using System.Threading;
using Cysharp.Threading.Tasks;
using CasualKit.Core;

namespace CasualKit.UI
{
    public static class UIPanels
    {
        public const int LoadingLayer = 2;

        public static async UniTask OpenAsync(
            EUIPanel panel,
            int layer = 0,
            bool additive = false,
            bool locked = false,
            CancellationToken cancellationToken = default)
        {
            UniTaskCompletionSource source = new UniTaskCompletionSource();

            void OnOpened(UIPanelOpened opened)
            {
                if (opened.Panel != panel)
                    return;

                EB.Presentation.Remove<UIPanelOpened>(OnOpened);
                source.TrySetResult();
            }

            EB.Presentation.Add<UIPanelOpened>(OnOpened);
            EB.Presentation.Invoke(new OpenUIPanelEvent(panel, layer, additive, locked));

            bool canceled = await source.Task.AttachExternalCancellation(cancellationToken).SuppressCancellationThrow();
            if (canceled)
                EB.Presentation.Remove<UIPanelOpened>(OnOpened);
        }

        public static void Close(EUIPanel panel)
        {
            EB.Presentation.Invoke(new CloseUIPanelEvent(panel));
        }

        public static async UniTask ShowLoading(CancellationToken cancellationToken = default)
        {
            await OpenAsync(EUIPanel.Loading, LoadingLayer, additive: false, locked: true, cancellationToken);
            SetLoadingProgress(0f);
        }

        public static void SetLoadingProgress(float progress)
        {
            EB.Presentation.Invoke(new LoadingProgressChanged(progress));
        }

        public static void HideLoading()
        {
            Close(EUIPanel.Loading);
        }
    }
}
