using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UnityEngine.TestTools;
using Zero.Bootstrap;
using Zero.Core;
using Zero.Core.Events;
using Zero.Infrastructure;
using Zero.Services.Events;

namespace Zero.Tests.EditMode
{
    [TestFixture]
    public sealed class BootstrapPipelineTests
    {
        private ILogService _log;
        private IBootstrapProgressReporter _reporter;

        [SetUp]
        public void SetUp()
        {
            _log = new StubLogService();
            _reporter = new BootstrapProgressReporter();
        }

        [UnityTest]
        public IEnumerator OrderPreserved() => UniTask.ToCoroutine(async () =>
        {
            var calls = new List<string>();
            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                new RecordingStep("A", calls),
                new RecordingStep("B", calls),
                new RecordingStep("C", calls),
            }, _log, _reporter);

            await pipeline.RunAsync(null, CancellationToken.None);

            Assert.AreEqual(new[] { "A", "B", "C" }, calls);
        });

        [UnityTest]
        public IEnumerator CriticalStepAbortsPipeline() => UniTask.ToCoroutine(async () =>
        {
            var calls = new List<string>();
            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                new RecordingStep("A", calls),
                new ThrowingStep("Boom", calls, isCritical: true, maxRetries: 0),
                new RecordingStep("Tail", calls),
            }, _log, _reporter);

            try
            {
                await pipeline.RunAsync(null, CancellationToken.None);
                Assert.Fail("Expected pipeline to throw on critical step.");
            }
            catch (BootstrapStepFailedException ex)
            {
                Assert.IsInstanceOf<InvalidOperationException>(ex.InnerException,
                    "Original failure must be preserved as InnerException.");
            }

            Assert.AreEqual(new[] { "A", "Boom" }, calls);
        });

        [UnityTest]
        public IEnumerator NonCriticalFailureSwallowed() => UniTask.ToCoroutine(async () =>
        {
            var calls = new List<string>();
            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                new RecordingStep("A", calls),
                new ThrowingStep("NoBoom", calls, isCritical: false, maxRetries: 0),
                new RecordingStep("Tail", calls),
            }, _log, _reporter);

            await pipeline.RunAsync(null, CancellationToken.None);

            // Step ran once (no retries) and then "Tail" still ran.
            Assert.AreEqual(new[] { "A", "NoBoom", "Tail" }, calls);
        });

        [UnityTest]
        public IEnumerator ProgressReachesOne() => UniTask.ToCoroutine(async () =>
        {
            var samples = new List<float>();
            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                new RecordingStep("A"),
                new RecordingStep("B"),
                new RecordingStep("C"),
            }, _log, _reporter);

            using (_reporter.Progress.Subscribe(p => samples.Add(p)))
            {
                await pipeline.RunAsync(null, CancellationToken.None);
            }

            Assert.That(samples, Is.Not.Empty);
            Assert.AreEqual(1f, samples[samples.Count - 1], 1e-4f);
            for (int i = 1; i < samples.Count; i++)
            {
                Assert.GreaterOrEqual(samples[i], samples[i - 1] - 1e-4f, "Progress must be monotonic.");
            }
        });

        [UnityTest]
        public IEnumerator OuterCancellationPropagates() => UniTask.ToCoroutine(async () =>
        {
            var calls = new List<string>();
            using var cts = new CancellationTokenSource();
            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                new RecordingStep("A", calls),
                new DelayStep("Wait", TimeSpan.FromSeconds(5)),
                new RecordingStep("Tail", calls),
            }, _log, _reporter);

            // Cancel mid-flight via a fire-and-forget UniTask.
            CancelAfter(cts, 50).Forget();

            try
            {
                await pipeline.RunAsync(null, cts.Token);
                Assert.Fail("Expected OperationCanceledException to propagate.");
            }
            catch (OperationCanceledException) { /* expected */ }

            CollectionAssert.Contains(calls, "A");
            CollectionAssert.DoesNotContain(calls, "Tail");
        });

        [UnityTest]
        public IEnumerator TimeoutOnCriticalStepAborts() => UniTask.ToCoroutine(async () =>
        {
            var calls = new List<string>();
            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                new RecordingStep("A", calls),
                new HangingStep("Hang", TimeSpan.FromMilliseconds(50), isCritical: true, maxRetries: 0),
                new RecordingStep("Tail", calls),
            }, _log, _reporter);

            try
            {
                await pipeline.RunAsync(null, CancellationToken.None);
                Assert.Fail("Expected BootstrapStepFailedException on critical timeout.");
            }
            catch (BootstrapStepFailedException ex)
            {
                Assert.IsInstanceOf<TimeoutException>(ex.InnerException,
                    "Timeout must be preserved as InnerException.");
            }

            CollectionAssert.Contains(calls, "A");
            CollectionAssert.DoesNotContain(calls, "Tail");
        });

        [UnityTest]
        public IEnumerator RetryPolicyHonored() => UniTask.ToCoroutine(async () =>
        {
            // Fails twice then succeeds on the third attempt.
            var attempts = new List<int>();
            var step = new FlakyStep("Flaky", attempts, succeedOnAttempt: 3, maxRetries: 2);
            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                step,
                new RecordingStep("After"),
            }, _log, _reporter);

            await pipeline.RunAsync(null, CancellationToken.None);

            Assert.AreEqual(3, attempts.Count, "Step should run exactly 3 times (initial + 2 retries).");
        });

        [UnityTest]
        public IEnumerator CriticalStepFailure_PublishesBootstrapFailed_WithStepNameAndAttempt() => UniTask.ToCoroutine(async () =>
        {
            using var bus = new R3EventBus();
            var observed = new List<BootstrapFailed>();
            using var sub = bus.On<BootstrapFailed>().Subscribe(observed.Add);

            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                new ThrowingStep("Boom", null, isCritical: true, maxRetries: 0),
            }, _log, _reporter, bus);

            try
            {
                await pipeline.RunAsync(null, CancellationToken.None);
                Assert.Fail("Expected pipeline to abort on critical step.");
            }
            catch (BootstrapStepFailedException) { /* expected */ }

            Assert.AreEqual(1, observed.Count, "Abort must publish exactly one BootstrapFailed.");
            Assert.AreEqual("Boom", observed[0].StepName);
            Assert.AreEqual(1, observed[0].Attempt);
            Assert.IsInstanceOf<InvalidOperationException>(observed[0].Error);
        });

        [UnityTest]
        public IEnumerator CriticalStepFailure_ThrowsBootstrapStepFailedException_WithStepName() => UniTask.ToCoroutine(async () =>
        {
            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                new ThrowingStep("Boom", null, isCritical: true, maxRetries: 0),
            }, _log, _reporter);

            try
            {
                await pipeline.RunAsync(null, CancellationToken.None);
                Assert.Fail("Expected pipeline to abort on critical step.");
            }
            catch (BootstrapStepFailedException ex)
            {
                Assert.AreEqual("Boom", ex.StepName);
                Assert.AreEqual(1, ex.Attempt);
                Assert.IsInstanceOf<InvalidOperationException>(ex.InnerException);
            }
        });

        [UnityTest]
        public IEnumerator CriticalStepTimeout_PublishesBootstrapFailed() => UniTask.ToCoroutine(async () =>
        {
            using var bus = new R3EventBus();
            var observed = new List<BootstrapFailed>();
            using var sub = bus.On<BootstrapFailed>().Subscribe(observed.Add);

            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                new HangingStep("Hang", TimeSpan.FromMilliseconds(50), isCritical: true, maxRetries: 0),
            }, _log, _reporter, bus);

            try
            {
                await pipeline.RunAsync(null, CancellationToken.None);
                Assert.Fail("Expected pipeline to abort on critical timeout.");
            }
            catch (BootstrapStepFailedException) { /* expected */ }

            Assert.AreEqual(1, observed.Count, "Abort must publish exactly one BootstrapFailed.");
            Assert.AreEqual("Hang", observed[0].StepName);
            Assert.IsInstanceOf<TimeoutException>(observed[0].Error);
        });

        private static async UniTaskVoid CancelAfter(CancellationTokenSource cts, int delayMs)
        {
            await UniTask.Delay(delayMs);
            cts.Cancel();
        }

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
            private readonly bool _critical;
            private readonly int _maxRetries;
            public override string Name => _name;
            public override bool IsCritical => _critical;
            public override int MaxRetries => _maxRetries;
            public ThrowingStep(string name, List<string> calls, bool isCritical, int maxRetries)
            { _name = name; _calls = calls; _critical = isCritical; _maxRetries = maxRetries; }
            protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
            {
                _calls?.Add(_name);
                throw new InvalidOperationException($"{_name} boom");
            }
        }

        private sealed class HangingStep : BootstrapStepBase
        {
            private readonly string _name;
            private readonly TimeSpan _timeout;
            private readonly bool _critical;
            private readonly int _maxRetries;
            public override string Name => _name;
            public override bool IsCritical => _critical;
            public override TimeSpan Timeout => _timeout;
            public override int MaxRetries => _maxRetries;
            public HangingStep(string name, TimeSpan timeout, bool isCritical, int maxRetries)
            { _name = name; _timeout = timeout; _critical = isCritical; _maxRetries = maxRetries; }
            protected override async UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
            {
                await UniTask.Never(ct);
            }
        }

        private sealed class DelayStep : BootstrapStepBase
        {
            private readonly string _name;
            private readonly TimeSpan _delay;
            public override string Name => _name;
            public override TimeSpan Timeout => TimeSpan.FromSeconds(60);
            public DelayStep(string name, TimeSpan delay) { _name = name; _delay = delay; }
            protected override async UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
            {
                await UniTask.Delay(_delay, cancellationToken: ct);
            }
        }

        private sealed class FlakyStep : BootstrapStepBase
        {
            private readonly string _name;
            private readonly List<int> _attempts;
            private readonly int _succeedOnAttempt;
            private readonly int _maxRetries;
            private int _count;
            public override string Name => _name;
            public override int MaxRetries => _maxRetries;
            public FlakyStep(string name, List<int> attempts, int succeedOnAttempt, int maxRetries)
            { _name = name; _attempts = attempts; _succeedOnAttempt = succeedOnAttempt; _maxRetries = maxRetries; }
            protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
            {
                _count++;
                _attempts.Add(_count);
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
