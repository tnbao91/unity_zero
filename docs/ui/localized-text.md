# Localized Text

## Overview

LocalizedText is a component that automatically fetches and displays text from the localization service based on a key. It updates whenever the locale changes, keeping all text in sync across the app without manual callbacks. Attach to any GameObject with a TextMeshProUGUI component.

## Public API

```csharp
public sealed class LocalizedText : MonoBehaviour
{
    [SerializeField] private string _key;

    // Automatically injected and read-only
    // Subscribes to IL10nService.OnLocaleChanged in OnEnable()
    // Unsubscribes in OnDisable()
}
```

## Extension Points

LocalizedText is a leaf component with no extension points. To customize text fetching or formatting, create your own MonoBehaviour that injects `IL10nService` and implements custom logic.

## Examples

In your UI scene:
1. Create a TextMeshProUGUI component (e.g., a label or button text).
2. Attach LocalizedText to the same GameObject.
3. Set the `_key` field to your localization key (e.g., `ui.button.start`).
4. Play; the text automatically loads and updates on locale change.

In code, trigger a locale change:
```csharp
var l10n = /* resolve from DI container */;
await l10n.SetLocaleAsync(new LocaleIdentifier("en"));
// All LocalizedText components automatically update
```

Custom text component with formatting:
```csharp
public sealed class LocalizedTextWithFormat : MonoBehaviour
{
    [SerializeField] private string _keyTemplate; // "ui.level.label" → "Level {0}"
    [SerializeField] private int _levelNumber;
    [Inject] private IL10nService _l10n;

    private void OnEnable()
    {
        RefreshText();
        _l10n.OnLocaleChanged.Subscribe(_ => RefreshText()).AddTo(this);
    }

    private void RefreshText()
    {
        string template = _l10n.Get(_keyTemplate);
        GetComponent<TextMeshProUGUI>().text = string.Format(template, _levelNumber);
    }
}
```

## Known Limitations

- LocalizedText requires `IL10nService` injection via Reflex; it cannot be used in Editor-only scripts or scenes that are not part of the bootstrap hierarchy.
- If a localization key is missing, the component falls back to displaying the key itself (e.g., `ui.button.start` if the key is not found). No exception is thrown.
- Locale changes are observable via `IL10nService.OnLocaleChanged`, which is an `Observable<Locale>` (R3 type). LocalizedText automatically subscribes, but custom subscribers must manage their own disposables.
- TextMeshProUGUI must be present on the same GameObject or the component logs an error and disables itself.

## Design Rationale

LocalizedText decouples localization from UI code. Rather than having every text component manually call `il10n.Get(key)` in `Start()`, LocalizedText makes localization reactive — text updates automatically when the app locale changes, without explicit re-binding.

The component is `sealed` and requires a TextMeshProUGUI component on the same GameObject (enforced by `[RequireComponent]`) to keep the contract simple and prevent the user from attaching it to the wrong type of UI element.

The fallback-to-key behavior on missing localization keys prevents red errors during development when string tables are incomplete. In production, missing keys are a content error that should be caught in QA; the fallback is defensive enough to prevent a crash while debugging.
