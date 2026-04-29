using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Zero.Core
{
    public enum UiLayer
    {
        Hud = 100,
        Popup = 200,
        Overlay = 300,
        System = 400
    }

    public enum PopupTransition
    {
        None,
        Fade,
        Slide,
        Scale
    }

    public interface IUIService
    {
        UniTask InitializeAsync(CancellationToken ct = default);
        UniTask<TResult> PushAsync<TPopup, TData, TResult>(
            TData data,
            PopupTransition transition = PopupTransition.Fade,
            float duration = 0.2f,
            CancellationToken ct = default)
            where TPopup : class;
        UniTask PopAsync(CancellationToken ct = default);
        UniTask ReplaceAsync<TPopup, TData, TResult>(
            TData data,
            PopupTransition transition = PopupTransition.Fade,
            float duration = 0.2f,
            CancellationToken ct = default)
            where TPopup : class;
        UniTask ShowScreenAsync<TScreen, TData>(
            TData data,
            CancellationToken ct = default)
            where TScreen : class;
        void ShowToast(string text, float duration = 2f);
        Transform GetLayerRoot(UiLayer layer);
    }
}
