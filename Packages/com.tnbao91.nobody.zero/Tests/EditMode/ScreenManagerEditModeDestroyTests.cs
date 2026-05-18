using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Zero.Core;
using Zero.UI;
using Object = UnityEngine.Object;

namespace Zero.Tests.EditMode
{
    /// <summary>
    /// Regression guard for the EditMode-unsafe <c>Object.Destroy</c> pitfall.
    /// <c>ScreenManager.ShowAsync</c> calls <c>UnloadScreenAsync</c> on the second show,
    /// which destroys the previous screen instance. Before the <c>Util.SafeDestroy</c>
    /// fix that path called <c>Object.Destroy</c> directly and threw
    /// "Destroy may not be called from edit mode" here. This test fails iff that returns.
    /// </summary>
    public sealed class ScreenManagerEditModeDestroyTests
    {
        private sealed class ScreenA { }
        private sealed class ScreenB { }

        private sealed class StubHandle : IAssetHandle<GameObject>
        {
            private readonly GameObject _asset;
            public StubHandle(GameObject asset) => _asset = asset;
            public GameObject Asset => _asset;
            public bool IsLoaded => true;
            public void Dispose() { }
        }

        private sealed class StubAssetService : IAssetService
        {
            private readonly GameObject _prefab;
            public StubAssetService(GameObject prefab) => _prefab = prefab;

            public int ActiveHandleCount => 0;
            public UniTask InitializeAsync(CancellationToken ct = default) => UniTask.CompletedTask;

            public UniTask<IAssetHandle<T>> LoadAsync<T>(string key, CancellationToken ct = default)
                where T : Object
                => UniTask.FromResult<IAssetHandle<T>>((IAssetHandle<T>)(object)new StubHandle(_prefab));

            public UniTask<bool> HasKeyAsync<T>(string key, CancellationToken ct = default)
                where T : Object
                => UniTask.FromResult(true);

            public UniTask PreloadAsync(IReadOnlyList<string> keys, IProgress<float> progress = null,
                CancellationToken ct = default)
                => UniTask.CompletedTask;
        }

        [UnityTest]
        public IEnumerator SecondShow_DestroysPreviousScreen_InEditMode_DoesNotThrow()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var prefab = new GameObject("ScreenPrefab");
                var layer = new GameObject("ScreenLayer").transform;
                var sm = new ScreenManager(new StubAssetService(prefab), layer);

                try
                {
                    await sm.ShowAsync<ScreenA, object>(null);
                    Assert.AreEqual(1, layer.childCount,
                        "First screen should be instantiated under the layer.");

                    // Second show -> UnloadScreenAsync -> SafeDestroy on the first instance.
                    // Pre-fix this threw "Destroy may not be called from edit mode".
                    await sm.ShowAsync<ScreenB, object>(null);
                    Assert.AreEqual(1, layer.childCount,
                        "Previous screen destroyed; only the new instance remains.");
                }
                finally
                {
                    sm.Dispose();
                    Object.DestroyImmediate(layer.gameObject);
                    Object.DestroyImmediate(prefab);
                }
            });
        }
    }
}
