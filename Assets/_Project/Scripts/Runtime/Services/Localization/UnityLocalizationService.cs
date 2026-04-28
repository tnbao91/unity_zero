using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using Zero.Core;

namespace Zero.Services.Localization
{
    // Thin wrapper around com.unity.localization. Surfaces the Unity API behind
    // IL10nService so consumers in Zero.Core never depend on the package.
    public sealed class UnityLocalizationService : IL10nService, IDisposable
    {
        private readonly ILogService _log;
        private readonly TableReference _defaultTable;
        private readonly Subject<string> _onLocaleChanged = new();
        private readonly Action<Locale> _selectedLocaleHandler;
        private bool _disposed;

        private const string DefaultTableName = "Strings";

        public UnityLocalizationService(ILogService log)
        {
            _log = log;
            _defaultTable = DefaultTableName;

            _selectedLocaleHandler = locale =>
            {
                if (_disposed) return;
                _onLocaleChanged.OnNext(locale != null ? locale.Identifier.Code : string.Empty);
            };
            LocalizationSettings.SelectedLocaleChanged += _selectedLocaleHandler;
        }

        public Observable<string> OnLocaleChanged => _onLocaleChanged;

        public string CurrentLocale => LocalizationSettings.SelectedLocale != null
            ? LocalizationSettings.SelectedLocale.Identifier.Code
            : string.Empty;

        public string Get(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            try
            {
                return LocalizationSettings.StringDatabase.GetLocalizedString(_defaultTable, key, args);
            }
            catch (Exception ex)
            {
                _log?.Warn($"[L10n] GetLocalizedString failed for key='{key}': {ex.Message}");
                return key;
            }
        }

        public async UniTask SetLocaleAsync(string locale, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(locale)) return;
            await LocalizationSettings.InitializationOperation.ToUniTask(cancellationToken: ct);

            var available = LocalizationSettings.AvailableLocales;
            var target = available?.GetLocale(new LocaleIdentifier(locale));
            if (target == null)
            {
                _log?.Warn($"[L10n] Locale '{locale}' not available; ignoring SetLocaleAsync.");
                return;
            }
            LocalizationSettings.SelectedLocale = target;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            LocalizationSettings.SelectedLocaleChanged -= _selectedLocaleHandler;
            _onLocaleChanged.Dispose();
        }
    }
}
