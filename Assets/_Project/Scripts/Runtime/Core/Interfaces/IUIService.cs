using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Zero.Core
{
    public enum UiLayer
    {
        Hud,
        Popup,
        Overlay,
        System
    }

    public interface IPopupHandle : IDisposable
    {
        UniTask WaitForCloseAsync(CancellationToken ct = default);
    }

    public interface IUIService
    {
        UniTask InitializeAsync(CancellationToken ct = default);
        UniTask<IPopupHandle> ShowPopupAsync(string popupId, UiLayer layer = UiLayer.Popup, CancellationToken ct = default);
        void ShowToast(string message, float duration = 2f);
        void HideAll(UiLayer layer);
    }
}
