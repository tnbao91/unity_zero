using System;

namespace Zero.Core
{
    // Thrown by BootstrapPipeline when a critical step fails or times out.
    // Carries the step identity so catch sites don't parse it out of message
    // strings; the original failure is preserved as InnerException.
    public sealed class BootstrapStepFailedException : Exception
    {
        public string StepName { get; }
        public int Attempt { get; }

        public BootstrapStepFailedException(string stepName, int attempt, Exception innerException)
            : base($"Bootstrap step '{stepName}' failed on attempt {attempt}.", innerException)
        {
            StepName = stepName;
            Attempt = attempt;
        }
    }
}
