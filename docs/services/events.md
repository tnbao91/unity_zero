# Events Service (Event Bus)

> **Design + architecture live in [`docs/architecture/event-bus.md`](../architecture/event-bus.md).** This page is the service-level quick reference; the architecture doc covers the cross-tier rationale, peer-asmdef rules, and event-type conventions. Read that one for the *why*.

## Overview

`IEventBus` is the type-keyed pub/sub bus that connects the peer tiers (`Zero.Gameplay` / `Zero.Meta` / `Zero.UI`), which by rule never reference each other. The shipped impl is `R3EventBus` (`Packages/com.tnbao91.nobody.zero/Runtime/Services/Events/R3EventBus.cs`) — a **real** service backed by R3 `Subject`s, one per event type.

Cross-tier coupling goes through this bus *only* (`CLAUDE.md` → Asmdef boundaries). A publisher needs no reference to its subscribers; both reference `Zero.Core` for the event POCO and `IEventBus`.

## Public API

```csharp
namespace Zero.Core
{
    public interface IEventBus
    {
        Observable<T> On<T>();   // subscribe to events of type T
        void Publish<T>(T evt);  // fire an event of type T
    }
}
```

| Member | Behavior |
|---|---|
| `On<T>()` | Returns the (lazily-created) `Observable<T>` for type `T`. Subscribe with `using R3;` at the top of the file. |
| `Publish<T>(evt)` | Synchronous dispatch to all current subscribers of `T`. Creates the subject lazily if none exists, so publishing before anyone subscribes is safe (the event is simply dropped — `Subject` has no replay). |

Storage is `Dictionary<Type, object>` holding `Subject<T>` cast on access — deliberately **not** `Dictionary<Type, Subject<object>>`, which would box every value-type event.

## Extension Points

The impl is `sealed`. Swap the binding in `EventsServiceInstaller.cs` for an instrumented or replay variant:

```csharp
builder.RegisterType(
    typeof(LoggingEventBus),
    new[] { typeof(IEventBus) },
    Lifetime.Singleton,
    Resolution.Lazy);
```

A logging decorator is the typical case:

```csharp
public sealed class LoggingEventBus : IEventBus
{
    private readonly IEventBus _inner;
    private readonly ILogService _log;
    public LoggingEventBus(IEventBus inner, ILogService log) { _inner = inner; _log = log; }
    public Observable<T> On<T>() => _inner.On<T>();
    public void Publish<T>(T evt) { _log.Info($"[BUS] {typeof(T).Name}"); _inner.Publish(evt); }
}
```

## Examples

Define the event as a POCO in `Zero.Core`, publish from one tier, subscribe from another:

```csharp
// Zero.Core
public readonly struct CoinsChanged { public readonly int Total; public CoinsChanged(int t) => Total = t; }

// Zero.Meta — publisher
_bus.Publish(new CoinsChanged(_wallet.Coins));

// Zero.UI — subscriber (needs `using R3;`)
_bus.On<CoinsChanged>()
    .Subscribe(e => _coinLabel.text = e.Total.ToString())
    .AddTo(this);
```

## Known Limitations

- **No replay / no last-value.** A subscriber that joins after a `Publish` misses it. For "current value" semantics, store state in a service and expose it directly, or use an R3 `ReactiveProperty` on that service instead of a fire-and-forget bus event.
- **Synchronous dispatch.** `Publish` runs subscribers inline on the calling thread; a slow handler blocks the publisher. Keep handlers cheap or marshal heavy work off the callback.
- **Single-threaded by Unity convention.** A lock guards the subject dictionary against rare DI-driven background access, but delivery context follows R3 defaults (subscriber's context). Don't publish gameplay events from background threads.

## Design Rationale

- **Type as the key.** No string topics to typo or namespace; the event class *is* the channel, and `On<CoinsChanged>()` is compile-time checked.
- **One `Subject<T>` per type, lazily created.** Avoids the boxing a `Subject<object>` would cause for struct events, and avoids pre-allocating channels nothing uses.
- **R3, not UniRx / C# events.** Matches the locked stack (`CLAUDE.md` → Reactive: R3) and gives `AddTo`, operators, and lifetime management for free. See `docs/dev/PITFALLS.md` → "Subscribe without `using R3;`" for the one footgun.
