using System;

namespace Zero.Core
{
    public enum BootstrapStepAnchor
    {
        Append = 0,
        Before = 1,
        After = 2,
        Replace = 3,
    }

    // Consumer seam for the bootstrap pipeline. Register instances from YOUR
    // asmdef (inside a ContainerScope.OnRootContainerBuilding subscription) and
    // the pipeline factory composes them onto the template's default step list —
    // no package fork, no partial class. Registrations apply in registration
    // order; Before/After/Replace anchor on an IBootstrapStep.Name already in
    // the list (a default step or an earlier registration).
    public sealed class BootstrapStepRegistration
    {
        public IBootstrapStep Step { get; }
        public BootstrapStepAnchor Anchor { get; }
        public string AnchorStepName { get; }

        public BootstrapStepRegistration(
            IBootstrapStep step,
            BootstrapStepAnchor anchor = BootstrapStepAnchor.Append,
            string anchorStepName = null)
        {
            Step = step ?? throw new ArgumentNullException(nameof(step));
            if (anchor != BootstrapStepAnchor.Append && string.IsNullOrEmpty(anchorStepName))
            {
                throw new ArgumentException($"Anchor '{anchor}' requires an anchorStepName.", nameof(anchorStepName));
            }
            Anchor = anchor;
            AnchorStepName = anchorStepName;
        }
    }
}
