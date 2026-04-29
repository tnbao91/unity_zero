using System;
using R3;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zero.Core;

namespace Zero.UI
{
    /// <summary>
    /// Component for displaying bootstrap progress.
    /// Attach to a GameObject with Slider and TextMeshProUGUI components.
    /// Reads from IBootstrapProgressReporter (never resolves BootstrapPipeline directly).
    /// </summary>
    public sealed class LoadingScreenView : MonoBehaviour
    {
        [SerializeField] private Slider _progressSlider;
        [SerializeField] private TextMeshProUGUI _stepNameText;

        [Inject] private IBootstrapProgressReporter _reporter;

        private IDisposable _progressSubscription;
        private IDisposable _stepNameSubscription;

        private void Start()
        {
            if (_progressSlider != null)
            {
                _progressSlider.value = 0f;
                _progressSubscription = _reporter.Progress.Subscribe(
                    progress =>
                    {
                        if (_progressSlider != null)
                        {
                            _progressSlider.value = progress;
                        }
                    });
            }

            if (_stepNameText != null)
            {
                _stepNameText.text = "";
                _stepNameSubscription = _reporter.CurrentStepName.Subscribe(
                    stepName =>
                    {
                        if (_stepNameText != null)
                        {
                            _stepNameText.text = stepName ?? "";
                        }
                    });
            }
        }

        private void OnDestroy()
        {
            _progressSubscription?.Dispose();
            _stepNameSubscription?.Dispose();
        }
    }
}
