# Meta Layer Recipes

## Why no template impl

The template ships an empty `Zero.Meta.asmdef` placeholder and **does not** ship `WalletService`, `ProgressionService`, `RewardService`, `VariantResolver`, or `DailyLoginService`. Hybrid casual and puzzle have meta loops different enough that a "generic" meta layer is sludge most consumers rewrite. See [`docs/dev/PLAN.md`](../dev/PLAN.md) §2.4 for the long version.

What this doc provides: pseudo-code recipes consumers crib from when building per-game meta on top of the template's primitives — `ISaveService` for state, `IEventBus` for cross-tier events, `IRemoteConfigService` for live-ops parameters, `ITimeService` for cooldowns.

These are **patterns**, not copy-paste files. Names, types, and event payloads change per game.

---

## Wallet

Single-currency wallet backed by `ISaveService`. Emits `CurrencyChanged` so HUD / animations can react without polling.

```csharp
// Domain event POCO — lives in your game asmdef.
public readonly struct CurrencyChanged
{
    public readonly string CurrencyId;
    public readonly long OldBalance;
    public readonly long NewBalance;
    public readonly string Reason; // "level_reward", "iap", "spend_continue", etc.
    public CurrencyChanged(string id, long oldBalance, long newBalance, string reason)
        { CurrencyId = id; OldBalance = oldBalance; NewBalance = newBalance; Reason = reason; }
}

// Service interface in your game asmdef (NOT in Zero.Core).
public interface IWalletService
{
    long Get(string currencyId);
    void Add(string currencyId, long amount, string reason);
    bool TrySpend(string currencyId, long amount, string reason);
}

public sealed class SaveBackedWallet : IWalletService
{
    private readonly ISaveService _save;
    private readonly IEventBus _bus;

    public SaveBackedWallet(ISaveService save, IEventBus bus) { _save = save; _bus = bus; }

    public long Get(string currencyId)
        => _save.TryGet<long>(Key(currencyId), out var v) ? v : 0;

    public void Add(string currencyId, long amount, string reason)
    {
        if (amount <= 0) return;
        var old = Get(currencyId);
        var next = old + amount;
        _save.Set(Key(currencyId), next);
        _save.RequestSave();
        _bus.Publish(new CurrencyChanged(currencyId, old, next, reason));
    }

    public bool TrySpend(string currencyId, long amount, string reason)
    {
        if (amount <= 0) return true;
        var old = Get(currencyId);
        if (old < amount) return false;
        var next = old - amount;
        _save.Set(Key(currencyId), next);
        _save.RequestSave();
        _bus.Publish(new CurrencyChanged(currencyId, old, next, reason));
        return true;
    }

    private static string Key(string currencyId) => $"wallet.{currencyId}";
}
```

Wire in your game's installer alongside the template installers. Subscribe HUD code to `CurrencyChanged` for animated counter updates.

---

## Progression

Linear level progression via `ISaveService` int. Listens to `LevelCompleted` from the bus.

```csharp
public interface IProgressionService
{
    int CurrentLevel { get; }
    int HighestUnlockedLevel { get; }
}

public sealed class LinearProgression : IProgressionService, IDisposable
{
    private readonly ISaveService _save;
    private readonly IEventBus _bus;
    private readonly System.IDisposable _sub;

    public int CurrentLevel
        => _save.TryGet<int>("progression.current_level", out var v) ? v : 1;

    public int HighestUnlockedLevel
        => _save.TryGet<int>("progression.highest_unlocked", out var v) ? v : 1;

    public LinearProgression(ISaveService save, IEventBus bus)
    {
        _save = save;
        _bus = bus;
        _sub = bus.On<LevelCompleted>().Subscribe(OnLevelCompleted);
    }

    private void OnLevelCompleted(LevelCompleted evt)
    {
        var current = CurrentLevel;
        if (int.TryParse(evt.LevelId, out var levelNum) && levelNum >= current)
        {
            var next = levelNum + 1;
            _save.Set("progression.current_level", next);
            _save.Set("progression.highest_unlocked", System.Math.Max(HighestUnlockedLevel, next));
            _save.RequestSave();
        }
    }

    public void Dispose() => _sub?.Dispose();
}
```

For non-linear progression (world map with branches), persist a `Dictionary<string, LevelStatus>` via Newtonsoft.Json.

---

## Reward grant

Generic "grant a basket of currencies + items" called from level complete, daily login, or IAP success. Decouples reward authoring (designer-owned ScriptableObjects) from grant code.

```csharp
[CreateAssetMenu(fileName = "Reward", menuName = "Game/Reward")]
public sealed class RewardDefinition : ScriptableObject
{
    public CurrencyAmount[] Currencies;
    public ItemAmount[] Items;

    [System.Serializable] public struct CurrencyAmount { public string CurrencyId; public long Amount; }
    [System.Serializable] public struct ItemAmount { public string ItemId; public int Count; }
}

public sealed class RewardGranter
{
    private readonly IWalletService _wallet;
    private readonly IInventoryService _inventory;
    private readonly IEventBus _bus;

    public void Grant(RewardDefinition def, string reason)
    {
        foreach (var c in def.Currencies)
            _wallet.Add(c.CurrencyId, c.Amount, reason);
        foreach (var i in def.Items)
            _inventory.Add(i.ItemId, i.Count, reason);
        _bus.Publish(new RewardGranted(def.name, reason));
    }
}
```

Subscribe a "+50 coins" particle UI to `RewardGranted` so the visual effect is decoupled from where the reward originated.

---

## Daily login

Uses `ITimeService` (not `DateTime.UtcNow` — testability + clock-cheat resistance) and `ISaveService` for streak state.

```csharp
public sealed class DailyLoginChecker
{
    private readonly ITimeService _time;
    private readonly ISaveService _save;

    public bool IsClaimable()
    {
        if (!_save.TryGet<long>("daily.last_claim_unix", out var lastClaim))
            return true;
        var lastDate = DateTimeOffset.FromUnixTimeSeconds(lastClaim).UtcDateTime.Date;
        var todayDate = DateTimeOffset.FromUnixTimeSeconds(_time.UnixTimeSeconds).UtcDateTime.Date;
        return todayDate > lastDate;
    }

    public int CurrentStreak()
    {
        if (!_save.TryGet<int>("daily.streak", out var s)) return 0;
        if (!_save.TryGet<long>("daily.last_claim_unix", out var lastClaim)) return 0;
        var daysSince = (DateTimeOffset.FromUnixTimeSeconds(_time.UnixTimeSeconds).UtcDateTime.Date
                       - DateTimeOffset.FromUnixTimeSeconds(lastClaim).UtcDateTime.Date).Days;
        return daysSince > 1 ? 0 : s;  // streak resets after a missed day.
    }

    public void Claim()
    {
        if (!IsClaimable()) return;
        _save.Set("daily.streak", CurrentStreak() + 1);
        _save.Set("daily.last_claim_unix", _time.UnixTimeSeconds);
        _save.RequestSave();
    }
}
```

If clock-cheat resistance is critical (e.g. cosmetics-only daily login is fine; gem grants need server validation), gate `Claim` on `_time.IsServerSynced`.

---

## A/B variants via remote config

`IRemoteConfigService.GetVariant<T>` returns the user's bucket. Sticky per `installation_id` driven by Firebase / Unity Remote Config.

```csharp
public sealed class DifficultyTuner
{
    private readonly IRemoteConfigService _remote;
    public float SpawnRateMultiplier => _remote.GetVariant("enemy_spawn_rate_mult", 1.0f);
    public int LivesPerLevel => (int)_remote.GetVariant<long>("lives_per_level", 3);
}
```

Read at the use site, not at startup — variants can re-fetch via `OnConfigUpdated` and you want the gameplay code to pick up changes between sessions without a restart.

For analytics tracking which arm a user landed in:

```csharp
[Inject] private IRemoteConfigService _remote;
[Inject] private IAnalyticsService _analytics;

private void Awake()
{
    var arm = _remote.GetVariant<string>("onboarding_flow", "control");
    _analytics.SetUserProperty("ab_onboarding", arm);
}
```

---

## Putting it together — a per-game `MetaInstaller`

```csharp
public static class MetaInstaller
{
    public static void Install(ContainerBuilder builder)
    {
        builder.RegisterType(typeof(SaveBackedWallet), new[] { typeof(IWalletService) },
            Lifetime.Singleton, Resolution.Lazy);
        builder.RegisterType(typeof(LinearProgression), new[] { typeof(IProgressionService) },
            Lifetime.Singleton, Resolution.Lazy);
        builder.RegisterType(typeof(RewardGranter), new[] { typeof(RewardGranter) },
            Lifetime.Singleton, Resolution.Lazy);
        builder.RegisterType(typeof(DailyLoginChecker), new[] { typeof(DailyLoginChecker) },
            Lifetime.Singleton, Resolution.Lazy);
    }
}
```

Call `MetaInstaller.Install(builder)` from `ProjectScopeInstaller.InstallBindings` after the template's services are registered (so the meta types can resolve `ISaveService`, `IEventBus`, etc.).

---

## What none of these recipes ship

- **No "RewardChestService" / "GachaService"** — pull-rate authoring is genre-specific and easy to get legally wrong (loot box regulations differ per market). Build behind a flag, validate against your jurisdiction.
- **No leaderboards / cloud save / friends list** — these need a backend the template doesn't provide.
- **No tutorial state machine** — gameplay state is what `IGameStateMachine` is for; tutorial routing usually piggybacks on level state, but the trigger logic ("show this hand pointer at coordinates X") is too game-specific to template.
- **No localization for currency names** — wire `IL10nService.Get("currency.coin")` at the UI layer; meta keeps using stable currency ids ("coin", "gem").

If a pattern from your game would generalize well across hybrid casual / puzzle, [open an issue](../../README.md#contributing) — recipes can move into the template if multiple consumers want them.
