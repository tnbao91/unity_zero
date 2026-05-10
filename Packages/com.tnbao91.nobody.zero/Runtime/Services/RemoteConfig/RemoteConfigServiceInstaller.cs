using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.RemoteConfig
{
    public static class RemoteConfigServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.RegisterType(typeof(MockRemoteConfigService), new[] { typeof(IRemoteConfigService) }, Lifetime.Singleton, Resolution.Lazy);
        }
    }
}
