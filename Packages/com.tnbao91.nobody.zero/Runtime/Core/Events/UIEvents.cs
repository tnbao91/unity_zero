namespace Zero.Core.Events
{
    public readonly struct PopupOpened
    {
        public readonly string PopupKey;
        public PopupOpened(string popupKey) => PopupKey = popupKey;
    }

    public readonly struct PopupClosed
    {
        public readonly string PopupKey;
        public PopupClosed(string popupKey) => PopupKey = popupKey;
    }

    public readonly struct PopupBackdropTapped
    {
        public readonly string PopupKey;
        public PopupBackdropTapped(string popupKey) => PopupKey = popupKey;
    }
}
