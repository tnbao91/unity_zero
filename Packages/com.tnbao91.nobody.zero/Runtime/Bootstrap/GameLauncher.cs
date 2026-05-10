using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using UnityEngine;
using Zero.Core;

namespace Zero.Bootstrap
{
    [DefaultExecutionOrder(-100)]
    public sealed class GameLauncher : MonoBehaviour
    {
        [Inject] private BootstrapPipeline _pipeline;
        [Inject] private ILogService _log;

        private CancellationTokenSource _cts;

        private void Start()
        {
            _cts = new CancellationTokenSource();
            RunAsync().Forget();
        }

        private async UniTaskVoid RunAsync()
        {
            try
            {
                _log.Info("[Bootstrap] GameLauncher started.");
                await _pipeline.RunAsync(null, _cts.Token);
                _log.Info("[Bootstrap] All steps completed.");
            }
            catch (OperationCanceledException)
            {
                _log.Warn("[Bootstrap] Cancelled.");
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[Bootstrap] Pipeline failed");
            }
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
