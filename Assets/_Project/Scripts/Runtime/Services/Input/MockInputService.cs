using R3;
using UnityEngine;
using Zero.Core;

namespace Zero.Services.Input
{
    public sealed class MockInputService : IInputService
    {
        private readonly Subject<Vector2> _onTap = new();
        private readonly Subject<Vector2> _onDrag = new();
        private readonly Subject<int> _onGesture = new();

        public Observable<Vector2> OnTap => _onTap;
        public Observable<Vector2> OnDrag => _onDrag;
        public Observable<int> OnGesture => _onGesture;

        // Streams never emit in v1 — sample loop wires uGUI buttons directly.
        // v2 will swap to NewInputSystemService that polls Unity Input System.
    }
}
