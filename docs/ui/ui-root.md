# UIRoot — Scene Setup

## Overview

`UIRoot` is the **single MonoBehaviour you must place in any scene that uses popups, screens, or toasts**. The framework no longer spawns UI canvases at bootstrap; instead the consumer authors UI hierarchies in their own scenes and points `UIRoot` at four layer Transforms in the inspector. On enable, `UIRoot` registers those Transforms with `IUIService`; on disable, it detaches them. This keeps `Bootstrap.unity` minimal (splash background + studio logo only) and lets each scene own its own UI structure.

## Public API

```csharp
public sealed class UIRoot : MonoBehaviour
{
    [SerializeField] private Transform _hudLayer;
    [SerializeField] private Transform _popupLayer;
    [SerializeField] private Transform _overlayLayer;
    [SerializeField] private Transform _systemLayer;

    [Inject] private IUIService _uiService;
    // OnEnable  → _uiService.AttachRoot({Hud, Popup, Overlay, System})
    // OnDisable → _uiService.DetachRoot()
}

// IUIService surface for the layer registration:
void AttachRoot(IReadOnlyDictionary<UiLayer, Transform> layers);
void DetachRoot();
```

`UiLayer` enum values double as the recommended `Canvas.sortingOrder` for each layer — Hud=100, Popup=200, Overlay=300, System=400.

## Step-by-step scene setup

This is the **happy-path recipe** for `Loading.unity` / `Home.unity` / `Play.unity`. Do this once per scene that needs UI.

1. **Create the scene** (e.g., `Assets/_Project/Scenes/Home.unity`). Add it to Build Settings if you load it via classic SceneManager; if you use the Addressables-backed `ISceneService`, mark the scene addressable.
2. **Create a top-level `[UI]` GameObject** under the scene root.
3. **Under `[UI]`, create four child Canvas GameObjects** — one per layer. For each Canvas:
   - `Render Mode`: Screen Space – Overlay (or Camera, your call).
   - `Sorting Order`: 100 (Hud), 200 (Popup), 300 (Overlay), 400 (System). Higher = on top.
   - Add a `CanvasScaler`. Recommended: `Scale With Screen Size`, reference resolution `1080×1920`, match `0.5`.
   - Add a `GraphicRaycaster` (Unity adds it by default with the Canvas).
4. **Name the four canvases clearly**: `[UI.Hud]`, `[UI.Popup]`, `[UI.Overlay]`, `[UI.System]`.
5. **Add a `UIRoot` component** to the `[UI]` GameObject (or any other GameObject in the scene).
6. **Drag the four canvas Transforms** into the matching inspector slots: Hud Layer, Popup Layer, Overlay Layer, System Layer.
7. (Optional) Add a `SafeAreaFitter` to a child of `[UI.Hud]` if you want notch-aware HUD positioning. See [`safe-area.md`](safe-area.md).
8. (Optional) Add a `LoadingScreenView` to `[UI.Hud]` (in `Loading.unity`) to render bootstrap progress. See [`loading-screen.md`](loading-screen.md).
9. **Enter Play Mode**. Console should log `[UI] Root attached.` once `UIRoot.OnEnable` fires.

That is the entire setup. From this point, any `IUIService.PushAsync<...>(...)`, `ShowScreenAsync<...>(...)`, or `ShowToast(...)` call will instantiate prefabs under the appropriate layer Transform.

### Minimal layout (one Canvas, four sub-RectTransforms)

If you do not need separate sorting between layers, you can use a single Canvas with four child RectTransforms instead of four separate Canvases. UIService treats each layer as a parent Transform — it does not require each to be its own Canvas. Sort orders within a single Canvas are determined by sibling order; the Hud→Popup→Overlay→System parent order in the hierarchy gives you the same effect.

This is simpler but you lose per-layer override sorting. For most hybrid casual games, the single-Canvas layout is enough.

### Multiple scenes, one root at a time

`UIService` holds **one attached root**. When you transition from `Home.unity` to `Play.unity`:
- `Home.unity`'s `UIRoot.OnDisable` fires → `DetachRoot()` clears the layer dictionary.
- `Play.unity`'s `UIRoot.OnEnable` fires → `AttachRoot()` registers the new Transforms.

If you load scenes additively, only one `UIRoot` should be active at a time — disable or destroy the previous scene's `UIRoot` before enabling the next.

## Reflex injection requirement

`UIRoot` uses `[Inject] private IUIService _uiService;`. For the inject to fire, the GameObject must live in a scene that the Reflex container injects into. The template's bootstrap registers a root container via `ProjectScopeInstaller`; Unity auto-injects MonoBehaviours in any scene that loads after the root container is built (same pattern as `LoadingScreenView`, `LocalizedText`, `GameLauncher`).

If `_uiService` is null in `OnEnable`, you'll see a red console error: `[UIRoot] IUIService not injected. Reflex scope must be configured for the active scene.` This typically means the scene was instantiated outside Unity's normal load flow — fix by loading the scene via `SceneManager.LoadScene(...)` or `ISceneService.LoadAsync(...)`.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `InvalidOperationException: UIService has no UIRoot attached` on `PushAsync` | No `UIRoot` in the active scene, or `UIRoot.OnEnable` hasn't fired yet | Add `UIRoot` to the scene; ensure the GameObject is enabled before any UI call |
| `[UIRoot] No layer Transforms assigned` warning | All four inspector slots are empty | Drag at least one layer Transform into the slot |
| `ShowToast` silently drops messages | No root attached at time of call | Toasts intentionally don't throw — verify a `UIRoot` is active |
| Popup sort order looks wrong | Two scenes have `UIRoot`s active at once | Disable the previous scene's `UIRoot` before loading the next |
| `_uiService` null in `OnEnable` | Scene loaded outside Reflex scope | Load via `SceneManager` or `ISceneService` so Reflex injects |

## Design Rationale

The framework deliberately does not spawn canvases. Every hybrid casual / puzzle game has an opinionated UI structure (custom Canvas hierarchies, special scaling, art-driven layout). A runtime-spawned `[Zero.UI]` root forces the consumer to either work around it or rip it out — both annoying. Letting the consumer author the hierarchy themselves, then point `UIRoot` at it, gives full layout control with **zero framework code** standing between scene authoring and UI rendering.

The `OnEnable` / `OnDisable` lifecycle binds layer registration to scene activation. Unity already manages scene-active and GameObject-active lifecycles; piggybacking on those means the framework needs no explicit scene-transition hook. Multi-scene workflows (additive loads, scene swap) work without code changes.

`AttachRoot` accepting an `IReadOnlyDictionary<UiLayer, Transform>` (instead of a fixed four-arg method) allows partial registration — a minimal scene that only needs Hud + Popup can leave the other two slots null without service errors. UIService just does not parent into unregistered layers.
