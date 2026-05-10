using UnityEngine;

namespace Zero.UI
{
    /// <summary>
    /// MonoBehaviour that fits the RectTransform to the device safe area.
    /// Automatically re-applies on screen orientation change.
    /// </summary>
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private int _lastScreenWidth;
        private int _lastScreenHeight;

        private void OnEnable()
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null)
            {
                Debug.LogError("[SafeAreaFitter] RectTransform component required.", gameObject);
                enabled = false;
                return;
            }

            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            ApplySafeArea();
        }

        private void Update()
        {
            // Detect screen orientation/size change
            if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
            {
                _lastScreenWidth = Screen.width;
                _lastScreenHeight = Screen.height;
                ApplySafeArea();
            }
        }

        private void ApplySafeArea()
        {
            Rect safeArea = Screen.safeArea;

            // Convert safe area from screen pixels to canvas-normalized coordinates
            Vector2 canvasSize = _rectTransform.root.GetComponent<RectTransform>().sizeDelta;

            Vector2 minAnchor = safeArea.min / canvasSize;
            Vector2 maxAnchor = safeArea.max / canvasSize;

            _rectTransform.anchorMin = minAnchor;
            _rectTransform.anchorMax = maxAnchor;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }
    }
}
