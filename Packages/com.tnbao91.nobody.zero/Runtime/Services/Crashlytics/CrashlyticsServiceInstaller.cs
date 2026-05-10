using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.Crashlytics
{
    public static class CrashlyticsServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.RegisterType(typeof(MockCrashlyticsService), new[] { typeof(ICrashlyticsService) }, Lifetime.Singleton, Resolution.Lazy);
        }
    }
}
