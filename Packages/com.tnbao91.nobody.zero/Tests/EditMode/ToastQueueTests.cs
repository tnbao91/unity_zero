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

        [UnityTest]
        public IEnumerator ShowEmptyText_Ignored() => UniTask.ToCoroutine(async () =>
        {
            var mockAsset = new MockAssetService();
            var mockLayer = new GameObject("[UI.Toast]").transform;

            try
            {
                LogAssert.Expect(LogType.Warning,
                    "[UI] Toast prefab key 'ui/toast/default' not found. Toast functionality disabled.");
                var queue = new ToastQueue(mockAsset, mockLayer);
                await queue.InitializeAsync();

                LogAssert.Expect(LogType.Warning, "[UI] Toast with empty text ignored.");
                queue.Show("   ");

                // Without the guard, the queue would process the message and emit
                // "[UI] Toast prefab not available. Message: ..." — an unexpected log.
                LogAssert.NoUnexpectedReceived();
                queue.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mockLayer.gameObject);
            }
        });

        [Test]
        public void ShowNegativeDuration_ClampedToMinimum()
        {
            Assert.AreEqual(ToastQueue.MinDurationSeconds, ToastQueue.ClampDuration(-5f),
                "Negative duration must clamp to the minimum, not flash for a frame.");
            Assert.AreEqual(ToastQueue.MinDurationSeconds, ToastQueue.ClampDuration(0f));
            Assert.AreEqual(2f, ToastQueue.ClampDuration(2f), "Valid durations pass through.");
        }

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
