using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Zero.Gameplay.States
{
    /// <summary>
    /// Reference implementation: minimal state shell.
    /// Consumers replace this with their own genre-specific pause logic.
    /// </summary>
    public sealed class PauseState : IGameState
    {
        public UniTask EnterAsync(CancellationToken ct)
        {
            Debug.Log("[Gameplay] PauseState.EnterAsync");
            return UniTask.CompletedTask;
        }

        public UniTask ExitAsync(CancellationToken ct)
        {
            Debug.Log("[Gameplay] PauseState.ExitAsync");
            return UniTask.CompletedTask;
        }

        public void Tick(float deltaTime)
        {
        }
    }
}
