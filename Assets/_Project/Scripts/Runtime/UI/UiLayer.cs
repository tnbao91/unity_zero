using UnityEngine;

namespace Zero.UI
{
    /// <summary>
    /// Layer definition with associated sort orders for canvas grouping.
    /// </summary>
    public class UiLayer
    {
        public const int HudSortOrder = 100;
        public const int PopupSortOrder = 200;
        public const int OverlaySortOrder = 300;
        public const int SystemSortOrder = 400;
    }
}
