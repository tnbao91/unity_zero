# Toast

## Overview

Toasts are short, self-dismissing messages that appear in the System layer (topmost, sort order 400). They queue automatically — if multiple toasts are requested in quick succession, they display sequentially without overlap. The queue caps at 16 messages; older messages are dropped with a warning if exceeded.

## Public API

```csharp
// Display a toast
void UIService.ShowToast(string text, float duration = 2f);
```

## Extension Points

ToastQueue loads the toast prefab from Addressables key `ui/toast/default`. To provide a custom toast implementation:
1. Create a prefab with a TextMeshProUGUI component for the message text.
2. Optionally add animation or sound effects.
3. Register it via Addressables at key `ui/toast/default`.
4. If the key is missing, ToastQueue logs a warning and silently disables toasts.

The toast prefab must be:
- A Canvas or child of a Canvas (to position properly in the System layer).
- Self-dismissing (destroy itself after the duration, or be destroyed by ToastQueue).

## Examples

Simple toast prefab structure:
```
[Toast] (Canvas, Overlay, RenderMode=ScreenSpaceOverlay)
  ├─ Panel (Image, opaque background)
  └─ Message (TextMeshProUGUI)
```

Script to auto-dismiss:
```csharp
public sealed class ToastAutoClose : MonoBehaviour
{
    [SerializeField] private float _duration = 2f;

    private void Start()
    {
        _ = DismissAsync();
    }

    private async UniTask DismissAsync()
    {
        await UniTask.Delay((int)(_duration * 1000));
        Destroy(gameObject);
    }
}
```

Consumer usage:
```csharp
var uiService = /* resolve from DI container */;
uiService.ShowToast("Level Complete!", 3f);
```

## Known Limitations

- Toasts are rendered in the System layer (sort order 400), which is above all other UI. There is no way to specify a different layer.
- Queue is FIFO with no priority; all toasts wait their turn.
- Queue cap is hard-coded at 16. Exceeding it drops the oldest message (not the newest).
- Toast prefab must be provided by the consumer; the template ships no default toast.
- Toasts do not interact with each other — no merging duplicates, no collision avoidance.

## Design Rationale

Toasts are implemented as a simple FIFO queue backed by a callback-based renderer to keep the implementation headless. The template does not ship a toast prefab because every game's toast look and feel is different (brand colors, fonts, animations). By requiring the consumer to provide the prefab via Addressables, the template stays generic while still providing the queue and lifecycle scaffolding.

The queue auto-enqueues and auto-dequeues on fixed `Show` calls, so the consumer never manages async state — they call `ShowToast("message")` and it just works.

The 16-message cap prevents unbounded memory growth from spam. In practice, toasts appear at most a few per frame (level completion, item collected, error). The cap is a guard against pathological cases.

Toast display is synchronous (`Show`, not `ShowAsync`); the consumer does not wait for the toast to finish. If they need to know when a specific toast completes, they can wrap the prefab with a custom script that publishes an event on dismiss.
