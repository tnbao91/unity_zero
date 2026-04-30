using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zero.Core;

namespace Zero.UI
{
    /// <summary>
    /// Interface for popup data + result contract.
    /// </summary>
    public interface IPopup<in TData, in TResult>
    {
        UniTask OnOpenAsync(TData data, CancellationToken ct);
        UniTask OnCloseAsync(TResult result, CancellationToken ct);
    }

    /// <summary>
    /// Abstract base class for popups. Extends MonoBehaviour and implements IPopup.
    /// Subclasses override OnOpenAsync / OnCloseAsync to customize behavior;
    /// default transition uses PopupTransition enum + UITransitions utilities.
    /// </summary>
    public abstract class PopupBase<TData, TResult> : MonoBehaviour, IPopup<TData, TResult>
    {
        [SerializeField] private PopupTransition _transition = PopupTransition.Fade;
        [SerializeField] private float _transitionDuration = 0.2f;
        [SerializeField] private Vector2 _slideOffset = new Vector2(0, -100f);

        protected CanvasGroup CanvasGroup { get; private set; }
        protected PopupHandle<TResult> Handle { get; private set; }
        protected TData CurrentData { get; private set; }

        protected virtual void Awake()
        {
            CanvasGroup = GetComponent<CanvasGroup>();
            if (CanvasGroup == null)
            {
                CanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        public virtual async UniTask OnOpenAsync(TData data, CancellationToken ct)
        {
            CurrentData = data;
            await ApplyTransitionIn(ct);
        }

        public virtual async UniTask OnCloseAsync(TResult result, CancellationToken ct)
        {
            await ApplyTransitionOut(ct);
        }

        internal void SetHandle(PopupHandle<TResult> handle)
        {
            Handle = handle;
        }

        protected virtual async UniTask ApplyTransitionIn(CancellationToken ct)
        {
            switch (_transition)
            {
                case PopupTransition.None:
                    CanvasGroup.alpha = 1f;
                    break;
                case PopupTransition.Fade:
                    await UITransitions.FadeIn(CanvasGroup, _transitionDuration, ct);
                    break;
                case PopupTransition.Slide:
                    await UITransitions.SlideIn(GetComponent<RectTransform>(), _slideOffset, _transitionDuration, ct);
                    break;
                case PopupTransition.Scale:
                    await UITransitions.ScaleIn(transform, _transitionDuration, ct);
                    break;
            }
        }

        protected virtual async UniTask ApplyTransitionOut(CancellationToken ct)
        {
            switch (_transition)
            {
                case PopupTransition.None:
                    CanvasGroup.alpha = 0f;
                    break;
                case PopupTransition.Fade:
                    await UITransitions.FadeOut(CanvasGroup, _transitionDuration, ct);
                    break;
                case PopupTransition.Slide:
                    await UITransitions.SlideOut(GetComponent<RectTransform>(), _slideOffset, _transitionDuration, ct);
                    break;
                case PopupTransition.Scale:
                    await UITransitions.ScaleOut(transform, _transitionDuration, ct);
                    break;
            }
        }

        protected void ClosePopup(TResult result)
        {
            Handle?.Close(result);
        }
    }
}
