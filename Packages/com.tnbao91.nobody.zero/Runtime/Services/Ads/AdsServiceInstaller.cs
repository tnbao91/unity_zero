using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.Ads
{
    public static class AdsServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.RegisterType(typeof(MockAdsService), new[] { typeof(IAdsService) }, Lifetime.Singleton, Resolution.Lazy);
        }
    }
}
