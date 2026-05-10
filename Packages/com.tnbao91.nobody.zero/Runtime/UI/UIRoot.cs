using System.Collections.Generic;
using Reflex.Attributes;
using UnityEngine;
using Zero.Core;

namespace Zero.UI
{
    /// <summary>
    /// Consumer attaches this MonoBehaviour to a UI hierarchy in their scene
    /// (e.g., the Loading or Home scene root) and assigns the four layer
    /// Transforms in the inspector. On enable, the configured layers are
    /// pushed into IUIService; on disable, they're detached so subsequent
    /// scenes can register their own root.
    ///
    /// Layer Transforms typically live under a Canvas (one Canvas per layer
    /// or one root Canvas with sub-RectTransforms — both work). UIService
    /// parents popups/screens/toasts under these.
    /// </summary>
    public sealed class UIRoot : MonoBehaviour
    {
        [SerializeField] private Transform _hudLayer;
        [SerializeField] private Transform _popupLayer;
        [SerializeField] private Transform _overlayLayer;
        [SerializeField] private Transform _systemLayer;

        [Inject] private IUIService _uiService;

        private void OnEnable()
        {
            if (_uiService == null)
            {
                Debug.LogError("[UIRoot] IUIService not injected. Reflex scope must be configured for the active scene.", this);
                return;
            }

            var layers = new Dictionary<UiLayer, Transform>(4);
            if (_hudLayer != null) layers[UiLayer.Hud] = _hudLayer;
            if (_popupLayer != null) layers[UiLayer.Popup] = _popupLayer;
            if (_overlayLayer != null) layers[UiLayer.Overlay] = _overlayLayer;
            if (_systemLayer != null) layers[UiLayer.System] = _systemLayer;

            if (layers.Count == 0)
            {
                Debug.LogWarning("[UIRoot] No layer Transforms assigned in the inspector.", this);
                return;
            }

            _uiService.AttachRoot(layers);
        }

        private void OnDisable()
        {
            _uiService?.DetachRoot();
        }
    }
}
