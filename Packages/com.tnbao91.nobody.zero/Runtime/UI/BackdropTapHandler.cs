using UnityEngine;
using UnityEngine.EventSystems;
using Zero.Core;
using Zero.Core.Events;

namespace Zero.UI
{
    /// <summary>
    /// Handles tap/click events on popup backdrops and publishes PopupBackdropTapped events.
    /// </summary>
    internal sealed class BackdropTapHandler : MonoBehaviour, IPointerClickHandler
    {
        private string _popupKey;
        private IEventBus _eventBus;

        public void Initialize(string popupKey, IEventBus eventBus)
        {
            _popupKey = popupKey;
            _eventBus = eventBus;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_eventBus != null)
            {
                _eventBus.Publish(new PopupBackdropTapped(_popupKey));
            }
        }
    }
}
