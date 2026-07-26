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
    /// The template's contract: bootstrap never denies the player the game.
    ///
    /// Every shipped step is non-critical, so any of them can fail and the pipeline
    /// still reaches the end. The price of that is that a failure must not vanish —
    /// these tests pin both halves: the pipeline keeps going, AND the failure is
    /// observable both as a bus event (at the moment) and via IBootstrapReport
    /// (afterwards, for anything constructed too late to hear the event).
    /// </summary>
    [TestFixture]
    public sealed class BootstrapDegradationTests
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

        [Test]
        public void EveryShippedStep_IsNonCritical_SoBootstrapNeverBlocksEntry()
        {
            // Steps are constructed with null services on purpose: IsCritical is a
            // constant per type and touches no dependency. If that ever stops being
            // true this test will NRE, which is itself the signal to look.
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

            var critical = new List<string>();
            foreach (var step in shipped)
            {
                if (step.IsCritical) critical.Add(step.Name);
            }

            CollectionAssert.IsEmpty(critical,
                "No shipped step may be critical — a critical failure aborts the pipeline, and with no " +
                "retry UI in the template that leaves the player on the splash screen forever. " +
                "Offending steps: " + string.Join(", ", critical));
        }

        // ---- Continue-past-failure, and make it observable ----

        [UnityTest]
        public IEnumerator FailedStep_PublishesBootstrapStepDegraded_AndPipelineContinues() =>
            UniTask.ToCoroutine(async () =>
        {
            using var bus = new R3EventBus();
            var observed = new List<BootstrapStepDegraded>();
            using var sub = bus.On<BootstrapStepDegraded>().Subscribe(observed.Add);

            var calls = new List<string>();
            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                new RecordingStep("Before", calls),
                new ThrowingStep("Broken", calls, maxRetries: 0),
                new RecordingStep("After", calls),
            }, _log, _reporter, bus, _report);

            await pipeline.RunAsync(null, CancellationToken.None);

            CollectionAssert.AreEqual(new[] { "Before", "Broken", "After" }, calls,
                "A failed non-critical step must not stop the steps behind it.");

            Assert.AreEqual(1, observed.Count, "Exactly one BootstrapStepDegraded per exhausted step.");
            Assert.AreEqual("Broken", observed[0].StepName);
            Assert.AreEqual(1, observed[0].Attempts, "maxRetries 0 means a single attempt.");
            Assert.IsInstanceOf<InvalidOperationException>(observed[0].Error);
        });

        [UnityTest]
        public IEnumerator DegradedEvent_ReportsTotalAttempts_NotJustTheLastOne() =>
            UniTask.ToCoroutine(async () =>
        {
            using var bus = new R3EventBus();
            var observed = new List<BootstrapStepDegraded>();
            using var sub = bus.On<BootstrapStepDegraded>().Subscribe(observed.Add);

            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                new ThrowingStep("Broken", null, maxRetries: 2),
            }, _log, _reporter, bus, _report);

            await pipeline.RunAsync(null, CancellationToken.None);

            Assert.AreEqual(1, observed.Count);
            Assert.AreEqual(3, observed[0].Attempts, "maxRetries 2 means 3 attempts were spent.");
        });

        [UnityTest]
        public IEnumerator SucceedingPipeline_PublishesNoDegradedEvent_AndReportsHealthy() =>
            UniTask.ToCoroutine(async () =>
        {
            using var bus = new R3EventBus();
            var observed = new List<BootstrapStepDegraded>();
            using var sub = bus.On<BootstrapStepDegraded>().Subscribe(observed.Add);

            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                new RecordingStep("A"),
                new RecordingStep("B"),
            }, _log, _reporter, bus, _report);

            await pipeline.RunAsync(null, CancellationToken.None);

            CollectionAssert.IsEmpty(observed);
            Assert.IsTrue(_report.IsHealthy);
            CollectionAssert.IsEmpty(_report.Degraded);
        });

        [UnityTest]
        public IEnumerator FlakyStep_ThatEventuallySucceeds_DoesNotDegrade() =>
            UniTask.ToCoroutine(async () =>
        {
            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                new FlakyStep("Flaky", succeedOnAttempt: 2, maxRetries: 2),
            }, _log, _reporter, null, _report);

            await pipeline.RunAsync(null, CancellationToken.None);

            Assert.IsTrue(_report.IsHealthy, "A step that recovered on retry is not degraded.");
        });

        // ---- The durable record ----

        [UnityTest]
        public IEnumerator Report_SurvivesPastThePublish_SoLateConstructedCodeCanStillSee() =>
            UniTask.ToCoroutine(async () =>
        {
            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                new RecordingStep("Fine"),
                new ThrowingStep("Iap", null, maxRetries: 0),
            }, _log, _reporter, null, _report);

            await pipeline.RunAsync(null, CancellationToken.None);

            // A shop screen built long after the bus event fired.
            Assert.IsFalse(_report.IsHealthy);
            Assert.IsTrue(_report.IsDegraded("Iap"));
            Assert.IsFalse(_report.IsDegraded("Fine"));
            Assert.AreEqual(1, _report.Degraded.Count);
            Assert.AreEqual("Iap", _report.Degraded[0].StepName);
            Assert.AreEqual(1, _report.Degraded[0].Attempts);
        });

        [UnityTest]
        public IEnumerator Report_RecordsMultipleFailures_InFailureOrder() =>
            UniTask.ToCoroutine(async () =>
        {
            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                new ThrowingStep("First", null, maxRetries: 0),
                new RecordingStep("Fine"),
                new ThrowingStep("Second", null, maxRetries: 0),
            }, _log, _reporter, null, _report);

            await pipeline.RunAsync(null, CancellationToken.None);

            Assert.AreEqual(2, _report.Degraded.Count);
            Assert.AreEqual("First", _report.Degraded[0].StepName);
            Assert.AreEqual("Second", _report.Degraded[1].StepName);
        });

        [UnityTest]
        public IEnumerator Rerun_ClearsThePreviousRunsFailures() => UniTask.ToCoroutine(async () =>
        {
            // Fails on attempt 1 and 2 (one run of maxRetries:0 burns one attempt),
            // then succeeds — so run #1 degrades and run #2 does not.
            var flaky = new FlakyStep("Flaky", succeedOnAttempt: 2, maxRetries: 0);
            var pipeline = new BootstrapPipeline(new IBootstrapStep[] { flaky }, _log, _reporter, null, _report);

            await pipeline.RunAsync(null, CancellationToken.None);
            Assert.IsTrue(_report.IsDegraded("Flaky"), "First run should have degraded.");

            await pipeline.RunAsync(null, CancellationToken.None);

            Assert.IsTrue(_report.IsHealthy,
                "A retry re-run must not inherit the previous run's failures — otherwise the " +
                "retry can never clear the degraded state.");
            CollectionAssert.IsEmpty(_report.Degraded);
        });

        [Test]
        public void Report_IsDegraded_FailsSafeOnBadInput()
        {
            _report.Record("Real", new InvalidOperationException(), 1);

            Assert.IsFalse(_report.IsDegraded(null));
            Assert.IsFalse(_report.IsDegraded(string.Empty));
            Assert.IsFalse(_report.IsDegraded("NeverRan"));
            Assert.IsFalse(_report.IsDegraded("real"), "Match is ordinal, not case-insensitive.");
            Assert.IsTrue(_report.IsDegraded("Real"));
        }

        [UnityTest]
        public IEnumerator CriticalStepMechanism_StillWorks_ForConsumerSteps() =>
            UniTask.ToCoroutine(async () =>
        {
            // The template ships nothing critical, but a consumer game that genuinely
            // cannot run without its own step (server login, say) must still be able
            // to opt in. Removing the shipped criticals must not remove the mechanism.
            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                new CriticalThrowingStep("ConsumerGate"),
            }, _log, _reporter, null, _report);

            try
            {
                await pipeline.RunAsync(null, CancellationToken.None);
                Assert.Fail("A consumer's critical step must still abort the pipeline.");
            }
            catch (BootstrapStepFailedException ex)
            {
                Assert.AreEqual("ConsumerGate", ex.StepName);
            }

            Assert.IsTrue(_report.IsHealthy,
                "An abort is not a degradation — it is reported via BootstrapFailed instead.");
        });

        // ---- Stub steps ----

        private sealed class RecordingStep : BootstrapStepBase
        {
            private readonly string _name;
            private readonly List<string> _calls;
            public override string Name => _name;
            public RecordingStep(string name, List<string> calls = null) { _name = name; _calls = calls; }
            protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
            {
                _calls?.Add(_name);
                return UniTask.CompletedTask;
            }
        }

        private sealed class ThrowingStep : BootstrapStepBase
        {
            private readonly string _name;
            private readonly List<string> _calls;
            private readonly int _maxRetries;
            public override string Name => _name;
            public override int MaxRetries => _maxRetries;
            public ThrowingStep(string name, List<string> calls, int maxRetries)
            { _name = name; _calls = calls; _maxRetries = maxRetries; }
            protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
            {
                _calls?.Add(_name);
                throw new InvalidOperationException($"{_name} boom");
            }
        }

        private sealed class CriticalThrowingStep : BootstrapStepBase
        {
            private readonly string _name;
            public override string Name => _name;
            public override bool IsCritical => true;
            public override int MaxRetries => 0;
            public CriticalThrowingStep(string name) { _name = name; }
            protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
                => throw new InvalidOperationException($"{_name} boom");
        }

        private sealed class FlakyStep : BootstrapStepBase
        {
            private readonly string _name;
            private readonly int _succeedOnAttempt;
            private readonly int _maxRetries;
            private int _count;
            public override string Name => _name;
            public override int MaxRetries => _maxRetries;
            public FlakyStep(string name, int succeedOnAttempt, int maxRetries)
            { _name = name; _succeedOnAttempt = succeedOnAttempt; _maxRetries = maxRetries; }
            protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
            {
                _count++;
                if (_count < _succeedOnAttempt) throw new InvalidOperationException($"flaky #{_count}");
                return UniTask.CompletedTask;
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
