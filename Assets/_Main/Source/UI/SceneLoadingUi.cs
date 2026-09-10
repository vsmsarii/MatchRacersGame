using System.Threading;
using Cysharp.Threading.Tasks;
using CasualKit.Core;

namespace CasualKit.UI
{
    public sealed class SceneLoadingUi : Service, ISceneLoadingUi
    {
        public UniTask ShowAsync(CancellationToken cancellationToken = default)
        {
            return UIPanels.ShowLoading(cancellationToken);
        }

        public void SetProgress(float progress)
        {
            UIPanels.SetLoadingProgress(progress);
        }

        public void Hide()
        {
            UIPanels.HideLoading();
        }
    }
}
