using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UnityEngine.TestTools;
using Zero.Bootstrap;
using Zero.Bootstrap.Steps;
using Zero.Core;
using Zero.Core.Events;
using Zero.Infrastructure;
using Zero.Services.Events;
using Zero.Services.Localization;

namespace Zero.Tests.EditMode
{
    /// <summary>
    /// The 0.7.0 contract: the player waits for the blocking phase and nothing else.
    ///
    /// 0.6.0 stopped bootstrap from aborting onto a dead splash screen, but left all 16
    /// steps on the critical path — 30s timeouts × 2 attempts × 16 steps is a 940-second
    /// worst case for a game that should boot in about a second. These tests pin the split:
    /// a tiny blocking prefix bounded by a budget, then everything else in the background
    /// while the player is already playing.
    /// </summary>
    [TestFixture]
    public sealed class BootstrapPhaseTests
    {
        private ILogService _log;
        private IBootstrapProgressReporter _reporter;
        private IBootstrapReport _report;

        [SetUp]
        public void SetUp()
        {
            _log = new StubLogService();
            _reporter = new BootstrapProgressReporter();
            _report = new BootstrapReport();
        }

        // ---- The headline guarantee ----

        [UnityTest]
        public IEnumerator BootstrapReady_FiresAfterBlockingPhase_BeforeAnyDeferredStepRuns() =>
            UniTask.ToCoroutine(async () =>
        {
            using var bus = new R3EventBus();
            var timeline = new List<string>();
            using var sub = bus.On<BootstrapReady>().Subscribe(_ => timeline.Add("READY"));

            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                new PhasedStep("Blocking1", BootstrapPhase.Blocking, timeline),
                new PhasedStep("Deferred1", BootstrapPhase.Deferred, timeline),
                new PhasedStep("Blocking2", BootstrapPhase.Blocking, timeline),
                new PhasedStep("Deferred2", BootstrapPhase.Deferred, timeline),
            }, _log, _reporter, bus, _report);

            await pipeline.RunAsync(null, CancellationToken.None);

            // RunAsync returns when the blocking phase is done — the player can go now.
            CollectionAssert.AreEqual(new[] { "Blocking1", "Blocking2", "READY" }, timeline,
                "Blocking steps run in declared order, then READY. No deferred step may run first.");

            await pipeline.DeferredCompletion;

            CollectionAssert.AreEqual(
                new[] { "Blocking1", "Blocking2", "READY", "Deferred1", "Deferred2" }, timeline,
                "Deferred steps run after READY, still in declared order.");
        });

        [UnityTest]
        public IEnumerator DeferredSteps_KeepDeclaredOrder_SoRemoteConfigStillPrecedesVersionCheck() =>
            UniTask.ToCoroutine(async () =>
        {
            // The one ordering edge that lives entirely inside the deferred set. Running the
            // deferred phase sequentially preserves it for free — no dependency API needed.
            var timeline = new List<string>();
            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                new PhasedStep("RemoteConfig", BootstrapPhase.Deferred, timeline),
                new PhasedStep("VersionCheck", BootstrapPhase.Deferred, timeline),
            }, _log, _reporter, null, _report);

            await pipeline.RunAsync(null, CancellationToken.None);
            await pipeline.DeferredCompletion;

            CollectionAssert.AreEqual(new[] { "RemoteConfig", "VersionCheck" }, timeline);
        });

        [UnityTest]
        public IEnumerator BlockingStepFailure_StillPublishesReady_AndStillDegrades() =>
            UniTask.ToCoroutine(async () =>
        {
            using var bus = new R3EventBus();
            var ready = 0;
            var degraded = new List<BootstrapStepDegraded>();
            using var s1 = bus.On<BootstrapReady>().Subscribe(_ => ready++);
            using var s2 = bus.On<BootstrapStepDegraded>().Subscribe(degraded.Add);

            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                new ThrowingPhasedStep("BrokenBlocking", BootstrapPhase.Blocking),
            }, _log, _reporter, bus, _report);

            await pipeline.RunAsync(null, CancellationToken.None);

            Assert.AreEqual(1, ready, "0.6.0's guarantee still holds: a failed step never denies entry.");
            Assert.AreEqual(1, degraded.Count);
            Assert.IsTrue(_report.IsDegraded("BrokenBlocking"));
        });

        [UnityTest]
        public IEnumerator DeferredStepFailure_IsStillRecordedAndAnnounced() =>
            UniTask.ToCoroutine(async () =>
        {
            using var bus = new R3EventBus();
            var degraded = new List<BootstrapStepDegraded>();
            using var sub = bus.On<BootstrapStepDegraded>().Subscribe(degraded.Add);

            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                new ThrowingPhasedStep("BrokenDeferred", BootstrapPhase.Deferred),
            }, _log, _reporter, bus, _report);

            await pipeline.RunAsync(null, CancellationToken.None);
            await pipeline.DeferredCompletion;

            Assert.AreEqual(1, degraded.Count, "Moving a step off the boot path must not silence it.");
            Assert.AreEqual("BrokenDeferred", degraded[0].StepName);
            Assert.IsTrue(_report.IsDegraded("BrokenDeferred"));
        });

        // ---- The budget: the actual ceiling on time-to-first-screen ----

        [UnityTest]
        public IEnumerator BlockingBudgetExpiry_SkipsRemainingBlockingSteps_AndStillPublishesReady() =>
            UniTask.ToCoroutine(async () =>
        {
            using var bus = new R3EventBus();
            var ready = 0;
            var timeline = new List<string>();
            using var sub = bus.On<BootstrapReady>().Subscribe(_ => ready++);

            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                new HangingPhasedStep("Slow", BootstrapPhase.Blocking),
                new PhasedStep("NeverReached", BootstrapPhase.Blocking, timeline),
                new PhasedStep("StillRuns", BootstrapPhase.Deferred, timeline),
            }, _log, _reporter, bus, _report,
               blockingBudget: TimeSpan.FromMilliseconds(120));

            await pipeline.RunAsync(null, CancellationToken.None);

            Assert.AreEqual(1, ready, "Budget expiry must not strand the player on the splash screen.");
            CollectionAssert.DoesNotContain(timeline, "NeverReached",
                "Once the budget is gone, remaining blocking steps are skipped, not waited for.");
            Assert.IsTrue(_report.IsDegraded("Slow"));
            Assert.IsTrue(_report.IsDegraded("NeverReached"),
                "A skipped blocking step is degraded, not silently dropped.");

            await pipeline.DeferredCompletion;
            CollectionAssert.Contains(timeline, "StillRuns",
                "The deferred phase is not governed by the blocking budget.");
        });

        [UnityTest]
        public IEnumerator BootstrapReady_CarriesTheBlockingDuration() => UniTask.ToCoroutine(async () =>
        {
            using var bus = new R3EventBus();
            BootstrapReady? observed = null;
            using var sub = bus.On<BootstrapReady>().Subscribe(e => observed = e);

            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                new PhasedStep("Fast", BootstrapPhase.Blocking, null),
            }, _log, _reporter, bus, _report);

            await pipeline.RunAsync(null, CancellationToken.None);

            Assert.IsTrue(observed.HasValue);
            Assert.GreaterOrEqual(observed.Value.BlockingMs, 0d);
            Assert.Less(observed.Value.BlockingMs, 5000d, "A no-op blocking phase must not take seconds.");
        });

        // ---- Classification: pin the boot budget so it cannot silently regrow ----

        [Test]
        public void OnlySaveAndAsset_BlockThePlayer_EverythingElseIsDeferred()
        {
            // Null services are safe: Phase is a constant per type and touches no dependency.
            var shipped = new IBootstrapStep[]
            {
                new CrashlyticsStep(null),
                new LogStep(null),
                new DeviceProfileStep(null, null),
                new SaveStep(null),
                new AssetStep(null),
                new ConsentStep(null),
                new RemoteConfigStep(null),
                new AnalyticsStep(null),
                new LocalizationStep(null, null),
                new AttributionStep(null),
                new AdsStep(null, null),
                new IapStep(null),
                new AudioStep(null),
                new TimeStep(null),
                new NotificationStep(null),
                new VersionCheckStep(null),
            };

            Assert.AreEqual(16, shipped.Length, "Step count changed — update this test and docs/guide.html.");

            var blocking = new List<string>();
            foreach (var step in shipped)
            {
                if (step.Phase == BootstrapPhase.Blocking) blocking.Add(step.Name);
            }

            // Log is a no-op and DeviceProfile is synchronous; both cost nothing and are
            // wanted before the first frame (targetFrameRate in particular).
            CollectionAssert.AreEquivalent(
                new[] { "Log", "DeviceProfile", "Save", "Asset" }, blocking,
                "Anything added to the blocking phase is time the player spends on a splash " +
                "screen. Adding one is a deliberate decision, not a default.");
        }

        [Test]
        public void ConsentStep_HasNoTimeout_BecauseRealConsentPutsModalUiInFrontOfAHuman()
        {
            // A real UMP/ATT implementation shows a system dialog. A step timeout would
            // cancel it while the player is mid-read. Zero means "no deadline" to the pipeline.
            Assert.AreEqual(TimeSpan.Zero, new ConsentStep(null).Timeout);
        }

        [Test]
        public void DefaultPhase_IsBlocking_SoAnExistingConsumerStepKeepsItsSemantics()
        {
            Assert.AreEqual(BootstrapPhase.Blocking, new PhasedStep("X", BootstrapPhase.Blocking, null).Phase);
            Assert.AreEqual(BootstrapPhase.Blocking, new DefaultPhaseStep().Phase,
                "A consumer step that does not mention Phase must not silently slide off the boot path.");
        }

        [Test]
        public void DefaultStepTimeout_IsShortEnoughForAMobileBootPath()
        {
            Assert.LessOrEqual(new DefaultPhaseStep().Timeout, TimeSpan.FromSeconds(10),
                "30s per step was the old default; 16 of those is a 15-minute worst case.");
        }

        // ---- Stub steps ----

        private class PhasedStep : BootstrapStepBase
        {
            private readonly string _name;
            private readonly BootstrapPhase _phase;
            private readonly List<string> _timeline;
            public override string Name => _name;
            public override BootstrapPhase Phase => _phase;
            public PhasedStep(string name, BootstrapPhase phase, List<string> timeline)
            { _name = name; _phase = phase; _timeline = timeline; }
            protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
            {
                _timeline?.Add(_name);
                return UniTask.CompletedTask;
            }
        }

        private sealed class DefaultPhaseStep : BootstrapStepBase
        {
            public override string Name => "Default";
            protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
                => UniTask.CompletedTask;
        }

        private sealed class ThrowingPhasedStep : BootstrapStepBase
        {
            private readonly string _name;
            private readonly BootstrapPhase _phase;
            public override string Name => _name;
            public override BootstrapPhase Phase => _phase;
            public override int MaxRetries => 0;
            public ThrowingPhasedStep(string name, BootstrapPhase phase) { _name = name; _phase = phase; }
            protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
                => throw new InvalidOperationException($"{_name} boom");
        }

        private sealed class HangingPhasedStep : BootstrapStepBase
        {
            private readonly string _name;
            private readonly BootstrapPhase _phase;
            public override string Name => _name;
            public override BootstrapPhase Phase => _phase;
            public override int MaxRetries => 0;
            public override TimeSpan Timeout => TimeSpan.FromSeconds(30); // budget must win, not this
            public HangingPhasedStep(string name, BootstrapPhase phase) { _name = name; _phase = phase; }
            protected override async UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
                => await UniTask.Never(ct);
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
