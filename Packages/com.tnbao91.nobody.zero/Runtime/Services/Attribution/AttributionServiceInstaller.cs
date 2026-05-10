using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.Attribution
{
    public static class AttributionServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.RegisterType(typeof(MockAttributionService), new[] { typeof(IAttributionService) }, Lifetime.Singleton, Resolution.Lazy);
        }
    }
}
