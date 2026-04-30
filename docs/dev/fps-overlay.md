# FPS Overlay

## Overview

`FpsOverlay` is a small IMGUI text widget showing FPS, average frame time, and managed memory. Toggle with the F2 key. Lives in `Zero.DevTools` (gated behind `defineConstraints: ["UNITY_EDITOR || DEVELOPMENT_BUILD"]`). Spawned alongside the [Cheat Console](cheat-console.md) at `RuntimeInitializeOnLoadMethod(AfterSceneLoad)`.

## What it shows

```
FPS: 59.9
Frame: 16.69ms
Memory: 142.3MB
```

| Line | Source |
|---|---|
| `FPS` | `1f / avgFrameTime` over the last 60 `Time.unscaledDeltaTime` samples (rolling). |
| `Frame` | `avgFrameTime * 1000f` ms. Same 60-sample window. |
| `Memory` | `System.GC.GetTotalMemory(allocateMore: false) / 1MB`. Managed heap only — not native, not GPU. |

The 60-sample window smooths out single-frame spikes; for raw `Time.unscaledDeltaTime` reads, profile in Unity Profiler.

## Public API

```csharp
namespace Zero.DevTools
{
    public sealed class FpsOverlay : MonoBehaviour
    {
        public static void SetVisible(bool visible);
    }
}
```

The static `SetVisible` is what `FpsToggleCommand` (one of the [built-in console commands](cheat-console.md#built-in-commands)) calls. It works as long as the singleton instance is alive (set in `Awake`).

## Toggle inputs

| Input | Behavior |
|---|---|
| F2 (keyboard) | Toggles visibility. |
| `fps show` / `fps hide` (cheat console) | Sets visibility explicitly. |

No mobile gesture; on-device debugging usually pairs with a Bluetooth keyboard or the cheat console (4-finger tap).

## Extension Points

**Add metrics** by editing `Assets/_Project/Scripts/Runtime/DevTools/FpsOverlay.cs`. Common additions:

```csharp
// Render thread pressure
GUILayout.Label($"DrawCalls: {UnityEngine.Rendering.GraphicsBuffer.GetTextureInfo(...)}"); // or via FrameDebugger

// Battery / thermal
GUILayout.Label($"Battery: {SystemInfo.batteryLevel:P0}");
GUILayout.Label($"Thermal: {Application.thermalState}");

// Network
GUILayout.Label($"Reachability: {Application.internetReachability}");
```

Keep the overlay light — it runs every `OnGUI` call which on mobile is per-camera-render. Avoid string allocation hotpaths; use `ZString.Concat` if you add many lines.

**Hide unconditionally** in DEVELOPMENT_BUILD (e.g. for QA testers who shouldn't see metrics). Easiest is to remove the `AddComponent<FpsOverlay>()` call in `DevToolsBootstrap.cs`, or move it behind your own define:

```csharp
#if UNITY_EDITOR
    root.AddComponent<FpsOverlay>();
#endif
```

**Reposition** — currently top-left fixed at `Rect(5, 5, 200, 80)`. If safe-area or an HUD overlaps, edit the `Rect` constructor in `OnGUI`. `SafeAreaFitter` is uGUI-only and does not interact with IMGUI.

## Examples

Inspect frame budget while testing a particle-heavy scene:
1. Press Play in `Play.unity`.
2. F2 → overlay appears.
3. Adjust quality settings; watch `Frame` ms reflect changes.
4. F2 again → overlay hidden.

Or via console:
```
fps show
```

## Known Limitations

- **Managed heap only.** Native + GPU memory not reported. Use Unity Profiler / Memory Profiler for the full picture.
- **`OnGUI` allocates.** Each frame the overlay draws creates short-lived strings via `string.Format` — not zero-alloc. Acceptable for dev builds; the overlay stripping in production keeps this off the hot path.
- **No graph / sparkline.** Plain text. A frame-time graph would need Unity's `Texture2D.SetPixels` per frame which is expensive — out of scope for a dev overlay.
- **Static singleton.** `_instance` field is set in `Awake` and not cleared in `OnDestroy`. Domain reload during PlayMode → Editor transition can leave a stale ref; not a problem in actual gameplay because the GameObject is `DontDestroyOnLoad`.
- **F2 is hardcoded.** No remap config; edit the source if you need a different key.

## Design Rationale

- **IMGUI** so the overlay has zero scene / prefab dependency — drop the asmdef in, it works. uGUI would require a Canvas in the layer chain, which conflicts with the consumer-owned `UIRoot` pattern from Phase 3.
- **60-sample rolling average** balances stability vs responsiveness. Single-frame `1/Time.deltaTime` jitters too much to read; 5-second window hides hitches.
- **Bundled with `CheatConsole`** in `DevToolsBootstrap` because both are dev-only and toggled the same way (asmdef-gated, runtime-spawned). Splitting them would mean two `RuntimeInitializeOnLoadMethod` hooks and twice the GameObject churn.
- **No data export.** The overlay is for "is the frame budget OK right now" — for serial measurement, use the Unity Profiler or `ProfilerRecorder` API. The overlay deliberately does not pretend to be a benchmark tool.
