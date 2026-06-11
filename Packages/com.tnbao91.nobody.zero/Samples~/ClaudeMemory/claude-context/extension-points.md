# Extension points

How to extend Zero from YOUR game code without modifying the package.

## 1. Swap a mock SDK to a real one

Almost every third-party SDK service is a mock. Swap by writing a real impl + overriding the binding.

```csharp
// Assets/_Game/Services/FirebaseCrashlyticsService.cs (YOUR game)
public sealed class FirebaseCrashlyticsService : ICrashlyticsService
{
    public async UniTask InitializeAsync(CancellationToken ct = default)
        => await Firebase.FirebaseApp.CheckAndFixDependenciesAsync()
            .AsUniTask().AttachExternalCancellation(ct);

    public void RecordException(Exception ex) => Firebase.Crashlytics.Crashlytics.LogException(ex);
    public void Log(string message) => Firebase.Crashlytics.Crashlytics.Log(message);
    public void SetCustomKey(string key, string value) => Firebase.Crashlytics.Crashlytics.SetCustomKey(key, value);
    public void SetUserId(string id) => Firebase.Crashlytics.Crashlytics.SetUserId(id);
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

After swapping a real SDK in, re-review that step's `IsCritical`/`Timeout` — mocks are instant, real vendors hang (see upstream PITFALLS "Swapping a real SDK into a bootstrap step").

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

Game state machine (`IGameStateMachine`) needs consumer-authored game states — the template ships none. Implement `IGameState` per genre.

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
        _save.TryGet("game.coins", out int current); // false → current stays 0
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

    protected override async UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
    {
        progress?.Report(0.5f);
        await _assets.LoadAsync<GameDataAsset>("game/data/main", ct);
        progress?.Report(1f);
    }
}
```

Wire by registering a `BootstrapStepRegistration` in your installer (same `OnRootContainerBuilding` hook as recipe 7). Anchors: `Append` (default), or `Before`/`After`/`Replace` + an existing step's `Name` — the default-step table lives in upstream `docs/architecture/bootstrap-pipeline.md`:

```csharp
// Inside your Install(ContainerBuilder builder):
builder.RegisterFactory(
    c => new BootstrapStepRegistration(
        new MyGameDataStep(c.Resolve<IAssetService>()),
        BootstrapStepAnchor.After, "Save"),
    new[] { typeof(BootstrapStepRegistration) },
    Lifetime.Singleton, Resolution.Lazy);
```

Do NOT try to extend `ProjectScopeInstaller` with a partial class — C# partials cannot span assemblies, so that path only exists for template forks, never for UPM consumers. A typo'd anchor name throws at boot instead of silently skipping your step. Re-runs (`BootstrapRetryRequested` after a `BootstrapFailed`) execute every step again — keep steps idempotent.

## 6. Persist game state

Always use `ISaveService`. Keys are strings — namespace them:

```csharp
_save.Set("game.match3.level", 5);
_save.Set("game.match3.coins", 1234);
_save.TryGet("game.tutorial.completed", out bool unlocked); // false when absent
await _save.SaveAsync(); // explicit flush
```

There is **no automatic pause save** — wire it yourself, it is required on mobile (suspended apps get killed with no callback):

```csharp
private void OnApplicationPause(bool pauseStatus)
{
    if (pauseStatus) _save.SaveAsync().Forget();
}
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

`EncryptedJsonSaveService` is `sealed` and its `Migrate` hook is `private static` — you cannot subclass it. Version individual keys in your own data classes (as above), or swap the whole `ISaveService` binding for your own impl (recipe 1 pattern) when you need real envelope migrations. Corrupt saves are quarantined to `save.dat.corrupt` before reset; recovery/forensics details in upstream `docs/services/save.md`.

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

Ordering is guaranteed by load type, not luck: Zero subscribes at `BeforeSplashScreen`, your `BeforeSceneLoad` hook always runs after it, so your registrations land **last** — and Reflex resolves the last registration per contract. That is exactly what makes the recipe-1 mock-override deterministic. (`[DefaultExecutionOrder]` has no effect here — it orders MonoBehaviour callbacks, not `RuntimeInitializeOnLoadMethod`.)

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
    _save.TryGet("game.coins", out int current);
    _save.Set("game.coins", current + coins);
    _bus.Publish(new CurrencyChanged(currency: "coins", delta: coins));
}
```

The publisher (Gameplay) doesn't know about the subscriber (Meta). That's the point.
