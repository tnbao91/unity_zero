using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.Time
{
    public static class TimeServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.RegisterType(typeof(StubTimeService), new[] { typeof(ITimeService) }, Lifetime.Singleton, Resolution.Lazy);
        }
    }
}
