using R3;
using UnityEngine;

namespace Zero.Core
{
    public interface IInputService
    {
        Observable<Vector2> OnTap { get; }
        Observable<Vector2> OnDrag { get; }
        Observable<int> OnGesture { get; }
    }
}
