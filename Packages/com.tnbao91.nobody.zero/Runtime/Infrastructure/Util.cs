using UnityEngine;
using Object = UnityEngine.Object;

namespace Zero.Infrastructure
{
    /// <summary>
    /// Cross-cutting static helpers shared by assemblies that may not reference each
    /// other (e.g. <c>Zero.UI</c> and <c>Zero.Services.Pool</c>). Both already reference
    /// <c>Zero.Infrastructure</c>, so shared utilities live here once instead of being
    /// duplicated per assembly. Add further unrelated helpers (non-GameObject) here too.
    /// </summary>
    public static class Util
    {
        /// <summary>
        /// <see cref="Object.Destroy(Object)"/> is play-mode only and throws in EditMode
        /// tests / editor scripts. Route GameObject destruction through this guard: the
        /// runtime path stays unchanged while EditMode falls back to
        /// <see cref="Object.DestroyImmediate(Object)"/>.
        /// </summary>
        public static void SafeDestroy(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }
    }
}
