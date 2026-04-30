# Loading Screen

## Overview

LoadingScreenView is a component-only contract for displaying bootstrap progress. Attach it to a GameObject with Slider and TextMeshProUGUI components, and it automatically drives both from `IBootstrapProgressReporter`. No prefab ships; consumers author the loading UI in their own scene.

## Public API

```csharp
public sealed class LoadingScreenView : MonoBehaviour
{
    [SerializeField] private Slider _progressSlider;
    [SerializeField] private TextMeshProUGUI _stepNameText;

    // Automatically injected and read-only
    // Subscribes to progress and step name changes in Start()
    // Unsubscribes in OnDestroy()
}
```

## Extension Points

LoadingScreenView does not expose extension points — it is a leaf component. To customize loading UI, create your own MonoBehaviour that injects `IBootstrapProgressReporter` and drives your custom UI.

## Examples

In your `Loading.unity` scene:
1. Set up `UIRoot` per [`ui-root.md`](ui-root.md) (one-time per scene).
2. Under the `[UI.Hud]` Canvas, add a Slider (for progress bar) and a TextMeshProUGUI (for step name).
3. On the same GameObject — or a child of `[UI.Hud]` — attach the `LoadingScreenView` component.
4. Drag the Slider into the `_progressSlider` field.
5. Drag the TextMeshProUGUI into the `_stepNameText` field.
6. Play the scene; the loading screen subscribes to `IBootstrapProgressReporter` via Reflex injection. (Note: progress only advances during bootstrap; if your Loading scene loads *after* bootstrap completes, progress will already be at 1.0.)

```csharp
// Custom loading screen with a fill image instead of slider
public sealed class CustomLoadingView : MonoBehaviour
{
    [SerializeField] private Image _fillImage;
    [Inject] private IBootstrapProgressReporter _reporter;

    private void Start()
    {
        _fillImage.fillAmount = 0f;
        _reporter.Progress.Subscribe(p =>
        {
            if (_fillImage != null) _fillImage.fillAmount = p;
        }).AddTo(this);
    }
}
```

## Known Limitations

- LoadingScreenView reads from `IBootstrapProgressReporter`, which is populated only during the bootstrap phase. Once `GameLauncher.Start()` completes, the pipeline is done and progress no longer advances.
- Progress slider and step name text are optional (null-checked); if either field is not assigned, it is skipped.
- LoadingScreenView never resolves `BootstrapPipeline` directly; all reads flow through `IBootstrapProgressReporter` to avoid Lazy-singleton resolution race conditions.

## Design Rationale

LoadingScreenView is a component contract, not a prefab, to keep the template generic. Every game's loading UI looks different (custom art, animations, branding) — shipping a generic prefab either becomes unusable (too minimal) or opinionated (too specific). By defining the interface and letting consumers implement the UI, the template stays flexible while still providing the data-binding scaffold.

`IBootstrapProgressReporter` is a read-only interface; views never call methods on it, only subscribe to observables. This read-only pattern prevents views from accidentally interfering with bootstrap state.

The component uses R3's `Subscribe(Action<T>)` extension for lambda-friendly subscriptions. Disposal is automatic when the GameObject is destroyed (R3 integrates with Unity's lifecycle).
