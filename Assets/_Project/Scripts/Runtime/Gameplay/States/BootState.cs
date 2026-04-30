using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Zero.Gameplay.States
{
    /// <summary>
    /// Reference implementation: minimal state shell.
    /// Consumers replace this with their own genre-specific boot logic.
    /// </summary>
    public sealed class BootState : IGameState
    {
        public UniTask EnterAsync(CancellationToken ct)
        {
            Debug.Log("[Gameplay] BootState.EnterAsync");
            return UniTask.CompletedTask;
        }

        public UniTask ExitAsync(CancellationToken ct)
        {
            Debug.Log("[Gameplay] BootState.ExitAsync");
            return UniTask.CompletedTask;
        }

        public void Tick(float deltaTime)
        {
        }
    }
}
