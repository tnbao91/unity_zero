using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;

namespace Zero.DevTools
{
    [ConsoleCommand("save", "Usage: save reset — clears all saved data")]
    public sealed class SaveResetCommand : IConsoleCommand
    {
        private readonly ISaveService _saveService;
        private readonly ILogService _log;

        public string Name => "save reset";
        public string Help => "Resets all saved game data";

        public SaveResetCommand(ISaveService saveService, ILogService log)
        {
            _saveService = saveService;
            _log = log;
        }

        public async UniTask ExecuteAsync(string[] args, CancellationToken ct = default)
        {
            await _saveService.ResetAsync(ct);
            _log.Info("[Console] Save data cleared");
        }
    }

    [ConsoleCommand("loc", "Usage: loc set <locale> — changes the active locale")]
    public sealed class LocaleSetCommand : IConsoleCommand
    {
        private readonly IL10nService _l10nService;
        private readonly ILogService _log;

        public string Name => "loc set";
        public string Help => "Sets the current locale";

        public LocaleSetCommand(IL10nService l10nService, ILogService log)
        {
            _l10nService = l10nService;
            _log = log;
        }

        public async UniTask ExecuteAsync(string[] args, CancellationToken ct = default)
        {
            if (args.Length == 0)
            {
                _log.Warn("[Console] Usage: loc set <locale>");
                return;
            }

            var locale = args[0];
            await _l10nService.SetLocaleAsync(locale, ct);
            _log.Info($"[Console] Locale changed to {locale}");
        }
    }

    [ConsoleCommand("version", "Usage: version check — checks the app version against remote config")]
    public sealed class VersionCheckCommand : IConsoleCommand
    {
        private readonly IVersionCheckService _versionCheckService;
        private readonly ILogService _log;

        public string Name => "version check";
        public string Help => "Checks app version against remote config";

        public VersionCheckCommand(IVersionCheckService versionCheckService, ILogService log)
        {
            _versionCheckService = versionCheckService;
            _log = log;
        }

        public async UniTask ExecuteAsync(string[] args, CancellationToken ct = default)
        {
            var result = await _versionCheckService.CheckAsync(ct);
            _log.Info($"[Console] Version check: {result.Status} (local={result.LocalVersion}, min={result.RemoteMinVersion})");
        }
    }

    [ConsoleCommand("fps", "Usage: fps show/hide — toggles the FPS overlay")]
    public sealed class FpsToggleCommand : IConsoleCommand
    {
        private readonly ILogService _log;

        public string Name => "fps";
        public string Help => "Shows or hides the FPS overlay";

        public FpsToggleCommand(ILogService log)
        {
            _log = log;
        }

        public UniTask ExecuteAsync(string[] args, CancellationToken ct = default)
        {
            if (args.Length == 0)
            {
                _log.Warn("[Console] Usage: fps show/hide");
                return UniTask.CompletedTask;
            }

            var cmd = args[0].ToLower();
            if (cmd == "show")
            {
                FpsOverlay.SetVisible(true);
                _log.Info("[Console] FPS overlay shown");
            }
            else if (cmd == "hide")
            {
                FpsOverlay.SetVisible(false);
                _log.Info("[Console] FPS overlay hidden");
            }
            else
            {
                _log.Warn("[Console] Usage: fps show/hide");
            }

            return UniTask.CompletedTask;
        }
    }
}
