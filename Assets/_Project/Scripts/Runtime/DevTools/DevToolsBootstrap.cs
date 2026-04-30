using UnityEngine;

namespace Zero.DevTools
{
    public static class DevToolsBootstrap
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            var root = new GameObject("[Zero.DevTools]");
            root.AddComponent<CheatConsole>();
            root.AddComponent<FpsOverlay>();
            Object.DontDestroyOnLoad(root);
        }
#endif
    }
}
