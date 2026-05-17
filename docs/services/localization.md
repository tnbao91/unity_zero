# Localization Service

## Overview

`IL10nService` wraps the official `com.unity.localization` package (v1.5.11+), providing a simple abstraction for string retrieval, locale switching, and locale-change events. The service avoids building custom string tables — instead, it delegates to Unity's LocalizationSettings + StringDatabase. This keeps the template lightweight and lets consumers leverage Unity's editor UI for managing translations.

## Public API

```csharp
public interface IL10nService
{
    // The locale is identified by its raw string code (e.g. "en", "vi", "ja-JP")
    // — Zero.Core deliberately does NOT depend on UnityEngine.Localization.
    string Get(string key, params object[] args);
    Observable<string> OnLocaleChanged { get; }
    UniTask SetLocaleAsync(string locale, CancellationToken ct = default);
    string CurrentLocale { get; }
}

// Real implementation in Zero.Services.Localization (wraps com.unity.localization).
public sealed class UnityLocalizationService : IL10nService, IDisposable { ... }

// Mock for EditMode tests (dictionary-backed).
public sealed class MockLocalizationService : IL10nService { ... }
```

## Extension Points

**Custom locale provider:** if you need to load the locale list from a remote config or database (instead of Unity's built-in list), `UnityLocalizationService` is `sealed` — swap the binding in `LocalizationServiceInstaller.Install(...)` for your own `IL10nService` implementation (or a decorator that wraps the default and overrides locale resolution).

**Key namespacing:** keys follow a `scope.category.string` pattern to avoid collisions:
- `ui.popup.win.title`
- `ui.popup.settings.volume_label`
- `gameplay.tutorial.step_1_text`
- `meta.shop.currency_name`

This convention is enforced by documentation only (no runtime validation), but makes large localization tables navigable.

## Examples

**Simple string retrieval:**
```csharp
[Inject] private IL10nService _loc;

void UpdateUI()
{
    _levelLabel.text = _loc.Get("gameplay.ui.level_label", currentLevel);
    // Retrieves "Level {0}" from StringTable, substitutes {0} → currentLevel
}
```

**Locale switching with listener:**
```csharp
public sealed class LocaleSelector : MonoBehaviour
{
    [Inject] private IL10nService _loc;
    private IDisposable _subscription;

    private void OnEnable()
    {
        _subscription = _loc.OnLocaleChanged.Subscribe(newCode =>
        {
            Debug.Log($"Switched to {newCode}");
            // Re-render all UI text bound to keys.
        });
    }

    private void OnDisable() => _subscription?.Dispose();

    public async UniTask ChangeLanguageAsync(string localeCode, CancellationToken ct)
    {
        // Pass the raw string code; the implementation translates to LocaleIdentifier.
        await _loc.SetLocaleAsync(localeCode, ct);
    }
}
```

**Editor setup:** Open `Window → Asset Management → Localization Tables` to add languages and strings. The built-in editor window handles table creation, key management, and multi-language CSV export/import.

## Known Limitations

- **Requires LocalizationSettings asset:** on a fresh project with no Localization package configured, `LocalizationStep` logs a warning and skips init (doesn't throw). You must create the LocalizationSettings asset in the editor before localization calls work. See Unity Localization Package documentation.
- **No smart string replacement:** the `Get(key, args)` method uses naive `string.Format`-style placeholders. Complex grammatical rules (plurals, gender agreement) require hand-rolled logic per language, stored as multiple keys.
- **Locale list from package:** available locales are the ones you've configured in the Localization Tables editor. Runtime locale enumeration is possible but not exposed by this interface (use `LocalizationSettings.AvailableLocales` directly if needed).

## Design Rationale

**Why wrap `com.unity.localization` instead of rolling custom?** The official package is battle-tested, has a polished editor UI, and integrates with Addressables for remote string loading. Building a custom string table system duplicates this work and makes contributors maintain yet another wheel.

**Key namespace convention:** hybrid casual games scale from 1 language (prototype) to 20+ languages (live ops). A flat key list (`win_title`, `settings_volume`) quickly becomes unmaintainable. Namespacing by scope (`ui.popup.win.title`) mirrors the UI hierarchy and lets you grep for "all gameplay strings" or "all shop strings" easily.

**MockLocalizationService for tests:** EditMode tests can't load LocalizationSettings, so the mock dictionary-backed implementation lets you test code that depends on `IL10nService` without Unity assets present.

**Step short-circuit:** the bootstrap `LocalizationStep` guards with `LocalizationSettings.HasSettings` before calling init. On a fresh template clone (no Localization assets), the step logs a warning and returns successfully, so the game still launches. This unblocks iteration; you set up localization tables in the editor when ready.
