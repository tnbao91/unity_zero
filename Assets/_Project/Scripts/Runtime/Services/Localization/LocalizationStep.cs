using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Localization.Settings;
using Zero.Core;
using Zero.Infrastructure;

namespace Zero.Services.Localization
{
    // Awaits Unity Localization's InitializationOperation so locale + tables are
    // ready before any UI subscribes via IL10nService. Non-critical on purpose:
    // a missing locale shouldn't kill app launch — UI falls back to keys.
    public sealed class LocalizationStep : BootstrapStepBase
    {
        public override string Name => "Localization";
        public override bool IsCritical => false;

        // Unused field today; reserved so signature lines up with other steps that
        // depend on the resolved service (and future hooks like preload-table).
        private readonly IL10nService _service;

        public LocalizationStep(IL10nService service)
        {
            _service = service;
        }

        protected override async UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
        {
            await LocalizationSettings.InitializationOperation.ToUniTask(progress: progress, cancellationToken: ct);
        }
    }
}
