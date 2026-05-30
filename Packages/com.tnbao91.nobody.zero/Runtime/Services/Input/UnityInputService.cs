using System;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Zero.Core;
using Object = UnityEngine.Object;

namespace Zero.Services.Input
{
    /// <summary>
    /// Real input service wrapping Unity Input System + EnhancedTouch.
    /// Gesture detection: tap (down→up within 200ms, drag <20px), swipe (≥50px within 500ms),
    /// drag (continuous pointer motion), pinch (two-finger only).
    /// </summary>
    public sealed class UnityInputService : IInputService, IDisposable
    {
        private const float TapWindowMs = 200f;
        private const float SwipeWindowMs = 500f;
        private const float TapDragThresholdPx = 20f;
        private const float SwipeMinDistancePx = 50f;

        private readonly Subject<Vector2> _onPointerDown = new();
        private readonly Subject<Vector2> _onPointerUp = new();
        private readonly Subject<Vector2> _onTap = new();
        private readonly Subject<Vector2> _onDrag = new();
        private readonly Subject<SwipeInfo> _onSwipe = new();
        private readonly Subject<float> _onPinch = new();
        private readonly Subject<Unit> _onEscape = new();

        private GameObject _driverGo;
        private InputDriver _driver;
        private bool _disposed;

        public Observable<Vector2> OnPointerDown => _onPointerDown;
        public Observable<Vector2> OnPointerUp => _onPointerUp;
        public Observable<Vector2> OnTap => _onTap;
        public Observable<Vector2> OnDrag => _onDrag;
        public Observable<SwipeInfo> OnSwipe => _onSwipe;
        public Observable<float> OnPinch => _onPinch;
        public Observable<Unit> OnEscape => _onEscape;

        public UnityInputService()
        {
            // Enable EnhancedTouch globally (once per app lifetime)
            EnhancedTouchSupport.Enable();

            // Create hidden driver GameObject
            _driverGo = new GameObject("[Zero.Input]")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(_driverGo);
            }

            _driver = _driverGo.AddComponent<InputDriver>();
            _driver.Initialize(this);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_driver != null)
            {
                _driver.Shutdown();
            }

            if (_driverGo != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(_driverGo);
                else
                    Object.DestroyImmediate(_driverGo);
            }

            EnhancedTouchSupport.Disable();

            _onPointerDown.Dispose();
            _onPointerUp.Dispose();
            _onTap.Dispose();
            _onDrag.Dispose();
            _onSwipe.Dispose();
            _onPinch.Dispose();
            _onEscape.Dispose();
        }

        /// <summary>Internal driver component that runs the input poll loop.</summary>
        private sealed class InputDriver : MonoBehaviour
        {
            private UnityInputService _service;
            private Vector2 _pointerDownPos;
            private float _pointerDownTime;
            private Vector2 _lastPointerPos;
            // Fixed two-slot buffer reused every frame; _previousTouchCount tells how many are valid.
            private readonly UnityEngine.InputSystem.EnhancedTouch.Touch[] _previousTouches =
                new UnityEngine.InputSystem.EnhancedTouch.Touch[2];
            private int _previousTouchCount;

            public void Initialize(UnityInputService service)
            {
                _service = service;
            }

            public void Shutdown()
            {
                // Cleanup if needed
            }

            private void Update()
            {
                if (_service._disposed) return;

                HandlePointerInput();
                HandleEscapeInput();
                HandlePinch();
            }

            private void HandlePointerInput()
            {
                var pointer = Pointer.current;

                if (pointer == null) return;

                // Determine if pointer down/up
                bool isDown = pointer.press.isPressed;
                Vector2 pointerPos = pointer.position.ReadValue();

                if (isDown && !_wasPointerDown)
                {
                    // Pointer down
                    _pointerDownPos = pointerPos;
                    _lastPointerPos = pointerPos;
                    _pointerDownTime = Time.time;
                    _service._onPointerDown.OnNext(pointerPos);
                    _wasPointerDown = true;
                }
                else if (!isDown && _wasPointerDown)
                {
                    // Pointer up — classify gesture
                    Vector2 dragDelta = pointerPos - _pointerDownPos;
                    float dragDistance = dragDelta.magnitude;
                    float elapsedMs = (Time.time - _pointerDownTime) * 1000f;

                    _service._onPointerUp.OnNext(pointerPos);

                    // Classify as tap or swipe
                    bool isTap = dragDistance < TapDragThresholdPx && elapsedMs < TapWindowMs;
                    bool isSwipe = dragDistance >= SwipeMinDistancePx && elapsedMs < SwipeWindowMs;

                    if (isTap)
                    {
                        _service._onTap.OnNext(pointerPos);
                    }
                    else if (isSwipe)
                    {
                        Vector2 direction = dragDelta.normalized;
                        float magnitude = dragDistance;
                        float velocity = magnitude / (elapsedMs / 1000f);
                        _service._onSwipe.OnNext(new SwipeInfo(direction, magnitude, velocity));
                    }

                    _wasPointerDown = false;
                }
                else if (isDown && _wasPointerDown && pointerPos != _lastPointerPos)
                {
                    // Drag
                    _service._onDrag.OnNext(pointerPos);
                }

                _lastPointerPos = pointerPos;
            }

            private void HandleEscapeInput()
            {
                var keyboard = Keyboard.current;

#if UNITY_ANDROID && !UNITY_EDITOR
                // Android back button via InputSystem
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                {
                    _service._onEscape.OnNext(Unit.Default);
                }
#elif UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR
                // PC escape key
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                {
                    _service._onEscape.OnNext(Unit.Default);
                }
#endif
            }

            private void HandlePinch()
            {
                var activeTouches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;

                if (activeTouches.Count < 2)
                {
                    _previousTouchCount = 0;
                    return;
                }

                var touch1 = activeTouches[0];
                var touch2 = activeTouches[1];

                float currentDistance = Vector2.Distance(touch1.screenPosition, touch2.screenPosition);

                if (_previousTouchCount >= 2)
                {
                    float previousDistance = Vector2.Distance(_previousTouches[0].screenPosition, _previousTouches[1].screenPosition);
                    if (previousDistance > 0)
                    {
                        float scaleRatio = currentDistance / previousDistance;
                        _service._onPinch.OnNext(scaleRatio);
                    }
                }

                // Reuse the fixed buffer — no per-frame allocation.
                _previousTouches[0] = touch1;
                _previousTouches[1] = touch2;
                _previousTouchCount = 2;
            }

            private bool _wasPointerDown;
        }
    }
}
