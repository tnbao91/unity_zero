# Safe Area

## Overview

SafeAreaFitter automatically adjusts a RectTransform to fit within the device's safe area (the region not covered by notches, rounded corners, or other intrusions). Attach it to any UI element that needs to respect the safe area, and it updates on screen orientation changes.

## Public API

```csharp
public sealed class SafeAreaFitter : MonoBehaviour
{
    // No public API — component-only interface.
    // Add to a GameObject with a RectTransform and enable the component.
}
```

## Extension Points

No extension points. To customize safe area behavior, create your own MonoBehaviour that reads `Screen.safeArea` and applies custom logic.

## Examples

In your Canvas UI hierarchy:
1. Create a Panel (RectTransform) that should respect the safe area.
2. Attach SafeAreaFitter to the Panel GameObject.
3. Play; the panel automatically adjusts its anchors to the safe area.

Typical use case: a top HUD bar that should clear the notch.
```
Canvas (fullscreen)
  ├─ [SafeAreaFitter] TopHUD (anchors auto-adjusted)
  ├─ CenterContent
  └─ BottomHUD
```

## Known Limitations

- Safe area detection relies on `Screen.safeArea`, which is 0 on most non-notched devices.
- Unity Editor's Game view does not simulate notches; test on actual devices (iOS with notch/Dynamic Island, Android with waterdrop/hole-punch) for accurate behavior.
- SafeAreaFitter applies anchors only; if your RectTransform has non-zero `offsetMin` / `offsetMax`, they are reset to zero.
- Orientation changes are detected by polling `Screen.width` / `Screen.height` in `Update()`. Rapid orientation toggles may cause brief jitter.

## Design Rationale

Safe area support is mandatory for modern mobile apps. Rather than documenting the math of converting screen-space safe areas to canvas-space anchors, SafeAreaFitter encapsulates that conversion so consumers can attach it to any UI element and forget about it.

The component uses polling rather than `OnScreenOrientationChanged` callback (which is called at unpredictable times) because screen dimensions can change without triggering the callback (e.g., dynamic notch visibility on some Android devices).

SafeAreaFitter is `sealed` and component-only because extending it adds little value — the safe area is a device property, not a game property.
