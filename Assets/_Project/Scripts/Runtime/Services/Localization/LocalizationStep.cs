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

        private readonly IL10nService _service;
        private readonly ILogService _log;

        public LocalizationStep(IL10nService service, ILogService log)
        {
            _service = service;
            _log = log;
        }

        protected override async UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
        {
            // Fresh template ships no LocalizationSettings asset / no built Locales.
            // Skip with a warning instead of letting the package throw
            // InvalidKeyException through Debug.LogException (which surfaces as a
            // red console error even though this step is non-critical).
            if (!LocalizationSettings.HasSettings)
            {
                _log?.Warn("[L10n] No LocalizationSettings asset configured — skipping init. Configure via Window → Asset Management → Localization Tables.");
                progress?.Report(1f);
                return;
            }

            try
            {
                await LocalizationSettings.InitializationOperation.ToUniTask(progress: progress, cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log?.Warn($"[L10n] Initialization failed; continuing without localization. {ex.GetType().Name}: {ex.Message}");
                progress?.Report(1f);
            }
        }
    }
}
