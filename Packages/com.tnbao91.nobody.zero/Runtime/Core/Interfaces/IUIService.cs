using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

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
        // Layer canvases live in the consumer's scene. A UIRoot MonoBehaviour
        // calls AttachRoot in OnEnable and DetachRoot in OnDisable. Push/Show/Toast
        // throw InvalidOperationException if no root is attached.
        void AttachRoot(IReadOnlyDictionary<UiLayer, Transform> layers);
        void DetachRoot();

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
