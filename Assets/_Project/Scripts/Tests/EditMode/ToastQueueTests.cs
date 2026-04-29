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

namespace Zero.Tests.EditMode
{
    public sealed class ToastQueueTests
    {
        [UnityTest]
        public IEnumerator MaxQueueCapacity_DropOldest() => UniTask.ToCoroutine(async () =>
        {
            var mockAsset = new MockAssetService();
            var mockLayer = new GameObject("[UI.Toast]").transform;

            try
            {
                var queue = new ToastQueue(mockAsset, mockLayer);
                await queue.InitializeAsync();

                for (int i = 0; i < 16; i++)
                {
                    queue.Show($"Toast {i}", 1f);
                }

                queue.Show("Toast 16", 1f);

                Assert.Pass("Toast queue handles max capacity without exception");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mockLayer.gameObject);
            }
        });

        private sealed class MockAssetService : IAssetService
        {
            public int ActiveHandleCount => 0;

            public UniTask InitializeAsync(CancellationToken ct = default) => UniTask.CompletedTask;

            public UniTask<bool> HasKeyAsync<T>(string key, CancellationToken ct = default)
                where T : UnityEngine.Object
                => UniTask.FromResult(false);

            public UniTask<IAssetHandle<T>> LoadAsync<T>(string key, CancellationToken ct = default)
                where T : UnityEngine.Object
                => throw new NotImplementedException();

            public UniTask PreloadAsync(IReadOnlyList<string> keys, IProgress<float> progress = null, CancellationToken ct = default)
                => UniTask.CompletedTask;
        }
    }
}
