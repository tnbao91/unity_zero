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
public sealed class R3EventBus : IEventBus
{
    public Observable<T> On<T>() { ... }
    public void Publish<T>(T evt) { ... }
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

**Bus extension:** the current implementation is a thin `Dictionary<Type, object>` storing `Subject<T>`. To add batching, filtering, or logging, subclass `R3EventBus` and override `On<T>()` or `Publish<T>()`.

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

- **Value-type boxing:** `Record struct` events get boxed when stored in the internal `Dictionary<Type, object>` and unboxed on each publish. For very high-frequency events (>1000/s), use R3 streams directly in the publishing service instead of the bus.
- **No ordering guarantee:** if multiple subscribers exist, their relative call order is undefined. Design events to be idempotent or use a request/response pattern (via UniTask.Future + completion sources) for sequential ordering.
- **No type hierarchy:** `On<Animal>()` will NOT match `Publish(new Dog())`. Each event type must be subscribed explicitly.

## Design Rationale

Why a bus instead of direct service references? Three design reasons:

1. **Peer asmdef enforcement:** Zero.Gameplay, Zero.UI, and Zero.Meta cannot reference each other (Codex review point). A bus allows Gameplay to notify UI without `using Zero.UI`.

2. **Lazy initialization:** subscribers can be added before the publisher is instantiated (e.g., UI loaded before Gameplay manager). The bus tolerates subscriber registration on non-existent event types.

3. **Hot-swappable logic:** in editor play sessions, you can tear down/rebuild subsystems without cascade unsubscribe code. Each system independently subscribes on startup.

**Why `Dictionary<Type, object>`?** Because event POCOs are user-defined and live in different asmdefs (Gameplay defines `LevelCompleted`, Meta defines `RewardEarned`). A strongly-typed container would require the bus to reference all consuming asmdefs, creating the circular-import problem it solves. The `Type` key + cast-on-access trades a tiny performance cost (one cast per publish) for zero coupling.
