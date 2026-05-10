using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.AdPlacement
{
    public static class AdPlacementServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.RegisterType(typeof(DefaultAdPlacementService), new[] { typeof(IAdPlacementService) }, Lifetime.Singleton, Resolution.Lazy);
        }
    }
}
