using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.Analytics
{
    public static class AnalyticsServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.RegisterType(typeof(MockAnalyticsService), new[] { typeof(IAnalyticsService) }, Lifetime.Singleton, Resolution.Lazy);
        }
    }
}
