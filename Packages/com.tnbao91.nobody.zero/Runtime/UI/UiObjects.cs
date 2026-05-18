using UnityEngine;
using Object = UnityEngine.Object;

namespace Zero.UI
{
    /// <summary>
    /// UI-internal GameObject helpers. <see cref="SafeDestroy"/> mirrors
    /// <c>UnityPoolService.SafeDestroy</c> — <see cref="Object.Destroy(Object)"/> is play-mode
    /// only and throws in EditMode tests, so destruction must route through this guard.
    /// Duplicated here (not referenced from <c>Zero.Services.Pool</c>) because <c>Zero.UI</c>
    /// must not depend on a service assembly.
    /// </summary>
    internal static class UiObjects
    {
        internal static void SafeDestroy(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }
    }
}
