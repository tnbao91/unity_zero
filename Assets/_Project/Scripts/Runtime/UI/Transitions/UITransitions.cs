using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;

namespace Zero.UI
{
    /// <summary>
    /// Static utility class for UI transitions powered by LitMotion.
    /// All methods use LitMotion.BindTo extensions for smooth animations.
    /// </summary>
    public static class UITransitions
    {
        public static async UniTask FadeIn(CanvasGroup canvasGroup, float duration, CancellationToken ct = default)
        {
            canvasGroup.alpha = 0f;
            await LMotion.Create(0f, 1f, duration)
                .BindToCanvasGroupAlpha(canvasGroup)
                .ToUniTask(cancellationToken: ct);
        }

        public static async UniTask FadeOut(CanvasGroup canvasGroup, float duration, CancellationToken ct = default)
        {
            canvasGroup.alpha = 1f;
            await LMotion.Create(1f, 0f, duration)
                .BindToCanvasGroupAlpha(canvasGroup)
                .ToUniTask(cancellationToken: ct);
        }

        public static async UniTask SlideIn(RectTransform rectTransform, Vector2 fromOffset, float duration, CancellationToken ct = default)
        {
            Vector2 startPos = rectTransform.anchoredPosition + fromOffset;
            Vector2 endPos = rectTransform.anchoredPosition;
            rectTransform.anchoredPosition = startPos;

            await LMotion.Create(startPos, endPos, duration)
                .BindToAnchoredPosition(rectTransform)
                .ToUniTask(cancellationToken: ct);
        }

        public static async UniTask SlideOut(RectTransform rectTransform, Vector2 toOffset, float duration, CancellationToken ct = default)
        {
            Vector2 startPos = rectTransform.anchoredPosition;
            Vector2 endPos = startPos + toOffset;

            await LMotion.Create(startPos, endPos, duration)
                .BindToAnchoredPosition(rectTransform)
                .ToUniTask(cancellationToken: ct);
        }

        public static async UniTask ScaleIn(Transform transform, float duration, CancellationToken ct = default)
        {
            transform.localScale = Vector3.zero;
            await LMotion.Create(Vector3.zero, Vector3.one, duration)
                .BindToLocalScale(transform)
                .ToUniTask(cancellationToken: ct);
        }

        public static async UniTask ScaleOut(Transform transform, float duration, CancellationToken ct = default)
        {
            transform.localScale = Vector3.one;
            await LMotion.Create(Vector3.one, Vector3.zero, duration)
                .BindToLocalScale(transform)
                .ToUniTask(cancellationToken: ct);
        }
    }
}
