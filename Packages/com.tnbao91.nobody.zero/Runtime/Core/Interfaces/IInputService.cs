using R3;
using UnityEngine;

namespace Zero.Core
{
    public readonly struct SwipeInfo
    {
        public readonly Vector2 Direction;
        public readonly float Magnitude;
        public readonly float Velocity;

        public SwipeInfo(Vector2 direction, float magnitude, float velocity)
        {
            Direction = direction;
            Magnitude = magnitude;
            Velocity = velocity;
        }
    }

    public interface IInputService
    {
        Observable<Vector2> OnPointerDown { get; }
        Observable<Vector2> OnPointerUp { get; }
        Observable<Vector2> OnTap { get; }
        Observable<Vector2> OnDrag { get; }
        Observable<SwipeInfo> OnSwipe { get; }
        Observable<float> OnPinch { get; }
        Observable<Unit> OnEscape { get; }
    }
}
