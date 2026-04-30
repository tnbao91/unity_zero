using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using Zero.Core;

namespace Zero.Gameplay
{
    /// <summary>
    /// Flat state machine implementation. Rejects re-entry of the same state type unless explicit.
    /// Transitions are sequential: ExitAsync (old) → EnterAsync (new), then publish observable.
    /// Not a MonoBehaviour; consumer drives Tick from their update loop.
    /// </summary>
    public sealed class GameStateMachine : IGameStateMachine, IDisposable
    {
        private IGameState _currentState;
        private bool _isTransitioning;
        private Subject<IGameState> _onStateChanged = new();

        public IGameState CurrentState => _currentState;
        public Observable<IGameState> OnStateChanged => _onStateChanged;

        public void Dispose()
        {
            _onStateChanged?.Dispose();
            _onStateChanged = null;
        }

        /// <summary>
        /// Transition to a new state. Sequentially exits current, enters new, publishes observable.
        /// If already transitioning, queues the change (if supported) or throws (if not).
        /// </summary>
        public async UniTask ChangeStateAsync(IGameState newState, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();

            if (newState == null)
                throw new ArgumentNullException(nameof(newState));

            // Reject re-entry of the same instance (common mistake: forgetting to clear ref before retransitioning).
            if (_currentState != null && ReferenceEquals(_currentState, newState))
                throw new InvalidOperationException($"Attempt to re-enter state {newState.GetType().Name}. Create a fresh instance if the state is reusable.");

            // Reject concurrent transitions. Consumer is responsible for sequencing.
            // A real queue would add hidden ordering surprises; explicit reject is the
            // minimal-template choice per PLAN §3 Phase 4 ("queued or rejected — decide").
            if (_isTransitioning)
                throw new InvalidOperationException("A state transition is already in progress. Await the previous ChangeStateAsync before starting another.");

            _isTransitioning = true;
            try
            {
                // Exit current state
                if (_currentState != null)
                {
                    await _currentState.ExitAsync(ct);
                }

                // Enter new state
                _currentState = newState;
                await _currentState.EnterAsync(ct);

                // Publish state change
                _onStateChanged?.OnNext(_currentState);
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_onStateChanged == null)
                throw new ObjectDisposedException(nameof(GameStateMachine));
        }
    }
}
