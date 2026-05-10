# Extension points

How to extend Zero from YOUR game code without modifying the package.

## 1. Swap a mock SDK to a real one

Almost every third-party SDK service is a mock. Swap by writing a real impl + overriding the binding.

```csharp
// Assets/_Game/Services/FirebaseCrashlyticsService.cs (YOUR game)
public sealed class FirebaseCrashlyticsService : ICrashlyticsService
{
    public void LogException(Exception ex) => Firebase.Crashlytics.Crashlytics.LogException(ex);
    public void SetUserId(string id) => Firebase.Crashlytics.Crashlytics.SetUserId(id);
    public void SetCustomKey(string key, string value) => Firebase.Crashlytics.Crashlytics.SetCustomKey(key, value);
}

// Assets/_Game/Bootstrap/MyGameOverridesInstaller.cs
using Reflex.Core;
using Reflex.Attributes;
using Zero.Core;

public static class MyGameOverridesInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Hook() => ContainerScope.OnRootContainerBuilding += Install;

    private static void Install(ContainerBuilder builder)
    {
        // Re-register over Zero's mock binding (last write wins)
        builder.RegisterType<FirebaseCrashlyticsService>(typeof(ICrashlyticsService),
            Lifetime.Singleton, Resolution.Lazy);
    }
}
```

Recipes per SDK at upstream `docs/services/<sdk>.md`:
- `crashlytics.md` — Firebase Crashlytics / Sentry
- `consent.md` — Google UMP / Unity ATT
- `remote-config.md` — Firebase Remote Config / Unity Remote Config
- `analytics.md` — GA4 / Firebase Analytics / Unity Analytics
- `attribution.md` — AppsFlyer / Adjust
- `ads.md` — AppLovin MAX / IronSource / AdMob
- `iap.md` — Unity IAP / RevenueCat
- `receipt-validator.md` — server-side validation patterns

## 2. Add a game state

Game state machine (`IGameStateMachine`) ships with sample states (BootState, MenuState, PlayState, PauseState, ResultState) — replace them.

```csharp
// Assets/_Game/States/MyMatch3PlayState.cs
public sealed class MyMatch3PlayState : IGameState
{
    private readonly IEventBus _bus;
    private readonly IUIService _ui;

    public MyMatch3PlayState(IEventBus bus, IUIService ui) { _bus = bus; _ui = ui; }

    public async UniTask EnterAsync(CancellationToken ct)
    {
        await _ui.ShowScreen<HudScreen>(ct);
        _bus.Publish(new LevelStarted(level: 1, mode: "match3"));
    }

    public async UniTask ExitAsync(CancellationToken ct) { /* cleanup */ }

    public void Tick(float deltaTime) { /* per-frame */ }
}

// Switch to it
await stateMachine.ChangeStateAsync(new MyMatch3PlayState(bus, ui), ct);
```

**Don't share state instances across transitions** — concurrent and same-instance re-entry both throw.

## 3. Add a popup

```csharp
// Assets/_Game/UI/Popups/CoinPackPopup.cs
public sealed class CoinPackPopup : PopupBase<CoinPackData, CoinPackResult>
{
    [Inject] private IIapService _iap;

    protected override async UniTask OnShowAsync(CancellationToken ct)
    {
        // populate from Data
    }

    private async UniTask OnBuyClicked()
    {
        var ok = await _iap.PurchaseAsync(Data.ProductId);
        Close(new CoinPackResult { Bought = ok });
    }
}
```

Place prefab at Addressables key `ui/popup/coinpackpopup` (lowercase of class name).

Show:
```csharp
var result = await _ui.PushAsync<CoinPackPopup, CoinPackData, CoinPackResult>(
    new CoinPackData { ProductId = "coins_500" }, ct);
```

**Prerequisite:** scene has a `UIRoot` MonoBehaviour with Hud/Popup/Overlay/System Transform slots.

## 4. Add a cheat command

```csharp
// Assets/_Game/Cheats/AddCoinsCommand.cs
[ConsoleCommand("coins add", "Add coins. Usage: coins add <amount>")]
public sealed class AddCoinsCommand : IConsoleCommand
{
    [Inject] private ISaveService _save;

    public string Execute(string[] args)
    {
        if (!int.TryParse(args[0], out var amount)) return "usage: coins add <int>";
        var current = _save.Get<int>("game.coins", 0);
        _save.Set("game.coins", current + amount);
        return $"+{amount} coins (now {current + amount})";
    }
}
```

Auto-discovered via reflection at console init. Tilde key (PC) / 4-finger tap (mobile) opens. Available in Editor + Development Build only (asmdef gated).

## 5. Add a bootstrap step

```csharp
// Assets/_Game/Bootstrap/MyGameDataStep.cs
public sealed class MyGameDataStep : BootstrapStepBase
{
    public override string Name => "MyGameData";
    public override bool IsCritical => false; // pipeline continues even if this fails
    public override TimeSpan Timeout => TimeSpan.FromSeconds(15);

    private readonly IAssetService _assets;
    public MyGameDataStep(IAssetService assets) { _assets = assets; }

    protected override async UniTask ExecuteAsync(IProgress<float> progress, CancellationToken ct)
    {
        progress?.Report(0.5f);
        await _assets.LoadAsync<GameDataAsset>("game/data/main", ct);
        progress?.Report(1f);
    }
}
```

Wire via partial extension OR your own installer:

```csharp
// Override the steps list — add yours after Zero's
// Cleanest: extend ProjectScopeInstaller via partial class file in YOUR asmdef
```

## 6. Persist game state

Always use `ISaveService`. Keys are strings — namespace them:

```csharp
_save.Set("game.match3.level", 5);
_save.Set("game.match3.coins", 1234);
var unlocked = _save.Get<bool>("game.tutorial.completed", false);
await _save.SaveAsync(); // explicit flush; auto-saves on app pause
```

For schema migration, define keys in YOUR data classes and version them:
```csharp
public sealed class GameSaveData
{
    public int Version = 2;
    public int Coins;
    public List<int> UnlockedLevels;
}
// Old v1 had `int LevelsCompleted` (count); v2 splits into list — write Migrate
```

`EncryptedJsonSaveService.Migrate(JObject root, int from, int to)` is virtual; override in YOUR `<Game>SaveService : EncryptedJsonSaveService` if you need it. But re-binding the save service is rare — most games just version individual keys.

## 7. Add a custom service (per-game)

```csharp
// Assets/_Game/Services/Shop/IShopService.cs (YOUR namespace)
public interface IShopService
{
    UniTask<bool> BuyAsync(string productId, CancellationToken ct);
    Observable<ShopItem> OnUnlocked { get; }
}

// Assets/_Game/Services/Shop/ShopService.cs
public sealed class ShopService : IShopService
{
    private readonly IIapService _iap;
    private readonly ISaveService _save;
    public ShopService(IIapService iap, ISaveService save) { _iap = iap; _save = save; }
    // impl
}

// Assets/_Game/Bootstrap/MyGameInstaller.cs
public static class MyGameInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Hook() => ContainerScope.OnRootContainerBuilding += Install;

    private static void Install(ContainerBuilder builder)
    {
        builder.RegisterType<ShopService>(typeof(IShopService),
            Lifetime.Singleton, Resolution.Lazy);
    }
}
```

The hook runs after Zero's installers (assuming your game's `BeforeSceneLoad` registers later in the call order). For deterministic ordering, override `[DefaultExecutionOrder]` or use a single combined installer.

## 8. Subscribe to lifecycle events (decoupled)

```csharp
// In your meta progression code (Zero.Meta peer or your asmdef)
private IDisposable _sub;

void Start()
{
    _sub = _bus.On<LevelCompleted>().Subscribe(OnLevelCompleted);
}

void OnDestroy() => _sub?.Dispose();

private void OnLevelCompleted(LevelCompleted evt)
{
    var coins = evt.Stars * 50;
    _save.Set("game.coins", _save.Get<int>("game.coins", 0) + coins);
    _bus.Publish(new CurrencyChanged(currency: "coins", delta: coins));
}
```

The publisher (Gameplay) doesn't know about the subscriber (Meta). That's the point.
