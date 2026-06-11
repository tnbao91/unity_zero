using System;
using System.Collections.Generic;
using Zero.Core;

namespace Zero.Bootstrap
{
    // Applies container-registered BootstrapStepRegistrations onto the template's
    // default step list, in registration order. Unknown anchor names throw: a
    // typo'd step name is a developer error that must surface loudly at boot,
    // not silently skip a step.
    public static class BootstrapStepComposer
    {
        public static IBootstrapStep[] Compose(
            IReadOnlyList<IBootstrapStep> defaultSteps,
            IEnumerable<BootstrapStepRegistration> registrations)
        {
            if (defaultSteps == null) throw new ArgumentNullException(nameof(defaultSteps));

            var steps = new List<IBootstrapStep>(defaultSteps);
            if (registrations == null) return steps.ToArray();

            foreach (var registration in registrations)
            {
                Apply(steps, registration);
            }
            return steps.ToArray();
        }

        private static void Apply(List<IBootstrapStep> steps, BootstrapStepRegistration registration)
        {
            switch (registration.Anchor)
            {
                case BootstrapStepAnchor.Append:
                    steps.Add(registration.Step);
                    return;
                case BootstrapStepAnchor.Before:
                    steps.Insert(IndexOfAnchor(steps, registration), registration.Step);
                    return;
                case BootstrapStepAnchor.After:
                    steps.Insert(IndexOfAnchor(steps, registration) + 1, registration.Step);
                    return;
                case BootstrapStepAnchor.Replace:
                    steps[IndexOfAnchor(steps, registration)] = registration.Step;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(registration), registration.Anchor, "Unhandled BootstrapStepAnchor.");
            }
        }

        private static int IndexOfAnchor(List<IBootstrapStep> steps, BootstrapStepRegistration registration)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                if (string.Equals(steps[i].Name, registration.AnchorStepName, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            throw new ArgumentException(
                $"BootstrapStepRegistration for step '{registration.Step.Name}': anchor step " +
                $"'{registration.AnchorStepName}' not found in the pipeline. Anchor names must match an " +
                "IBootstrapStep.Name already in the list (a default step or an earlier registration).");
        }
    }
}
