# Version Check Flow

## Overview

The bootstrap pipeline runs `VersionCheckStep` after `RemoteConfigStep` and stores the result on `IVersionCheckService.LastResult`. The step does not show UI — that decision belongs to the consumer's first scene. This doc describes the consumer-side gate that reads `LastResult` and routes to the right experience: maintenance lockout, force-update, soft-update toast, or proceed.

For service mechanics (semver compare, status precedence, malformed handling) see [docs/services/version-check.md](../services/version-check.md).

## Recommended scene flow

```
Bootstrap.unity
    └─> bootstrap pipeline runs (incl. VersionCheckStep)
    └─> bootstrap finishes
    └─> Loading.unity (your scene; LoadingScreenView reads progress)
        └─> LiveOpsGate MonoBehaviour reads IVersionCheckService.LastResult
            ├─ Maintenance     → push MaintenancePopup, then Application.Quit
            ├─ ForceUpdate     → push ForceUpdatePopup, then OpenURL(store), Application.Quit
            ├─ SoftUpdate      → ShowToast("New version available"), then load Home.unity
            └─ Ok              → load Home.unity
```

## LiveOpsGate example

```csharp
using Cysharp.Threading.Tasks;
using R3;
using Reflex.Attributes;
using UnityEngine;
using Zero.Core;

public sealed class LiveOpsGate : MonoBehaviour
{
    [Inject] private IVersionCheckService _versionCheck;
    [Inject] private IUIService _ui;
    [Inject] private ISceneService _scene;

    private async void Start()
    {
        var result = _versionCheck.LastResult;
        switch (result.Status)
        {
            case VersionStatus.Maintenance:
                await _ui.PushAsync<MaintenancePopup, MaintenanceData, Unit>(
                    new MaintenanceData(result.RemoteMinVersion));
                Application.Quit();
                return;

            case VersionStatus.ForceUpdate:
                await _ui.PushAsync<ForceUpdatePopup, Unit, Unit>(default);
                Application.OpenURL(GetStoreUrl());
                Application.Quit();
                return;

            case VersionStatus.SoftUpdate:
                _ui.ShowToast("A new version is available — update at your convenience.");
                break;

            case VersionStatus.Ok:
                break;
        }

        await _scene.LoadAsync("Home.unity");
    }

    private static string GetStoreUrl()
    {
#if UNITY_IOS
        return "itms-apps://itunes.apple.com/app/idYOUR_ID";
#elif UNITY_ANDROID
        return "https://play.google.com/store/apps/details?id=YOUR_PACKAGE";
#else
        return "https://yourgame.example/download";
#endif
    }
}
```

`UIRoot` must be attached to the Loading scene before `Start` runs — otherwise `IUIService.PushAsync` throws. See [docs/ui/ui-root.md](../ui/ui-root.md).

## Soft-update friction levels

`SoftUpdate` is "encouraged but not required". Pick one based on your retention strategy:

- **Toast every launch.** Cheapest, low friction. Use `_ui.ShowToast(...)`.
- **Modal once per day.** Persist `liveops.soft_update_last_shown_unix` via `ISaveService`; show modal only if `> 24h`. The user can dismiss and play.
- **Modal every launch with "remind me later" cooldown.** As above but no `<24h` window — modal returns even if dismissed today.
- **Bottom-sheet banner** (consumer adds a custom UI). Less invasive than a modal, more visible than a toast.

## Re-checking during runtime

`IVersionCheckService.CheckAsync` re-runs the gate. Common triggers:

- **App foreground.** Bus event `AppPaused(false)` (resumed) — re-check; remote config may have flipped `maintenance_mode` while the user was away.
- **After purchase / deeplink.** Premium / live-event flows that depend on a recent server state.
- **Manual retry.** From the maintenance popup's "Try again" button.

```csharp
[Inject] private IVersionCheckService _versionCheck;
[Inject] private IEventBus _bus;

private void Awake()
{
    _bus.On<AppPaused>().Where(e => !e.IsPaused).Subscribe(async _ =>
    {
        var result = await _versionCheck.CheckAsync();
        if (result.Status == VersionStatus.Maintenance)
            ShowMaintenanceLockout();
    });
}
```

## Remote-config setup

The service reads three keys: `min_version`, `recommended_version`, `maintenance_mode`. Set them in your remote-config dashboard (Firebase Remote Config / Unity Remote Config / your custom backend) under those exact names.

Example values for a 1.2.0 release:
- `min_version` = `"1.0.0"` — anything below is force-updated.
- `recommended_version` = `"1.2.0"` — soft-update message for 1.0.x and 1.1.x.
- `maintenance_mode` = `false` — flip to `true` to hard-block all clients (planned downtime, emergency rollback).

## Known Limitations

- **No automatic store deeplink.** Build platform-specific store URLs in your code.
- **No A/B variant on min_version.** The service reads a single value via `TryGetString`. Use `IRemoteConfigService.GetVariant<string>("min_version", ...)` directly if you need staged rollout.
- **Bootstrap proceeds even on Maintenance.** The pipeline does not auto-block — `LiveOpsGate` in the first scene is what enforces. This keeps the bootstrap pipeline UI-independent and reusable.
