namespace Zero.Core.Events
{
    public readonly record struct PopupOpened(string PopupKey);

    public readonly record struct PopupClosed(string PopupKey);

    public readonly record struct PopupBackdropTapped(string PopupKey);
}
