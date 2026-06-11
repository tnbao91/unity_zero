using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using Reflex.Attributes;
using UnityEngine;
using Zero.Core;
using Zero.Core.Events;

namespace Zero.Bootstrap
{
    [DefaultExecutionOrder(-100)]
    public sealed class GameLauncher : MonoBehaviour
    {
        [Inject] private BootstrapPipeline _pipeline;
        [Inject] private ILogService _log;
        [Inject] private IEventBus _eventBus;

        private CancellationTokenSource _cts;
        private IDisposable _retrySubscription;
        private bool _isRunning;

        private void Start()
        {
            _cts = new CancellationTokenSource();
            _log.Info("[Bootstrap] GameLauncher started.");

            // Whole-run retry is launcher policy; the pipeline owns per-step retry.
            // On abort the pipeline publishes BootstrapFailed; a consumer loading
            // screen surfaces retry UI and publishes BootstrapRetryRequested. The
            // re-run executes every step again — steps must be idempotent (PITFALLS).
            _retrySubscription = _eventBus.On<BootstrapRetryRequested>()
                .Subscribe(_ => RunPipeline());

            RunPipeline();
        }

        private void RunPipeline()
        {
            if (_isRunning)
            {
                _log.Warn("[Bootstrap] Run requested while pipeline is already running. Ignored.");
                return;
            }
            RunAsync().Forget();
        }

        private async UniTaskVoid RunAsync()
        {
            _isRunning = true;
            try
            {
                await _pipeline.RunAsync(null, _cts.Token);
                _log.Info("[Bootstrap] All steps completed.");
            }
            catch (OperationCanceledException)
            {
                _log.Warn("[Bootstrap] Cancelled.");
            }
            catch (Exception ex)
            {
                // BootstrapFailed was already published by the pipeline; consumers
                // own the retry UX from there. Here we only record the failure.
                _log.Error(ex, "[Bootstrap] Pipeline failed");
            }
            finally
            {
                _isRunning = false;
            }
        }

        private void OnDestroy()
        {
            _retrySubscription?.Dispose();
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
