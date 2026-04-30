using UnityEngine;
using UnityEngine.InputSystem;

namespace Zero.DevTools
{
    public sealed class FpsOverlay : MonoBehaviour
    {
        private float[] _frameTimes = new float[60];
        private int _frameIndex;
        private bool _isVisible = true;
        private static FpsOverlay _instance;

        private void Awake()
        {
            _instance = this;
        }

        private void Update()
        {
            // Toggle visibility on F2
            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            {
                SetVisible(!_isVisible);
            }

            // Record frame time
            _frameTimes[_frameIndex] = Time.unscaledDeltaTime;
            _frameIndex = (_frameIndex + 1) % _frameTimes.Length;
        }

        private void OnGUI()
        {
            if (!_isVisible)
                return;

            var avgFrameTime = 0f;
            foreach (var t in _frameTimes)
            {
                avgFrameTime += t;
            }
            avgFrameTime /= _frameTimes.Length;

            var fps = avgFrameTime > 0 ? 1f / avgFrameTime : 0f;
            var memoryMB = System.GC.GetTotalMemory(false) / (1024f * 1024f);

            var text = $"FPS: {fps:F1}\nFrame: {avgFrameTime * 1000f:F2}ms\nMemory: {memoryMB:F1}MB";

            GUI.backgroundColor = Color.black * 0.7f;
            GUI.Box(new Rect(5, 5, 200, 80), "", GUI.skin.box);
            GUI.backgroundColor = Color.white;

            GUI.color = Color.white;
            GUI.Label(new Rect(10, 10, 190, 70), text);
            GUI.color = Color.white;
        }

        public static void SetVisible(bool visible)
        {
            if (_instance != null)
            {
                _instance._isVisible = visible;
            }
        }
    }
}
