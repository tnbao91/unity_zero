using System;
using R3;
using Zero.Core;

namespace Zero.Infrastructure
{
    // Singleton bridge between BootstrapPipeline (writer) and LoadingScreen / HUD
    // displays (readers). Lives in Infrastructure so Pipeline + UI both depend on a
    // shared layer without either reaching across to the other. Avoids the Lazy
    // resolution race that would happen if LoadingScreen tried to resolve the
    // Pipeline directly.
    public sealed class BootstrapProgressReporter : IBootstrapProgressReporter, IDisposable
    {
        private readonly Subject<float> _progress = new();
        private readonly Subject<string> _stepName = new();
        private bool _disposed;

        public Observable<float> Progress => _progress;
        public Observable<string> CurrentStepName => _stepName;

        public void Report(float progress, string stepName)
        {
            if (_disposed) return;
            _progress.OnNext(progress);
            if (!string.IsNullOrEmpty(stepName)) _stepName.OnNext(stepName);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _progress.Dispose();
            _stepName.Dispose();
        }
    }
}
