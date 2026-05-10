using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.IAP
{
    public static class IapServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.RegisterType(typeof(MockIapService), new[] { typeof(IIAPService) }, Lifetime.Singleton, Resolution.Lazy);
        }
    }
}
