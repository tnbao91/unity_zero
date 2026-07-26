using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Zero.Core
{
    /// <summary>
    /// When a step runs relative to the player reaching the game.
    /// </summary>
    public enum BootstrapPhase
    {
        /// <summary>
        /// Runs before <c>BootstrapReady</c>. The player is staring at a splash screen for
        /// the whole of this phase, so it is a budget, not a queue — put a step here only if
        /// the first screen is wrong without it.
        /// </summary>
        Blocking = 0,

        /// <summary>
        /// Runs after <c>BootstrapReady</c>, while the player is already in the game. Costs
        /// them nothing. This is the right phase for almost everything: ads, IAP, analytics,
        /// attribution, consent, notifications, remote config.
        /// </summary>
        Deferred = 1,
    }

    public interface IBootstrapStep
    {
        string Name { get; }
        bool IsCritical { get; }

        // Blocking steps delay the player; deferred steps do not. Defaults to Blocking in
        // BootstrapStepBase so an existing consumer step keeps its current semantics rather
        // than silently sliding off the boot path.
        BootstrapPhase Phase { get; }

        // Per-step deadline; pipeline cancels with linked CTS if breached.
        // Network-bound steps (RemoteConfig, Crashlytics) override to widen.
        TimeSpan Timeout { get; }

        // How many extra attempts after first failure for non-critical steps.
        // Critical steps fail-fast on first throw and ignore MaxRetries.
        int MaxRetries { get; }

        UniTask ExecuteAsync(IProgress<float> progress, CancellationToken ct);
    }
}
