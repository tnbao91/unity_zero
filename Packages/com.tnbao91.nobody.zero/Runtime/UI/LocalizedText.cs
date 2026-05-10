using System;
using R3;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using Zero.Core;

namespace Zero.UI
{
    /// <summary>
    /// MonoBehaviour that automatically updates TextMeshProUGUI text based on localization key.
    /// Subscribes to IL10nService.OnLocaleChanged and updates on every locale switch.
    /// Requires a TextMeshProUGUI component on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string _key;

        [Inject] private IL10nService _localizationService;

        private TextMeshProUGUI _text;
        private IDisposable _localeSubscription;

        private void OnEnable()
        {
            _text = GetComponent<TextMeshProUGUI>();
            if (_text == null)
            {
                Debug.LogError("[LocalizedText] TextMeshProUGUI component required.", gameObject);
                enabled = false;
                return;
            }

            // Immediately set the text
            RefreshText();

            // Subscribe to locale changes
            _localeSubscription = _localizationService.OnLocaleChanged.Subscribe(_ => RefreshText());
        }

        private void OnDisable()
        {
            _localeSubscription?.Dispose();
            _localeSubscription = null;
        }

        private void RefreshText()
        {
            if (string.IsNullOrEmpty(_key))
            {
                _text.text = "";
                return;
            }

            try
            {
                _text.text = _localizationService.Get(_key);
            }
            catch
            {
                // Fallback to the key itself if localization fails
                _text.text = _key;
            }
        }
    }
}
