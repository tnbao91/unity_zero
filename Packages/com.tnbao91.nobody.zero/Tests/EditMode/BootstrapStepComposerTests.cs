using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Zero.Bootstrap;
using Zero.Core;

namespace Zero.Tests.EditMode
{
    // Phase 6 spec: consumers extend the bootstrap pipeline by registering
    // BootstrapStepRegistration into the container — composed here onto the
    // default step list with name-based anchors, in registration order.
    [TestFixture]
    public sealed class BootstrapStepComposerTests
    {
        [Test]
        public void Append_AddsToEnd()
        {
            var result = BootstrapStepComposer.Compose(
                Defaults("A", "B"),
                new[] { Registration("X") });

            Assert.AreEqual(new[] { "A", "B", "X" }, Names(result));
        }

        [Test]
        public void InsertBefore_PlacesBeforeAnchor()
        {
            var result = BootstrapStepComposer.Compose(
                Defaults("A", "B", "C"),
                new[] { Registration("X", BootstrapStepAnchor.Before, "B") });

            Assert.AreEqual(new[] { "A", "X", "B", "C" }, Names(result));
        }

        [Test]
        public void InsertAfter_PlacesAfterAnchor()
        {
            var result = BootstrapStepComposer.Compose(
                Defaults("A", "B", "C"),
                new[] { Registration("X", BootstrapStepAnchor.After, "B") });

            Assert.AreEqual(new[] { "A", "B", "X", "C" }, Names(result));
        }

        [Test]
        public void Replace_SwapsAnchorStep()
        {
            var result = BootstrapStepComposer.Compose(
                Defaults("A", "B", "C"),
                new[] { Registration("X", BootstrapStepAnchor.Replace, "B") });

            Assert.AreEqual(new[] { "A", "X", "C" }, Names(result));
        }

        [Test]
        public void UnknownAnchorStep_Throws()
        {
            Assert.Throws<ArgumentException>(() => BootstrapStepComposer.Compose(
                Defaults("A", "B"),
                new[] { Registration("X", BootstrapStepAnchor.Before, "Typo") }),
                "A typo'd anchor name must fail loudly at boot, not silently skip the step.");
        }

        [Test]
        public void RegistrationsApplyInOrder_LaterCanAnchorOnEarlier()
        {
            var result = BootstrapStepComposer.Compose(
                Defaults("A"),
                new[]
                {
                    Registration("X"),
                    Registration("Y", BootstrapStepAnchor.Before, "X"),
                });

            Assert.AreEqual(new[] { "A", "Y", "X" }, Names(result));
        }

        private static IBootstrapStep[] Defaults(params string[] names)
            => names.Select(n => (IBootstrapStep)new NamedStep(n)).ToArray();

        private static BootstrapStepRegistration Registration(
            string name,
            BootstrapStepAnchor anchor = BootstrapStepAnchor.Append,
            string anchorStepName = null)
            => new BootstrapStepRegistration(new NamedStep(name), anchor, anchorStepName);

        private static string[] Names(IReadOnlyList<IBootstrapStep> steps)
            => steps.Select(s => s.Name).ToArray();

        private sealed class NamedStep : IBootstrapStep
        {
            public string Name { get; }
            public bool IsCritical => false;
            public TimeSpan Timeout => TimeSpan.FromSeconds(1);
            public int MaxRetries => 0;
            public NamedStep(string name) => Name = name;
            public UniTask ExecuteAsync(IProgress<float> progress, CancellationToken ct)
                => UniTask.CompletedTask;
        }
    }
}
