using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.Notification
{
    public static class NotificationServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
#if ZERO_USE_MOCK_NOTIFICATION
            builder.RegisterType(typeof(MockNotificationService), new[] { typeof(INotificationService) }, Lifetime.Singleton, Resolution.Lazy);
#else
            builder.RegisterType(typeof(UnityMobileNotificationService), new[] { typeof(INotificationService) }, Lifetime.Singleton, Resolution.Lazy);
#endif
        }
    }
}
