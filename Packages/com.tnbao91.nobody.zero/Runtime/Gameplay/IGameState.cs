using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Zero.Gameplay
{
    /// <summary>
    /// Contract for a state in a flat state machine.
    /// States are entered/exited asynchronously and may tick at application's discretion.
    /// </summary>
    public interface IGameState
    {
        /// <summary>
        /// Called when entering this state. Use for async initialization (load assets, etc).
        /// Cancellation from the outer token propagates; timeout cancellation is the caller's concern.
        /// </summary>
        UniTask EnterAsync(CancellationToken ct);

        /// <summary>
        /// Called when exiting this state. Use for async cleanup (save, unload, etc).
        /// Must be exception-safe and idempotent.
        /// </summary>
        UniTask ExitAsync(CancellationToken ct);

        /// <summary>
        /// Per-frame / per-logic-update hook. Called by the consumer's update loop;
        /// not called by the state machine itself.
        /// </summary>
        void Tick(float deltaTime);
    }
}
