using UnityEngine;
using UnityEngine.InputSystem;

namespace Zero.DevTools
{
    /// <summary>
    /// Lightweight FPS + frame-time overlay. Toggle with F2 (keyboard) in development builds.
    /// Renders via IMGUI for zero scene setup. Editor + dev builds only.
    /// </summary>
    public sealed class FpsOverlay : MonoBehaviour
    {
        private const int SampleCount = 60;
        private const float RefreshInterval = 0.25f; // seconds between text rebuilds

        // Cached rects + display string — OnGUI runs every frame (often twice: Layout + Repaint),
        // so allocating rects/strings there churns GC. Build the string at most 4x/sec in Update.
        private static readonly Rect BoxRect = new Rect(5, 5, 200, 80);
        private static readonly Rect LabelRect = new Rect(10, 10, 190, 70);

        private readonly float[] _frameTimes = new float[SampleCount];
        private int _frameIndex;
        private bool _isVisible = true;
        private float _refreshTimer;
        private string _displayText = string.Empty;
        private static FpsOverlay _instance;

        private void Awake()
        {
            _instance = this;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            {
                SetVisible(!_isVisible);
            }

            _frameTimes[_frameIndex] = Time.unscaledDeltaTime;
            _frameIndex = (_frameIndex + 1) % _frameTimes.Length;

            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= RefreshInterval)
            {
                _refreshTimer = 0f;
                RebuildDisplayText();
            }
        }

        private void RebuildDisplayText()
        {
            float total = 0f;
            for (int i = 0; i < _frameTimes.Length; i++) total += _frameTimes[i];
            float avgFrameTime = total / _frameTimes.Length;
            float fps = avgFrameTime > 0f ? 1f / avgFrameTime : 0f;
            float memoryMB = (float)(System.GC.GetTotalMemory(false) / (1024.0 * 1024.0));

            _displayText = $"FPS: {fps:F1}\nFrame: {avgFrameTime * 1000f:F2}ms\nMemory: {memoryMB:F1}MB";
        }

        public static void SetVisible(bool visible)
        {
            if (_instance != null)
            {
                _instance._isVisible = visible;
            }
        }

        private void OnGUI()
        {
            if (!_isVisible) return;

            GUI.Box(BoxRect, "", GUI.skin.box);
            GUI.Label(LabelRect, _displayText);
        }
    }
}
