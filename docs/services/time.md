# Time Service

## Overview

`ITimeService` exposes a single source of truth for wall-clock time the rest of the game can mock or substitute. The template ships `StubTimeService` — a thin wrapper around `DateTime.UtcNow` that always reports `IsServerSynced = false`. Live games should swap in a real impl that syncs against an authoritative backend or NTP, then reports synced time once `SyncAsync` succeeds.

This service exists so daily-login systems, cooldowns, and reward gates use *one* time source — never `DateTime.UtcNow` scattered across gameplay code, which makes testing impossible and lets device-clock manipulation grant infinite rewards.

## Public API

```csharp
namespace Zero.Core
{
    public interface ITimeService
    {
        DateTime UtcNow { get; }
        long UnixTimeSeconds { get; }
        bool IsServerSynced { get; }
        UniTask SyncAsync(CancellationToken ct = default);
    }
}
```

`StubTimeService` implementation (`Assets/_Project/Scripts/Runtime/Services/Time/StubTimeService.cs`):

| Member | Stub behavior |
|---|---|
| `UtcNow` | `DateTime.UtcNow` directly |
| `UnixTimeSeconds` | `DateTimeOffset.UtcNow.ToUnixTimeSeconds()` |
| `IsServerSynced` | `false` always |
| `SyncAsync` | `UniTask.CompletedTask` (no-op) |

## Extension Points

Swap the binding in `Assets/_Project/Scripts/Runtime/Services/Time/TimeServiceInstaller.cs`:

```csharp
builder.RegisterType(
    typeof(ServerTimeService),
    new[] { typeof(ITimeService) },
    Lifetime.Singleton,
    Resolution.Lazy);
```

A real impl typically:
1. Calls a backend endpoint (`GET /now`) on `SyncAsync` and stores the offset between server time and `DateTime.UtcNow`.
2. Returns `_serverNowAtSync + (DateTime.UtcNow - _localNowAtSync)` from `UtcNow`.
3. Sets `IsServerSynced = true` after the first successful sync.
4. Re-syncs periodically (e.g. every 30 minutes, or on app foreground via `AppPaused` event from the bus).

If an NTP-only impl is acceptable (no server), use `Cysharp.Threading.Tasks.UniTask` to wrap a UDP NTP query — but be aware NTP UDP is blocked by some carrier networks; fallback to local time with a warn.

## Examples

Daily login window check using the abstraction:

```csharp
public sealed class DailyLoginChecker
{
    private readonly ITimeService _time;
    private readonly ISaveService _save;

    public bool IsNewDay()
    {
        if (!_save.TryGet<long>("daily.last_login_unix", out var last))
            return true;

        var now = _time.UnixTimeSeconds;
        var lastDate = DateTimeOffset.FromUnixTimeSeconds(last).Date;
        var nowDate = DateTimeOffset.FromUnixTimeSeconds(now).Date;
        return nowDate > lastDate;
    }
}
```

Tests inject a stub that returns a controllable time:

```csharp
private sealed class FakeTimeService : ITimeService
{
    public DateTime UtcNow { get; set; } = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public long UnixTimeSeconds => new DateTimeOffset(UtcNow).ToUnixTimeSeconds();
    public bool IsServerSynced => true;
    public UniTask SyncAsync(CancellationToken ct = default) => UniTask.CompletedTask;
}
```

## Known Limitations

- **`StubTimeService` is trustless.** A player can change their device clock and all `ITimeService.UtcNow` reads shift accordingly. For anything economy-relevant, validate server-side.
- **No bootstrap step.** Unlike most services, time has no `TimeStep` — `SyncAsync` is on the consumer to call (typically after login or before any time-gated mechanic). The template intentionally does not block bootstrap on a network call to a server you have not deployed.
- **No "drift" warning.** The stub doesn't watch for clock-set events; consumer is responsible for re-syncing when `Application.focusChanged` indicates background return.
- **`IsServerSynced` is a binary.** No "minutes since last sync" surface — add it in your impl if needed.

## Design Rationale

- **Stub-only by design.** Backend choice is per-game (Firebase / PlayFab / custom) and the template avoids prescribing one. The stub gets you running locally; swap when you have a server.
- **`UtcNow` over `Now`.** Local time is timezone-dependent and rarely what game logic actually wants. UTC everywhere; render as local in the UI layer only.
- **`UnixTimeSeconds` alongside `UtcNow`.** Many time-gated systems compare integers (cooldown remaining, last login epoch) and `long` is cheaper to persist + compare than serializing `DateTime`.
- **No `OnTimeSynced` observable.** Consumers usually don't care about the sync event itself; they care whether `IsServerSynced` is true at the moment they make a decision. Adding the observable is fine if a real impl needs it — keep the interface stable for stub-only games.
