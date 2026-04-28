# Event Bus

## Overview

`IEventBus` is the unified mechanism for cross-asmdef communication in Unity Zero. Services, UI layers, and gameplay logic publish domain events (currency earned, level completed, settings changed) and subscribe to events published by other layers without direct references. This decoupling is critical because the three game-logic peers (UI, Gameplay, Meta) must never import each other.

## Public API

```csharp
// Core interface lives in Zero.Core
public interface IEventBus
{
    Observable<T> On<T>();  // Subscribe to events of type T
    void Publish<T>(T evt);  // Broadcast an event to all subscribers of type T
}

// Implementation in Zero.Services.Events
public sealed class R3EventBus : IEventBus, IDisposable
{
    public Observable<T> On<T>() { ... }
    public void Publish<T>(T evt) { ... }
    public void Dispose() { ... }
}
```

## Extension Points

**Custom event types:** any `class` or `struct` works. Prefer `record struct` for event POCOs (zero-alloc, immutable by default).

```csharp
// In your gameplay asmdef
public record struct LevelCompletedEvent(int LevelId, int Stars);

// In your UI asmdef (no reference to Gameplay)
private readonly IEventBus _bus;
public void Start()
{
    _bus.On<LevelCompletedEvent>()
        .Subscribe(e => ShowVictoryPopup(e.Stars));
}
```

**Replacing the bus.** `R3EventBus` is `sealed` — for batching, filtering, or per-event logging, write a new `IEventBus` impl (e.g. wrapping `R3EventBus` in a decorator) and rebind it in your own `<Game>ScopeInstaller.UserServices.cs` partial. Reflex picks the last registration.

## Examples

**Centralized currency display (Meta → UI):**
```csharp
// In Zero.Meta (or consumer's Meta layer)
public record struct CurrencyChangedEvent(string Currency, int Amount);

// Somewhere in meta logic
_eventBus.Publish(new CurrencyChangedEvent("gold", 100));

// In Zero.UI
public sealed class CurrencyHUD : MonoBehaviour
{
    [Inject] private IEventBus _bus;
    
    private void OnEnable()
    {
        _subscription = _bus.On<CurrencyChangedEvent>()
            .Subscribe(e => _currencyText.text = e.Amount.ToString());
    }
}
```

**Level lifecycle (Gameplay → Meta/UI):**
```csharp
// In Zero.Gameplay
public record struct LevelStartedEvent(int LevelId);
public record struct LevelCompletedEvent(int LevelId, int Stars);

// In Gameplay logic
_eventBus.Publish(new LevelStartedEvent(currentLevelId));
// ...
_eventBus.Publish(new LevelCompletedEvent(currentLevelId, earnedStars));

// In Meta or UI
_eventBus.On<LevelCompletedEvent>()
    .Subscribe(e => AwardReward(e.Stars));
```

## Known Limitations

- **No type hierarchy:** `On<Animal>()` will NOT match `Publish(new Dog())`. Each event type must be subscribed explicitly. The bus is keyed by `typeof(T)`, not by assignability.
- **No ordering guarantee:** when multiple subscribers exist on the same type, their relative call order is the order R3's `Subject<T>` happens to iterate its observers. Treat it as undefined and design events to be idempotent.
- **Lock granularity:** the dictionary access is guarded by a single `lock` (the dictionary itself); each `Publish` and `On` takes that lock briefly. The actual `Subject<T>.OnNext` runs outside the lock so subscribers can re-enter, but extremely high publish rates from many threads will contend on it. Hybrid casual rates (<1000 events/s) are nowhere near that ceiling.
- **No replay for late subscribers:** the bus stores `Subject<T>`, not `BehaviorSubject<T>` or `ReplaySubject<T>`. A subscriber added after a publish gets nothing; subscribe before the first publish or use a ReactiveProperty inside the publishing service if you need last-value semantics.

## Design Rationale

Why a bus instead of direct service references? Three design reasons:

1. **Peer asmdef enforcement:** Zero.Gameplay, Zero.UI, and Zero.Meta cannot reference each other (Codex review point). A bus allows Gameplay to notify UI without `using Zero.UI`.

2. **Lazy initialization:** subscribers can be added before the publisher is instantiated (e.g., UI loaded before Gameplay manager). The bus tolerates subscriber registration on non-existent event types.

3. **Hot-swappable logic:** in editor play sessions, you can tear down/rebuild subsystems without cascade unsubscribe code. Each system independently subscribes on startup.

**Why `Dictionary<Type, object>`?** Because event POCOs are user-defined and live in different asmdefs (Gameplay defines `LevelCompleted`, Meta defines `RewardEarned`). A strongly-typed container would require the bus to reference all consuming asmdefs, creating the circular-import problem it solves. The `Type` key + reference cast (`(Subject<T>)slot`) trades one cast per access for zero coupling. The deliberately-avoided alternative — `Dictionary<Type, Subject<object>>` — would have boxed value-type events on every `OnNext` into the slot's `object` parameter. The current shape forwards `T` strongly through `Subject<T>.OnNext(T)`, so a `record struct` event allocates nothing on publish.
