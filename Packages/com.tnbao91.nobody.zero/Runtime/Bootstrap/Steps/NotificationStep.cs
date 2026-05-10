using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;
using Zero.Infrastructure;

namespace Zero.Bootstrap.Steps
{
    public sealed class NotificationStep : BootstrapStepBase
    {
        public override string Name => "Notification";
        public override bool IsCritical => false;

        private readonly INotificationService _service;

        public NotificationStep(INotificationService service)
        {
            _service = service;
        }

        protected override async UniTask OnExecuteAsync(IProgress<float> progress, CancellationToken ct)
        {
            // Initialize only. Permission request deferred to value moment (e.g., after first level).
            // See docs/services/notification.md for explanation.
            await _service.InitializeAsync(ct);
        }
    }
}
