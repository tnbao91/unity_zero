using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;
using Zero.Infrastructure;

namespace Zero.Bootstrap.Steps
{
    public sealed class DeviceProfileStep : BootstrapStepBase
    {
        public override string Name => "DeviceProfile";

        // Blocking — synchronous and free, and targetFrameRate wants to be set before the first frames.
        public override BootstrapPhase Phase => BootstrapPhase.Blocking;

        // Failure means quality settings were not applied — the game runs at Unity's
        // defaults. That is a worse-looking game, not an unplayable one.
        public override bool IsCritical => false;

        private readonly IDeviceProfileService _profile;
        private readonly ILogService _log;

        public DeviceProfileStep(IDeviceProfileService profile, ILogService log)
        {
            _profile = profile;
            _log = log;
        }

        protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
        {
            _profile.Apply();
            var p = _profile.Current;
            _log.Info($"[Bootstrap] Device tier={p.Tier}, fps={p.TargetFps}, msaa={p.MsaaSampleCount}, shadow={p.ShadowsEnabled}, postFx={p.PostProcessingEnabled}");
            return UniTask.CompletedTask;
        }
    }
}
