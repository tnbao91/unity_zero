using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.Notification
{
    public static class NotificationServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.RegisterType(typeof(MockNotificationService), new[] { typeof(INotificationService) }, Lifetime.Singleton, Resolution.Lazy);
        }
    }
}
