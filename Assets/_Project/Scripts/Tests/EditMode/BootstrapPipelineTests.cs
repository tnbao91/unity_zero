using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UnityEngine;
using Zero.Core;
using Zero.Infrastructure;

namespace Zero.Tests.EditMode
{
    [TestFixture]
    public sealed class BootstrapPipelineTests
    {
        private ILogService _logService;
        private IBootstrapProgressReporter _progressReporter;

        [SetUp]
        public void SetUp()
        {
            _logService = new StubLogService();
            _progressReporter = new BootstrapProgressReporter();
        }

        [UnityTest]
        public IEnumerator OrderPreserved() => UniTask.ToCoroutine(async () =>
        {
            var callOrder = new List<string>();
            var steps = new IBootstrapStep[]
            {
                new StubStep("Step1", callOrder),
                new StubStep("Step2", callOrder),
                new StubStep("Step3", callOrder),
            };

            var pipeline = new BootstrapPipeline(steps, _logService, _progressReporter);
            await pipeline.RunAsync(CancellationToken.None);

            Assert.AreEqual(new[] { "Step1", "Step2", "Step3" }, callOrder);
        });

        [UnityTest]
        public IEnumerator CriticalStepAbort() => UniTask.ToCoroutine(async () =>
        {
            var callOrder = new List<string>();
            var steps = new IBootstrapStep[]
            {
                new StubStep("Step1", callOrder),
                new FailingStep("CriticalFail", callOrder, isCritical: true),
                new StubStep("Step3", callOrder), // Should NOT execute.
            };

            var pipeline = new BootstrapPipeline(steps, _logService, _progressReporter);
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await pipeline.RunAsync(CancellationToken.None));

            Assert.AreEqual(new[] { "Step1", "CriticalFail" }, callOrder);
            Assert.That(ex.Message, Does.Contain("CriticalFail"));
        });

        [UnityTest]
        public IEnumerator NonCriticalSwallow() => UniTask.ToCoroutine(async () =>
        {
            var callOrder = new List<string>();
            var steps = new IBootstrapStep[]
            {
                new StubStep("Step1", callOrder),
                new FailingStep("NonCriticalFail", callOrder, isCritical: false),
                new StubStep("Step3", callOrder), // SHOULD execute.
            };

            var pipeline = new BootstrapPipeline(steps, _logService, _progressReporter);
            await pipeline.RunAsync(CancellationToken.None); // Should NOT throw.

            Assert.AreEqual(new[] { "Step1", "NonCriticalFail", "Step3" }, callOrder);
        });

        [UnityTest]
        public IEnumerator ProgressReporting() => UniTask.ToCoroutine(async () =>
        {
            var progressValues = new List<float>();
            var stepNames = new List<string>();

            var subscription1 = _progressReporter.Progress.Subscribe(p => progressValues.Add(p));
            var subscription2 = _progressReporter.CurrentStepName.Subscribe(n => stepNames.Add(n));

            var steps = new IBootstrapStep[]
            {
                new StubStep("StepA"),
                new StubStep("StepB"),
                new StubStep("StepC"),
            };

            var pipeline = new BootstrapPipeline(steps, _logService, _progressReporter);
            await pipeline.RunAsync(CancellationToken.None);

            subscription1.Dispose();
            subscription2.Dispose();

            // Progress should be monotonically increasing and end at 1.0.
            Assert.Greater(progressValues.Count, 0);
            Assert.AreEqual(1.0f, progressValues[progressValues.Count - 1]);
            for (int i = 1; i < progressValues.Count; i++)
            {
                Assert.GreaterOrEqual(progressValues[i], progressValues[i - 1],
                    "Progress should be monotonically increasing.");
            }

            // Step names should be reported.
            Assert.Contains("StepA", stepNames);
            Assert.Contains("StepB", stepNames);
            Assert.Contains("StepC", stepNames);
        });

        [UnityTest]
        public IEnumerator CancellationPropagates() => UniTask.ToCoroutine(async () =>
        {
            var callOrder = new List<string>();
            var cts = new CancellationTokenSource();

            var steps = new IBootstrapStep[]
            {
                new StubStep("Step1", callOrder),
                new CancellableStep("CancelledStep", callOrder, cts.Token),
                new StubStep("Step3", callOrder), // Should NOT execute.
            };

            var pipeline = new BootstrapPipeline(steps, _logService, _progressReporter);

            // Cancel mid-execution.
            cts.Cancel();

            var ex = Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await pipeline.RunAsync(CancellationToken.None));

            Assert.NotNull(ex);
        });

        [UnityTest]
        public IEnumerator TimeoutFires() => UniTask.ToCoroutine(async () =>
        {
            var callOrder = new List<string>();

            var steps = new IBootstrapStep[]
            {
                new StubStep("Step1", callOrder),
                new HangingStep("HangingStep", TimeSpan.FromMilliseconds(100)), // Timeout 100ms.
                new StubStep("Step3", callOrder), // Should NOT execute due to timeout.
            };

            var pipeline = new BootstrapPipeline(steps, _logService, _progressReporter);

            // Should timeout and throw or swallow depending on IsCritical.
            try
            {
                await pipeline.RunAsync(CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Timeout manifests as cancellation; may propagate if critical.
            }

            // Step1 should have executed; HangingStep should have failed/timed out.
            Assert.Contains("Step1", callOrder);
            Assert.That(callOrder, Does.Not.Contain("Step3"));
        });

        [UnityTest]
        public IEnumerator RetryPolicy() => UniTask.ToCoroutine(async () =>
        {
            var callOrder = new List<string>();
            var retryingStep = new RetryableStep("RetryableStep", callOrder, failUntilAttempt: 2);

            var steps = new IBootstrapStep[]
            {
                new StubStep("Step1", callOrder),
                retryingStep, // Fails 2x, succeeds on 3rd.
                new StubStep("Step3", callOrder),
            };

            var pipeline = new BootstrapPipeline(steps, _logService, _progressReporter);
            await pipeline.RunAsync(CancellationToken.None);

            // All steps should execute (retry swallows non-critical failures).
            Assert.AreEqual(new[] { "Step1", "RetryableStep", "RetryableStep", "RetryableStep", "Step3" }, callOrder);
        });

        /// <summary>
        /// Stub step that always succeeds.
        /// </summary>
        private sealed class StubStep : BootstrapStepBase
        {
            private readonly List<string> _callOrder;

            public override string Name => "StubStep";
            public override bool IsCritical => false;

            public StubStep(string name = "StubStep", List<string> callOrder = null)
            {
                _callOrder = callOrder;
            }

            protected override UniTask OnExecuteAsync(CancellationToken ct)
            {
                _callOrder?.Add(Name);
                return UniTask.CompletedTask;
            }
        }

        /// <summary>
        /// Step that fails on execute.
        /// </summary>
        private sealed class FailingStep : BootstrapStepBase
        {
            private readonly List<string> _callOrder;

            public override string Name { get; }
            public override bool IsCritical { get; }

            public FailingStep(string name, List<string> callOrder, bool isCritical)
            {
                Name = name;
                IsCritical = isCritical;
                _callOrder = callOrder;
            }

            protected override UniTask OnExecuteAsync(CancellationToken ct)
            {
                _callOrder?.Add(Name);
                throw new InvalidOperationException($"{Name} failed.");
            }
        }

        /// <summary>
        /// Step that respects cancellation.
        /// </summary>
        private sealed class CancellableStep : BootstrapStepBase
        {
            private readonly List<string> _callOrder;
            private readonly CancellationToken _cancelToken;

            public override string Name { get; }
            public override bool IsCritical => false;

            public CancellableStep(string name, List<string> callOrder, CancellationToken cancelToken)
            {
                Name = name;
                _callOrder = callOrder;
                _cancelToken = cancelToken;
            }

            protected override async UniTask OnExecuteAsync(CancellationToken ct)
            {
                _callOrder?.Add(Name);
                // Wait on the provided cancel token.
                _cancelToken.ThrowIfCancellationRequested();
                await UniTask.Delay(100, cancellationToken: ct);
            }
        }

        /// <summary>
        /// Step that never completes (for timeout testing).
        /// </summary>
        private sealed class HangingStep : BootstrapStepBase
        {
            public override string Name { get; }
            public override bool IsCritical => false;
            public override TimeSpan Timeout { get; }

            public HangingStep(string name, TimeSpan timeout)
            {
                Name = name;
                Timeout = timeout;
            }

            protected override async UniTask OnExecuteAsync(CancellationToken ct)
            {
                // Hang indefinitely (or until cancellation).
                await UniTask.Never(ct);
            }
        }

        /// <summary>
        /// Step that fails N times then succeeds.
        /// </summary>
        private sealed class RetryableStep : BootstrapStepBase
        {
            private readonly List<string> _callOrder;
            private readonly int _failUntilAttempt;
            private int _attemptCount;

            public override string Name { get; }
            public override bool IsCritical => false;
            public override int MaxRetries => 3;

            public RetryableStep(string name, List<string> callOrder, int failUntilAttempt)
            {
                Name = name;
                _callOrder = callOrder;
                _failUntilAttempt = failUntilAttempt;
            }

            protected override UniTask OnExecuteAsync(CancellationToken ct)
            {
                _callOrder?.Add(Name);
                _attemptCount++;
                if (_attemptCount < _failUntilAttempt)
                {
                    throw new InvalidOperationException($"{Name} attempt {_attemptCount} failed.");
                }
                return UniTask.CompletedTask;
            }
        }

        /// <summary>
        /// Stub ILogService for testing.
        /// </summary>
        private sealed class StubLogService : ILogService
        {
            public void Debug(string message) { }
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) { }
            public void Error(Exception ex, string message) { }
        }
    }
}
