using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Reflex.Core;
using Zero.Core;

namespace Zero.Tests.EditMode
{
    // Pins the Reflex behavior the bootstrap-step seam depends on: All<T>()
    // returns bindings in registration order (Reflex stores resolvers per
    // contract in a List, appended in ContainerBuilder.Build order). If a
    // Reflex upgrade ever changes this, BootstrapStepComposer's "applied in
    // registration order" contract would silently break — this test makes
    // that a loud failure instead.
    [TestFixture]
    public sealed class ReflexRegistrationOrderTests
    {
        [Test]
        public void ResolveAll_PreservesRegistrationOrder()
        {
            var builder = new ContainerBuilder().SetName("registration-order-test");
            builder.RegisterValue(
                new BootstrapStepRegistration(new NamedStep("first")),
                new[] { typeof(BootstrapStepRegistration) });
            builder.RegisterValue(
                new BootstrapStepRegistration(new NamedStep("second")),
                new[] { typeof(BootstrapStepRegistration) });
            builder.RegisterValue(
                new BootstrapStepRegistration(new NamedStep("third")),
                new[] { typeof(BootstrapStepRegistration) });

            using var container = builder.Build();
            var names = container.All<BootstrapStepRegistration>()
                .Select(r => r.Step.Name)
                .ToArray();

            Assert.AreEqual(new[] { "first", "second", "third" }, names);
        }

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
