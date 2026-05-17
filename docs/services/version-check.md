# Version Check Service

## Overview

`IVersionCheckService` compares the running app's `Application.version` against three remote-config keys (`min_version`, `recommended_version`, `maintenance_mode`) and produces a `VersionCheckResult` with status `Maintenance` / `ForceUpdate` / `SoftUpdate` / `Ok`. The implementation is `VersionCheckService` (in `Zero.Services.VersionCheck`). Status precedence: `Maintenance` wins regardless of versions; `ForceUpdate` fires if local < min; `SoftUpdate` fires if local < recommended; otherwise `Ok`. Malformed or missing remote keys degrade gracefully to `Ok` with a warn — the bootstrap pipeline never blocks on flaky remote config.

`VersionCheckStep` is non-critical and runs after `RemoteConfigStep`. **The step does not show UI** — the consumer reads `IVersionCheckService.LastResult` in their first scene and decides how to route (maintenance popup, force-update screen, soft-update toast, or proceed).

## Public API

```csharp
namespace Zero.Core
{
    public enum VersionStatus
    {
        Ok,
        SoftUpdate,
        ForceUpdate,
        Maintenance,
    }

    public readonly struct VersionCheckResult
    {
        public VersionStatus Status { get; }
        public string LocalVersion { get; }
        public string RemoteMinVersion { get; }

        public VersionCheckResult(VersionStatus status, string localVersion, string remoteMinVersion);
    }

    public interface IVersionCheckService
    {
        UniTask<VersionCheckResult> CheckAsync(CancellationToken ct = default);
        VersionCheckResult LastResult { get; }
    }
}
```

Remote-config keys consumed:

| Key | Type | Meaning |
|---|---|---|
| `min_version` | string | Lowest semver the client may run. Below this → `ForceUpdate`. |
| `recommended_version` | string (optional) | Encouraged semver. Below this but ≥ min → `SoftUpdate`. |
| `maintenance_mode` | bool | If true, return `Maintenance` regardless of versions. |

Semver parser is 3-part `major.minor.patch`. Pre-release / build metadata (`1.0.0-beta`, `1.0.0+abc`) are not recognized — that's a deliberate scope choice, see [Known Limitations](#known-limitations).

## Extension Points

**Swap the implementation** by editing `Assets/_Project/Scripts/Runtime/Services/VersionCheck/VersionCheckServiceInstaller.cs`:

```csharp
builder.RegisterFactory<IVersionCheckService>(
    c => new MyCustomVersionCheckService(c.Resolve<IRemoteConfigService>(), c.Resolve<ILogService>()),
    new[] { typeof(IVersionCheckService) },
    Lifetime.Singleton,
    Reflex.Enums.Resolution.Lazy);
```

**Override the local version source.** The default ctor pulls `Application.version` via the installer factory. Tests inject a known semver because the default template `ProductVersion` is `0.1` (2-part, fails parse). To override at runtime (e.g. read from a build manifest), edit the factory body:

```csharp
c => new VersionCheckService(c.Resolve<IRemoteConfigService>(), c.Resolve<ILogService>(), MyBuildManifest.SemanticVersion);
```

**Add new status fields** (e.g. `ReleaseNotesUrl`, `ChangelogText`): `VersionCheckResult` is a `readonly struct` (cannot be derived from) — replace it with a new struct that includes the extra fields and update the `IVersionCheckService.CheckAsync` return type accordingly. Keep `IVersionCheckService` minimal; advertise extra metadata via `IRemoteConfigService.TryGetString` reads in your routing code instead of fattening the interface.

## Examples

Resolve `LastResult` in the first scene and route:

```csharp
public sealed class LiveOpsGate : MonoBehaviour
{
    [Inject] private IVersionCheckService _versionCheck;
    [Inject] private IUIService _ui;

    private async void Start()
    {
        var status = _versionCheck.LastResult.Status;
        switch (status)
        {
            case VersionStatus.Maintenance:
                await _ui.PushAsync<MaintenancePopup, Unit, Unit>(Unit.Default);
                Application.Quit();
                break;
            case VersionStatus.ForceUpdate:
                await _ui.PushAsync<ForceUpdatePopup, Unit, Unit>(Unit.Default);
                Application.OpenURL(StoreUrlForCurrentPlatform());
                Application.Quit();
                break;
            case VersionStatus.SoftUpdate:
                _ui.ShowToast("A new version is available!");
                LoadHomeScene();
                break;
            case VersionStatus.Ok:
                LoadHomeScene();
                break;
        }
    }
}
```

Re-check during runtime (e.g. after returning from background) by calling `CheckAsync` again. The result is cached on `LastResult` until the next call.

## Known Limitations

- **3-part semver only.** Pre-release suffixes and build metadata are ignored. If `min_version` is set to `1.0.0-beta` it parses as invalid and the service returns `Ok` with a warn. Use canonical 3-part values.
- **Single locale of remote config.** No A/B variants on `min_version` — every player on this build reads the same gate. Use `IRemoteConfigService.GetVariant<string>` directly if you need rollout segmentation.
- **No automatic store deeplink.** The service produces a status; the consumer is responsible for showing the popup and opening the appropriate store URL.
- **Bootstrap step does not retry independently.** If `IRemoteConfigService` fetch failed (which is what populates the keys this service reads), the step still runs but every `TryGetString` returns false and the service returns `Ok` with warns. The remote-config step's own retry policy is what matters here.

## Design Rationale

- **Factory binding via `RegisterFactory`** because the ctor takes `string localVersion` — Reflex can't auto-resolve `string`. Tests inject `"1.0.0"` so they don't depend on `Application.version` (template default `0.1` would fail the 3-part parse and the test matrix would degenerate to all-`Ok`).
- **Step does not show UI** — bootstrap is UI-independent. The step only fetches the result; the consumer's first scene reads `LastResult` and decides routing. This keeps the pipeline composable and lets consumers integrate live-ops gates into their existing scene flow without forking the bootstrap pipeline.
- **Malformed/missing → `Ok` + warn** rather than `ForceUpdate` or throwing. A flaky remote-config fetch should not lock players out of the game. The reverse failure mode (running an outdated build because remote config is down) is acceptable for hybrid casual; if your economy is server-validated the gate happens server-side anyway.
- **No "soft block" enforcement** — the service surfaces `SoftUpdate` as a status, but does not delay or partially disable the game. Consumer chooses the friction level (toast, modal once-per-day, modal every launch).
