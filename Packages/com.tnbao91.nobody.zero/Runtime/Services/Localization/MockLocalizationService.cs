using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using Zero.Core;

namespace Zero.Services.Localization
{
    // Dictionary-backed Mock for EditMode tests + headless runs. No Unity.Localization
    // dependency is exercised; locale switching emits OnLocaleChanged synchronously.
    public sealed class MockLocalizationService : IL10nService, IDisposable
    {
        private readonly Dictionary<string, Dictionary<string, string>> _tables;
        private readonly Subject<string> _onLocaleChanged = new();
        private string _currentLocale;
        private bool _disposed;

        public MockLocalizationService(string initialLocale = "en")
            : this(new Dictionary<string, Dictionary<string, string>>(), initialLocale) { }

        public MockLocalizationService(Dictionary<string, Dictionary<string, string>> tables, string initialLocale = "en")
        {
            _tables = tables ?? new Dictionary<string, Dictionary<string, string>>();
            _currentLocale = initialLocale ?? "en";
        }

        public Observable<string> OnLocaleChanged => _onLocaleChanged;
        public string CurrentLocale => _currentLocale;

        public string Get(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (_tables.TryGetValue(_currentLocale, out var table) && table.TryGetValue(key, out var raw))
            {
                if (args == null || args.Length == 0) return raw;
                try { return string.Format(CultureInfo.InvariantCulture, raw, args); }
                catch { return raw; }
            }
            return key;
        }

        public UniTask SetLocaleAsync(string locale, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(locale) || locale == _currentLocale) return UniTask.CompletedTask;
            _currentLocale = locale;
            _onLocaleChanged.OnNext(locale);
            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _onLocaleChanged.Dispose();
        }
    }
}
