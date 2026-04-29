# Input Service

## Overview

Wraps Unity's Input System (v1.19.0+) + EnhancedTouch for cross-platform pointer and gesture detection. Emits observables for pointer events (down/up/drag), gestures (tap/swipe/pinch), and input termination (Esc/Android back). No initialization overhead — events available immediately after DI resolution.

## Public API

```csharp
public interface IInputService
{
    Observable<Vector2> OnPointerDown { get; }
    Observable<Vector2> OnPointerUp { get; }
    Observable<Vector2> OnTap { get; }           // pointer down→up, <200ms, <20px drag
    Observable<Vector2> OnDrag { get; }          // continuous while held, ≥1px motion
    Observable<SwipeInfo> OnSwipe { get; }       // (direction, magnitude px, velocity px/s)
    Observable<float> OnPinch { get; }           // scale ratio (two-finger only)
    Observable<Unit> OnEscape { get; }           // Esc key or Android back button
}

public readonly struct SwipeInfo
{
    public readonly Vector2 Direction;           // normalized
    public readonly float Magnitude;             // distance in pixels
    public readonly float Velocity;              // pixels per second
}
```

**Gesture thresholds (configurable in source, currently hardcoded):**
- Tap: 200ms window, <20px drag
- Swipe: ≥50px distance, <500ms window
- Pinch: two-finger only, emits per-frame scale ratio

**Implementation:** `UnityInputService` (real) vs `MockInputService` (mock). Swap via `#if ZERO_USE_MOCK_INPUT` in `InputServiceInstaller.cs`.

## Extension Points

1. **Gesture thresholds:** Edit constants in `UnityInputService.cs` (TapWindowMs, SwipeMinDistancePx, etc.) and recompile. No interface change needed.
2. **Long-press:** Add a timer in the driver's `InputDriver.Update()`, emit on threshold. Long-press is not currently shipped (tap only).
3. **Multi-touch beyond pinch:** EnhancedTouch provides `Touch.activeTouches` list — extend the pinch handler to classify multi-touch patterns (4-finger tap, etc.).

## Examples

**Listening to taps:**
```csharp
var service = container.Resolve<IInputService>();
service.OnTap.Subscribe(pos => 
    Debug.Log($"Tapped at {pos}"));
```

**Listening to swipes with velocity:**
```csharp
service.OnSwipe.Subscribe(swipe =>
{
    if (swipe.Magnitude > 100f)
    {
        Debug.Log($"Fast swipe {swipe.Direction}, v={swipe.Velocity} px/s");
    }
});
```

**Handling escape (pause menu):**
```csharp
service.OnEscape.Subscribe(_ => 
    stateMachine.TransitionTo(StateType.Paused));
```

## Known Limitations

1. **InputSystem not mockable headless:** `UnityInputService` cannot be tested in CI without device input. `MockInputService` is available for headless flows. EditMode tests cover gesture classification logic only (pure functions), not the actual input polling.

2. **Pinch limited to two fingers:** Multi-finger gestures beyond pinch (3-finger pan, 4-finger rotation) are not classified. Consumer can extend via EnhancedTouch API if needed.

3. **Android back button:** Detected via `Keyboard.escapeKey.wasPressedThisFrame` (InputSystem routes it there). If Android back button doesn't fire, verify Input System's `Android Gamepad Configuration` in `Edit → Project Settings → Input System Package`.

4. **No hardware acceleration:** Gestures are software-detected (distance/time thresholds). Not suitable for 120fps+ edge-case games; acceptable for hybrid casual (60fps).

## Design Rationale

**Why EnhancedTouch over legacy TouchInput?**  
Input System v1.19.0+ provides EnhancedTouch as the official touch + pointer unification layer. Legacy `Input.touches` is deprecated. EnhancedTouch handles cross-platform normalization (mouse = pointer, touch = pointer, touchpad = pointer).

**Why gesture detection inside the service?**  
Consumers expect `OnTap`, `OnSwipe` as first-class events, not raw pointer sequences they must decode. Embedding thresholds in the service keeps the API clean; consumers can stil access raw OnPointerDown/Up if needed.

**Why IDisposable?**  
`UnityInputService` manages a hidden driver GameObject and EnhancedTouch global state. On disposal (e.g., when switching scenes or in tests), we tear down both. Mock doesn't dispose anything.

**Why Observable instead of UnityEvent?**  
R3 observables are the project's reactive standard (cross-service). Events compose cleanly with `.Where()`, `.DistinctUntilChanged()`, etc., enabling complex input chains without hand-rolled state.
