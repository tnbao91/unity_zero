using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Zero.Gameplay.States
{
    /// <summary>
    /// Reference implementation: minimal state shell.
    /// Consumers replace this with their own genre-specific menu logic.
    /// </summary>
    public sealed class MenuState : IGameState
    {
        public UniTask EnterAsync(CancellationToken ct)
        {
            Debug.Log("[Gameplay] MenuState.EnterAsync");
            return UniTask.CompletedTask;
        }

        public UniTask ExitAsync(CancellationToken ct)
        {
            Debug.Log("[Gameplay] MenuState.ExitAsync");
            return UniTask.CompletedTask;
        }

        public void Tick(float deltaTime)
        {
        }
    }
}
