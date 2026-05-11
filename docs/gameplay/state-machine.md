# State Machine

## Overview

`GameStateMachine` is a flat (non-hierarchical) state machine for managing gameplay flow. States are async-aware (`EnterAsync`, `ExitAsync`) and tickable. Consumer drives state changes from their own logic (e.g., level-loaded → PlayState, level-complete → ResultState).

## Public API

```csharp
public interface IGameState
{
    UniTask EnterAsync(CancellationToken ct);
    UniTask ExitAsync(CancellationToken ct);
    void Tick(float deltaTime);
}

public interface IGameStateMachine
{
    IGameState CurrentState { get; }
    Observable<IGameState> OnStateChanged { get; }
    UniTask ChangeStateAsync(IGameState newState, CancellationToken ct = default);
}
```

### Lifetime & Thread Safety
- `GameStateMachine` is a singleton in the Reflex container (registered in `GameplayServiceInstaller`).
- Single-threaded (Unity convention); no locks needed.
- Dispose when app quits (`IDisposable`; container handles this).

### State Transition Behavior
- `ChangeStateAsync` is sequential: calls `ExitAsync` on the old state, then `EnterAsync` on the new.
- Publishes `OnStateChanged` observable only after both calls complete.
- Throws `InvalidOperationException` if you attempt to re-enter the same state instance (guard against logic bugs).
- Throws `ArgumentNullException` if `newState` is null.
- Cancellation from the caller's token propagates and aborts the transition.
- Concurrent calls are serialized via yield loops (no explicit locking).

## Extension Points

### Custom State Shells
Create state classes implementing `IGameState`:

```csharp
public sealed class MyGameState : IGameState
{
    private readonly IAssetService _assets;
    private GameObject _levelRoot;

    public MyGameState(IAssetService assets)
    {
        _assets = assets;
    }

    public async UniTask EnterAsync(CancellationToken ct)
    {
        _levelRoot = await _assets.LoadAsync<GameObject>("level_prefab", ct);
        _levelRoot.SetActive(true);
    }

    public async UniTask ExitAsync(CancellationToken ct)
    {
        _levelRoot.SetActive(false);
        Object.Destroy(_levelRoot);
    }

    public void Tick(float deltaTime)
    {
        // Called by consumer's Update loop
    }
}
```

### Listening to State Changes
Consumer can react to state transitions:

```csharp
[Inject] private IGameStateMachine _stateMachine;

private void Awake()
{
    _stateMachine.OnStateChanged.Subscribe(newState =>
    {
        Debug.Log($"State changed to {newState.GetType().Name}");
    });
}
```

### Driving State Changes
Consumer owns the logic for when to transition:

```csharp
private async void OnPlayButtonPressed()
{
    await _stateMachine.ChangeStateAsync(new PlayState());
}

private async void OnLevelComplete()
{
    await _stateMachine.ChangeStateAsync(new ResultState());
}
```

## Examples

### Minimal Play Loop
```csharp
public sealed class GameController : MonoBehaviour
{
    [Inject] private IGameStateMachine _stateMachine;
    [SerializeField] private float _tickDeltaTime = 0.016f;

    private void Start()
    {
        // Consumer initializes the state machine
        _stateMachine.ChangeStateAsync(new MenuState()).Forget();
    }

    private void Update()
    {
        // Consumer drives Tick
        _stateMachine.CurrentState?.Tick(_tickDeltaTime);
    }
}
```

### Handling Cancellation
```csharp
private CancellationTokenSource _cts = new();

private async void OnAppPause()
{
    // Cancel any in-flight state transitions
    _cts.Cancel();
    _cts = new();
}

private async void GoToPlayState()
{
    try
    {
        await _stateMachine.ChangeStateAsync(new PlayState(), _cts.Token);
    }
    catch (OperationCanceledException)
    {
        Debug.Log("Play transition cancelled (app paused)");
    }
}
```

## Known Limitations

- **Flat only** — no hierarchical (nested) states. If your game needs substates (e.g., PlayState with idle / attack / damage substates), implement a nested state machine *inside* PlayState.
- **No transition validation** — the machine does not prevent illogical transitions (e.g., PlayState → PlayState is allowed if you create a fresh instance). Enforce game rules in your caller logic.
- **No timeout on transitions** — `EnterAsync` / `ExitAsync` run to completion. If a state hangs, the machine hangs. Use a linked `CancellationTokenSource` if you need timeouts (example in Known Limitations of `CLAUDE.md`).
- **CurrentState is public** — consumer can read but not set it directly (no property setter). Use `ChangeStateAsync` only.

## Design Rationale

**Why flat?** Most hybrid casual / puzzle games use a simple state diagram (Boot → Menu → Loading → Play → Result → Menu). HSM is over-engineered for that. If you need substates, nest another state machine inside the state you're extending.

**Why async enter/exit?** Gameplay state transitions often involve asset loading (level prefabs), animations (fades), and async cleanup. Forcing synchronous entry/exit either blocks the frame or requires an extra coordinator layer. UniTask's async API lets each state own its initialization without boilerplate.

**Why observable instead of events?** R3 observables integrate naturally with the existing `IEventBus` for cross-layer communication. State changes are usually interesting to UI and other peers; an observable is the idiomatic way to push them.

**Why reject re-entry of same instance?** Accidental reference holding is a common bug (e.g., cache a PlayState instance, forget to clear it, try to re-enter it later). The guard catches this at dev time instead of letting a silent no-op pass.

**Why genre-agnostic?** `Zero.Gameplay` ships only the state-machine + level-loading scaffolds + 5 lifecycle events on `IEventBus`. Genre-specific systems (grid, runner, idle, merge, match-3) are explicitly out of template scope (per `docs/dev/PLAN.md` §1). Consumer adds those in their own game asmdef and references `Zero.Gameplay` + `Zero.Services.Events`. **Never add genre-specific code into `Zero.Gameplay`** — review will reject it.
