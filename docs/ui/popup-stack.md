# Popup Stack

## Overview

The popup stack manages modal dialogs that appear on top of the game UI. Each popup is modally presented, blocking interaction with layers below until dismissed. Popups support typed generic data binding and result returns, automatic layering and sort order management, and LitMotion-powered transitions.

> **Prerequisite:** the active scene must have a `UIRoot` MonoBehaviour with its layer Transforms assigned. Without one, `PushAsync` throws `InvalidOperationException("UIService has no UIRoot attached.")`. See [`ui-root.md`](ui-root.md) for the one-time scene setup recipe.

## Public API

```csharp
// Push a new popup (blocks until closed)
UniTask<TResult> UIService.PushAsync<TPopup, TData, TResult>(
    TData data,
    PopupTransition transition = PopupTransition.Fade,
    float duration = 0.2f,
    CancellationToken ct = default);

// Pop the top popup
UniTask UIService.PopAsync(CancellationToken ct = default);

// Replace the top popup without intermediate dismiss
UniTask UIService.ReplaceAsync<TPopup, TData, TResult>(
    TData data,
    PopupTransition transition = PopupTransition.Fade,
    float duration = 0.2f,
    CancellationToken ct = default);

// Get the root transform for a layer
Transform UIService.GetLayerRoot(Core.UiLayer layer);

// Popup base class
public abstract class PopupBase<TData, TResult> : MonoBehaviour, IPopup<TData, TResult>
{
    protected virtual UniTask OnOpenAsync(TData data, CancellationToken ct);
    protected virtual UniTask OnCloseAsync(TResult result, CancellationToken ct);
    protected void ClosePopup(TResult result);
}

// Popup transition types
public enum PopupTransition { None, Fade, Slide, Scale }
```

## Extension Points

**Custom popup implementation:**
1. Create a class extending `PopupBase<TData, TResult>` where `TData` is your input data and `TResult` is your result type.
2. Override `OnOpenAsync(TData, CancellationToken)` to initialize UI based on data (optional).
3. Call `ClosePopup(result)` to close the popup and return the result.
4. Place the script on a prefab and register it via Addressables key `ui/popup/<ClassName.ToLowerInvariant()>`.

**Custom transitions:**
Override `ApplyTransitionIn` and `ApplyTransitionOut` in your `PopupBase` subclass to customize animation behavior.

**Backdrop event handling:**
Subscribe to `IEventBus.On<PopupBackdropTapped>()` to listen for backdrop taps and decide whether to close the popup.

## Examples

```csharp
// Define a popup with string input and boolean result
public sealed class ConfirmPopup : PopupBase<string, bool>
{
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _cancelButton;

    protected override void Awake()
    {
        base.Awake();
        _confirmButton.onClick.AddListener(() => ClosePopup(true));
        _cancelButton.onClick.AddListener(() => ClosePopup(false));
    }

    public override UniTask OnOpenAsync(string title, CancellationToken ct)
    {
        _titleText.text = title;
        return base.OnOpenAsync(title, ct);
    }
}

// Usage: push the popup and await the result
var uiService = /* resolve from container */;
bool confirmed = await uiService.PushAsync<ConfirmPopup, string, bool>(
    "Are you sure?",
    PopupTransition.Fade,
    0.3f,
    cancellationToken);
```

## Known Limitations

- Popups are rendered in the `Popup` layer (sort order 200) by default. Custom layers require manual canvas assignment.
- Modal mask backdrop **is** auto-rendered (semi-transparent black raycast-blocker behind each popup). Tap-outside publishes `PopupBackdropTapped` via `IEventBus`; the consumer decides whether to close the popup.
- Stack does not support queuing or priority-based insertion; all pushes append to the stack top.
- Nested popups are supported but consume memory for each instance in the stack.
- Layer canvases are **not** spawned by the framework. The active scene must have a `UIRoot` component (see [`ui-root.md`](ui-root.md)).

## Design Rationale

The popup stack is backed by a `Dictionary<UiLayer, PopupStack>` of internal stacks, one per layer. Each layer (Hud, Popup, Overlay, System) has a dedicated Canvas with `sortingOrder` tied to the layer enum. Within a layer, popups receive monotonically increasing sort orders so the top of the stack always renders on top and receives input events first.

The generic `PushAsync<TPopup, TData, TResult>` signature enforces type safety at call sites — the compiler prevents mismatched data/result pairs. This avoids the runtime ambiguity of untyped popup systems where data shape and result type are discovered at runtime.

Transitions are powered by LitMotion, which integrates deeply with UniTask cancellation, so interrupting a popup during its transition (e.g., from app pause) cleanly aborts the tween without leaving the UI in a half-animated state.

PopupBackdropTapped events flow through `IEventBus`, decoupling the decision to close from the popup layer itself — the consumer can implement retention logic ("are you sure you want to exit?") without modifying the popup stack.
