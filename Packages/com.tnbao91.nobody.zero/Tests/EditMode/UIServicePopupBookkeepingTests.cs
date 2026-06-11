using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.TestTools;
using Zero.Core;
using Zero.Core.Events;
using Zero.Services.Events;
using Zero.UI;

namespace Zero.Tests.EditMode
{
    // Phase 6 spec: a cancelled PushAsync must remove ITS OWN bookkeeping entry.
    // The old Stack.Pop() evicted whichever popup was on top — with interleaved
    // pushes, cancelling the first evicted the second's entry, so the next
    // PopAsync closed (and published PopupClosed for) the wrong popup.
    [TestFixture]
    public sealed class UIServicePopupBookkeepingTests
    {
        [UnityTest]
        public IEnumerator InterleavedPushCancel_RemovesOwnEntryOnly() => UniTask.ToCoroutine(async () =>
        {
            var roots = new GameObject("[UI.Roots]");
            var popupLayer = new GameObject("[UI.Popup]").transform;
            popupLayer.SetParent(roots.transform, false);

            var prefabA = new GameObject("HangingPopup-Prefab");
            prefabA.AddComponent<HangingPopup>();
            var prefabB = new GameObject("InstantPopup-Prefab");
            prefabB.AddComponent<InstantPopup>();

            using var bus = new R3EventBus();
            var closedKeys = new List<string>();
            using var sub = bus.On<PopupClosed>().Subscribe(e => closedKeys.Add(e.PopupKey));

            var assets = new StubAssetService(new Dictionary<string, GameObject>
            {
                ["ui/popup/hangingpopup"] = prefabA,
                ["ui/popup/instantpopup"] = prefabB,
            });
            var ui = new UIService(assets, bus, new StubLogService());

            try
            {
                ui.AttachRoot(new Dictionary<UiLayer, Transform> { [UiLayer.Popup] = popupLayer });

                // A suspends inside OnOpenAsync; B opens fully on top of it.
                using var ctsA = new CancellationTokenSource();
                var taskA = ui.PushAsync<HangingPopup, int, int>(0, ct: ctsA.Token);
                var taskB = ui.PushAsync<InstantPopup, int, int>(0);

                ctsA.Cancel();
                try
                {
                    await taskA;
                    Assert.Fail("Cancelled push must propagate OperationCanceledException.");
                }
                catch (OperationCanceledException) { /* expected */ }

                // The next PopAsync must close B — the only popup still open.
                await ui.PopAsync();

                Assert.AreEqual(new[] { "instantpopup" }, closedKeys,
                    "Cancelling A must not evict B's bookkeeping entry.");

                try
                {
                    await taskB;
                    Assert.Fail("Popped push must complete via cancellation.");
                }
                catch (OperationCanceledException) { /* expected */ }
            }
            finally
            {
                ui.Dispose();
                UnityEngine.Object.DestroyImmediate(roots);
                UnityEngine.Object.DestroyImmediate(prefabA);
                UnityEngine.Object.DestroyImmediate(prefabB);
            }
        });

        // Overrides skip the base transition implementations — UITransitions
        // tween via LitMotion, which needs a player loop EditMode doesn't run.
        private sealed class HangingPopup : PopupBase<int, int>
        {
            public override UniTask OnOpenAsync(int data, CancellationToken ct) => UniTask.Never(ct);
            public override UniTask OnCloseAsync(int result, CancellationToken ct) => UniTask.CompletedTask;
        }

        private sealed class InstantPopup : PopupBase<int, int>
        {
            public override UniTask OnOpenAsync(int data, CancellationToken ct) => UniTask.CompletedTask;
            public override UniTask OnCloseAsync(int result, CancellationToken ct) => UniTask.CompletedTask;
        }

        private sealed class StubAssetService : IAssetService
        {
            private readonly Dictionary<string, GameObject> _prefabs;
            public StubAssetService(Dictionary<string, GameObject> prefabs) => _prefabs = prefabs;

            public int ActiveHandleCount => 0;
            public UniTask InitializeAsync(CancellationToken ct = default) => UniTask.CompletedTask;

            public UniTask<bool> HasKeyAsync<T>(string key, CancellationToken ct = default)
                where T : UnityEngine.Object
                => UniTask.FromResult(_prefabs.ContainsKey(key));

            public UniTask<IAssetHandle<T>> LoadAsync<T>(string key, CancellationToken ct = default)
                where T : UnityEngine.Object
                => UniTask.FromResult<IAssetHandle<T>>(new StubHandle<T>((T)(UnityEngine.Object)_prefabs[key]));

            public UniTask PreloadAsync(IReadOnlyList<string> keys, IProgress<float> progress = null, CancellationToken ct = default)
                => UniTask.CompletedTask;

            private sealed class StubHandle<T> : IAssetHandle<T> where T : UnityEngine.Object
            {
                public StubHandle(T asset) => Asset = asset;
                public T Asset { get; }
                public bool IsLoaded => true;
                public void Dispose() { }
            }
        }

        private sealed class StubLogService : ILogService
        {
            public bool IsEnabled { get; set; } = true;
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) { }
            public void Error(Exception exception, string context = null) { }
        }
    }
}
