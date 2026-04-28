using R3;

namespace Zero.Core
{
    // Progress reporter consumed by LoadingScreen + written by BootstrapPipeline.
    // Combined read/write interface for simplicity in Phase 1a — Pipeline writes via Report();
    // Views observe Progress + CurrentStepName. If split discipline becomes important
    // later, two role interfaces (IBootstrapProgressWriter, IBootstrapProgressReader)
    // can be introduced without breaking consumers.
    public interface IBootstrapProgressReporter
    {
        Observable<float> Progress { get; }
        Observable<string> CurrentStepName { get; }
        void Report(float progress, string stepName);
    }
}
