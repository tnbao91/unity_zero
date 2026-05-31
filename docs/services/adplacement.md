# Ad Placement Service

## Overview

`IAdPlacementService` is the **policy layer above ads** — it decides *whether* a placement is allowed to show right now (cooldown elapsed, session cap not hit, the ad network is ready) and delegates the actual show to [`IAdsService`](ads.md). The shipped impl is `DefaultAdPlacementService` (`Packages/com.tnbao91.nobody.zero/Runtime/Services/AdPlacement/DefaultAdPlacementService.cs`) — a **real** gating service that wraps the (mock-by-default) `IAdsService`.

The split keeps ad *frequency rules* (a game-design concern) out of the ad *network adapter* (an SDK concern). You register a placement's rules once, then call `TryShowAsync` from gameplay without re-checking cooldowns at every call site.

## Public API

```csharp
namespace Zero.Core
{
    public interface IAdPlacementService
    {
        bool CanShow(string placementId);
        UniTask<AdShowResult> TryShowAsync(string placementId, CancellationToken ct = default);
        void RegisterPlacement(string placementId, AdType type, TimeSpan cooldown, int sessionCap);
        void NotifyShown(string placementId);
    }
}
```

| Member | Behavior |
|---|---|
| `RegisterPlacement` | Declares a placement: its `AdType`, minimum `cooldown` between shows, and per-session show `sessionCap`. Re-registering resets that placement's counters. |
| `CanShow` | **Fail-safe** predicate — returns `false` for an unregistered placement, one that's capped, still in cooldown, or whose `AdType` isn't ready on `IAdsService`. |
| `TryShowAsync` | Re-checks `CanShow`, then calls `IAdsService.ShowAsync`. On a `Shown`/`Rewarded` result it bumps the placement's last-shown time and session count. Returns the `AdShowResult` (`NotReady`/`Failed` carry a reason). Never throws on an unknown placement — returns a `Failed`/`NotReady` result. |
| `NotifyShown` | Manually records a show (last-shown time + session count) for cases where the ad was shown outside `TryShowAsync`. |

Cooldown uses `Time.realtimeSinceStartup`; counters are in-memory and reset each process launch (hence *session* cap).

## Extension Points

The impl is `sealed`. Swap the binding in `AdPlacementServiceInstaller.cs` (or your consumer partial) to change the policy — e.g. persisted daily caps, remote-config-driven frequency, or A/B-tested cooldowns:

```csharp
builder.RegisterType(
    typeof(RemoteConfigAdPlacementService),
    new[] { typeof(IAdPlacementService) },
    Lifetime.Singleton,
    Resolution.Lazy);
```

A replacement typically pulls caps/cooldowns from [`IRemoteConfigService`](remote-config.md) and may persist counters via [`ISaveService`](save.md) to enforce *daily* caps across sessions (the default is per-session only).

## Examples

Register placements at boot, then guard a reward button:

```csharp
_placements.RegisterPlacement("interstitial_level_end", AdType.Interstitial, TimeSpan.FromSeconds(90), sessionCap: 8);
_placements.RegisterPlacement("rewarded_double_coins", AdType.Rewarded, TimeSpan.FromSeconds(0), sessionCap: 20);

// UI: only enable the button when policy + network allow it
doubleCoinsButton.interactable = _placements.CanShow("rewarded_double_coins");

// On click:
var result = await _placements.TryShowAsync("rewarded_double_coins", ct);
if (result.Result == AdResult.Rewarded)
    _wallet.Add(coins * 2);
```

## Known Limitations

- **Session-scoped, in-memory counters.** Cooldown and cap reset on app relaunch. For *daily* caps or rules that survive a restart, use a replacement that persists via `ISaveService`.
- **`CanShow` can race the show.** `TryShowAsync` re-checks internally, but a `CanShow` you read for UI state can go stale (network readiness changes). Treat it as a hint; trust the `TryShowAsync` result.
- **No reward verification.** Whether a `Rewarded` result is *legitimately* earned (vs spoofed) is the ad SDK's / your backend's job — this service only gates frequency. Pair with server-side validation for economy-relevant rewards.
- **Banners don't really fit the cooldown model.** `RegisterPlacement` accepts `AdType.Banner`, but cooldown/cap semantics are designed for interstitial/rewarded; manage banners through `IAdsService` directly.

## Design Rationale

- **Policy vs adapter separation.** Frequency capping is design, not SDK plumbing. Keeping it here means swapping ad networks (the `IAdsService` adapter) never touches your pacing rules, and tuning pacing never risks the SDK integration.
- **Fail-safe queries, explicit results.** `CanShow` returns `false` rather than throwing on bad input, while `TryShowAsync` surfaces *why* it didn't show via `AdResult` + `ErrorMessage` — so callers branch on a value instead of a thrown exception (`CLAUDE.md` → "Validate inputs at service boundaries").
- **Per-session default, persistence opt-in.** The simplest correct default (in-memory session caps) ships in-template; daily/cross-session persistence is a deliberate consumer extension since it depends on the game's save schema.
