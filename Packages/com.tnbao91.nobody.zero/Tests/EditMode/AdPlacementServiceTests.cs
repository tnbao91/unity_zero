using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UnityEngine.TestTools;
using Zero.Core;
using Zero.Services.AdPlacement;

namespace Zero.Tests.EditMode
{
    /// <summary>
    /// Boundary guards + gating policy of DefaultAdPlacementService. The null/empty-id
    /// tests anchor the contract documented in docs/services/adplacement.md ("fail-safe
    /// queries, explicit results"): CanShow/TryShowAsync/NotifyShown fail safe (false /
    /// Failed result / no-op — NotifyShown deliberately mirrors its silent-ignore of
    /// unknown ids), while RegisterPlacement, the write, throws.
    /// </summary>
    public class AdPlacementServiceTests
    {
        private FakeAdsService _ads;
        private DefaultAdPlacementService _service;

        [SetUp]
        public void Setup()
        {
            _ads = new FakeAdsService();
            _service = new DefaultAdPlacementService(_ads, new MockLogService());
        }

        [Test]
        public void CanShow_NullPlacementId_ReturnsFalse()
        {
            Assert.IsFalse(_service.CanShow(null));
        }

        [Test]
        public void CanShow_EmptyPlacementId_ReturnsFalse()
        {
            Assert.IsFalse(_service.CanShow(""));
        }

        [Test]
        public void CanShow_UnknownPlacementId_ReturnsFalse()
        {
            Assert.IsFalse(_service.CanShow("never_registered"));
        }

        [Test]
        public void CanShow_AdsNotReady_ReturnsFalse()
        {
            _service.RegisterPlacement("inter", AdType.Interstitial, TimeSpan.Zero, sessionCap: 5);
            _ads.Ready = false;

            Assert.IsFalse(_service.CanShow("inter"));
        }

        [Test]
        public void RegisterPlacement_NullId_ThrowsWithCallerParamName()
        {
            var ex = Assert.Throws<ArgumentNullException>(() =>
                _service.RegisterPlacement(null, AdType.Interstitial, TimeSpan.Zero, sessionCap: 1));

            // Must blame the caller's argument, not leak the Dictionary's internal "key".
            Assert.AreEqual("placementId", ex.ParamName);
        }

        [Test]
        public void RegisterPlacement_EmptyId_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                _service.RegisterPlacement("", AdType.Interstitial, TimeSpan.Zero, sessionCap: 1));
        }

        [TestCase(0)]
        [TestCase(-3)]
        public void RegisterPlacement_NonPositiveSessionCap_Throws(int sessionCap)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _service.RegisterPlacement("inter", AdType.Interstitial, TimeSpan.Zero, sessionCap));
        }

        [Test]
        public void RegisterPlacement_NegativeCooldown_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _service.RegisterPlacement("inter", AdType.Interstitial, TimeSpan.FromSeconds(-1), sessionCap: 1));
        }

        [Test]
        public void RegisterPlacement_SameId_ResetsCounters()
        {
            _service.RegisterPlacement("inter", AdType.Interstitial, TimeSpan.Zero, sessionCap: 1);
            _service.NotifyShown("inter");
            Assert.IsFalse(_service.CanShow("inter"), "capped after NotifyShown");

            _service.RegisterPlacement("inter", AdType.Interstitial, TimeSpan.Zero, sessionCap: 1);

            Assert.IsTrue(_service.CanShow("inter"), "re-registering resets counters (documented behavior)");
        }

        [TestCase(null)]
        [TestCase("")]
        public void NotifyShown_NullOrEmptyPlacementId_IsNoOp(string placementId)
        {
            _service.RegisterPlacement("inter", AdType.Interstitial, TimeSpan.Zero, sessionCap: 1);

            _service.NotifyShown(placementId);

            Assert.IsTrue(_service.CanShow("inter"), "invalid id must not throw nor touch registered placements");
        }

        [Test]
        public void NotifyShown_RegisteredPlacement_CountsTowardCap()
        {
            _service.RegisterPlacement("inter", AdType.Interstitial, TimeSpan.Zero, sessionCap: 1);

            _service.NotifyShown("inter");

            Assert.IsFalse(_service.CanShow("inter"));
        }

        [UnityTest]
        public IEnumerator TryShowAsync_NullOrEmptyPlacementId_ReturnsFailedResult() => UniTask.ToCoroutine(async () =>
        {
            var forNull = await _service.TryShowAsync(null);
            var forEmpty = await _service.TryShowAsync("");

            Assert.AreEqual(AdResult.Failed, forNull.Result);
            Assert.AreEqual(AdResult.Failed, forEmpty.Result);
            StringAssert.Contains("invalid", forNull.ErrorMessage);
            StringAssert.Contains("invalid", forEmpty.ErrorMessage);
            Assert.AreEqual(0, _ads.ShowCalls);
        });

        [UnityTest]
        public IEnumerator TryShowAsync_UnknownPlacement_ReturnsFailedResult() => UniTask.ToCoroutine(async () =>
        {
            var result = await _service.TryShowAsync("never_registered");

            Assert.AreEqual(AdResult.Failed, result.Result);
            StringAssert.Contains("unknown placement", result.ErrorMessage);
            Assert.AreEqual(0, _ads.ShowCalls);
        });

        [UnityTest]
        public IEnumerator TryShowAsync_SessionCapReached_ReturnsNotReadyWithoutCallingNetwork() => UniTask.ToCoroutine(async () =>
        {
            _service.RegisterPlacement("inter", AdType.Interstitial, TimeSpan.Zero, sessionCap: 1);

            var first = await _service.TryShowAsync("inter");
            var second = await _service.TryShowAsync("inter");

            Assert.AreEqual(AdResult.Shown, first.Result);
            Assert.AreEqual(AdResult.NotReady, second.Result);
            Assert.AreEqual(1, _ads.ShowCalls, "capped placement must not reach IAdsService.ShowAsync");
        });

        [UnityTest]
        public IEnumerator TryShowAsync_ZeroCooldown_AllowsImmediateReshow() => UniTask.ToCoroutine(async () =>
        {
            _service.RegisterPlacement("inter", AdType.Interstitial, TimeSpan.Zero, sessionCap: 5);

            var first = await _service.TryShowAsync("inter");
            var second = await _service.TryShowAsync("inter");

            Assert.AreEqual(AdResult.Shown, first.Result);
            Assert.AreEqual(AdResult.Shown, second.Result);
            Assert.AreEqual(2, _ads.ShowCalls);
        });

        private sealed class FakeAdsService : IAdsService
        {
            public bool Ready = true;
            public int ShowCalls;

            public UniTask InitializeAsync(CancellationToken ct = default) => UniTask.CompletedTask;

            public bool IsReady(AdType type) => Ready;

            public UniTask<AdShowResult> ShowAsync(AdType type, string placementId, CancellationToken ct = default)
            {
                ShowCalls++;
                return UniTask.FromResult(new AdShowResult(type, AdResult.Shown, placementId));
            }

            public UniTask LoadAsync(AdType type, CancellationToken ct = default) => UniTask.CompletedTask;

            public Observable<AdShowResult> OnAdEvent => Observable.Empty<AdShowResult>();
        }

        private sealed class MockLogService : ILogService
        {
            public bool IsEnabled { get; set; } = true;

            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) { }
            public void Error(Exception exception, string context = null) { }
        }
    }
}
