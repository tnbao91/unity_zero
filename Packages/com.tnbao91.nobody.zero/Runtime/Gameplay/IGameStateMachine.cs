using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace Zero.Gameplay
{
    /// <summary>
    /// Flat state machine. Manages state transitions, async enter/exit, and publishes change observable.
    /// Thread-safe (single-threaded by Unity convention).
    /// </summary>
    public interface IGameStateMachine
    {
        /// <summary>
        /// Currently active state, or null if not yet entered.
        /// </summary>
        IGameState CurrentState { get; }

        /// <summary>
        /// Observable that fires once when the state changes (after ExitAsync + EnterAsync complete).
        /// </summary>
        Observable<IGameState> OnStateChanged { get; }

        /// <summary>
        /// Transition to a new state. Sequentially calls CurrentState.ExitAsync, then newState.EnterAsync.
        /// Throws InvalidOperationException if a transition is already in progress — consumer must
        /// await the previous call before starting another.
        /// Throws ArgumentNullException if newState is null, OperationCanceledException on cancel.
        /// </summary>
        UniTask ChangeStateAsync(IGameState newState, CancellationToken ct = default);
    }
}
