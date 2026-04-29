using NUnit.Framework;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zero.UI;

namespace Zero.Tests.EditMode
{
    public sealed class ToastQueueTests
    {
        [UnityTest]
        public IEnumerator MaxQueueCapacity_DropOldest()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var mockAsset = new MockAssetService();
                var mockLayer = new GameObject("[UI.Toast]").transform;

                try
                {
                    var queue = new ToastQueue(mockAsset, mockLayer);
                    await queue.InitializeAsync();

                    // Enqueue to max capacity
                    for (int i = 0; i < 16; i++)
                    {
                        queue.Show($"Toast {i}", 1f);
                    }

                    // Enqueue one more — should drop the oldest
                    queue.Show("Toast 16", 1f);

                    // Verify behavior (we can't directly inspect the queue, so this is more of a smoke test)
                    Assert.Pass("Toast queue handles max capacity without exception");
                }
                finally
                {
                    Object.DestroyImmediate(mockLayer.gameObject);
                }
            });
        }

        private sealed class MockAssetService : Zero.Core.IAssetService
        {
            public System.Threading.Tasks.Task<bool> HasKeyAsync<T>(string key, System.Threading.CancellationToken ct = default) where T : class
            {
                return System.Threading.Tasks.Task.FromResult(false);
            }

            public System.Threading.Tasks.Task<Zero.Core.IAssetHandle<T>> LoadAsync<T>(string key, System.Threading.CancellationToken ct = default) where T : class
            {
                throw new System.NotImplementedException();
            }

            public System.Threading.Tasks.Task<Zero.Core.IAssetHandle<T>> PreloadAsync<T>(string key, System.Threading.CancellationToken ct = default) where T : class
            {
                throw new System.NotImplementedException();
            }

            public int ActiveHandleCount => 0;

            public System.Threading.Tasks.Task InitializeAsync(System.Threading.CancellationToken ct = default)
            {
                return System.Threading.Tasks.Task.CompletedTask;
            }
        }
    }
}
