using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using Zero.Bootstrap;
using Zero.Bootstrap.Steps;
using Zero.Core;
using Zero.Infrastructure;

namespace Zero.Tests.EditMode
{
    // Phase 6 spec: Crashlytics runs first for ordering but must never block app
    // launch — a real SDK outage with IsCritical=true would strand the player on
    // the splash screen for the full timeout and then abort the run.
    [TestFixture]
    public sealed class CrashlyticsStepTests
    {
        [UnityTest]
        public IEnumerator InitFailure_DoesNotAbortPipeline() => UniTask.ToCoroutine(async () =>
        {
            var calls = new List<string>();
            var pipeline = new BootstrapPipeline(new IBootstrapStep[]
            {
                new CrashlyticsStep(new ThrowingCrashlyticsService()),
                new RecordingStep("Tail", calls),
            }, new StubLogService(), new BootstrapProgressReporter());

            // Must complete: Crashlytics is non-critical, failure is swallowed.
            await pipeline.RunAsync(null, CancellationToken.None);

            CollectionAssert.Contains(calls, "Tail");
        });

        [Test]
        public void TimeoutDefault_IsFiveSeconds()
        {
            var step = new CrashlyticsStep(new ThrowingCrashlyticsService());
            Assert.AreEqual(TimeSpan.FromSeconds(5), step.Timeout,
                "Crashlytics must not consume the default 30s launch budget when a real SDK hangs.");
            Assert.IsFalse(step.IsCritical, "Crash-reporter outage must never block app launch.");
        }

        private sealed class ThrowingCrashlyticsService : ICrashlyticsService
        {
            public UniTask InitializeAsync(CancellationToken ct = default)
                => throw new InvalidOperationException("vendor outage");
            public void RecordException(Exception exception) { }
            public void Log(string message) { }
            public void SetCustomKey(string key, string value) { }
            public void SetUserId(string userId) { }
        }

        private sealed class RecordingStep : BootstrapStepBase
        {
            private readonly string _name;
            private readonly List<string> _calls;
            public override string Name => _name;
            public RecordingStep(string name, List<string> calls) { _name = name; _calls = calls; }
            protected override UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
            {
                _calls.Add(_name);
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
