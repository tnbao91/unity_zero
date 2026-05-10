using R3;
using UnityEngine;
using Zero.Core;

namespace Zero.Services.Input
{
    public sealed class MockInputService : IInputService
    {
        private readonly Subject<Vector2> _onPointerDown = new();
        private readonly Subject<Vector2> _onPointerUp = new();
        private readonly Subject<Vector2> _onTap = new();
        private readonly Subject<Vector2> _onDrag = new();
        private readonly Subject<SwipeInfo> _onSwipe = new();
        private readonly Subject<float> _onPinch = new();
        private readonly Subject<Unit> _onEscape = new();

        public Observable<Vector2> OnPointerDown => _onPointerDown;
        public Observable<Vector2> OnPointerUp => _onPointerUp;
        public Observable<Vector2> OnTap => _onTap;
        public Observable<Vector2> OnDrag => _onDrag;
        public Observable<SwipeInfo> OnSwipe => _onSwipe;
        public Observable<float> OnPinch => _onPinch;
        public Observable<Unit> OnEscape => _onEscape;

        // Streams never emit in v1 — sample loop wires uGUI buttons directly.
        // Phase 2 swaps this for UnityInputService that polls Unity Input System.
    }
}
