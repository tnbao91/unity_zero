using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.Asset
{
    public static class AssetServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.RegisterType(typeof(AddressableAssetService), new[] { typeof(IAssetService) }, Lifetime.Singleton, Resolution.Lazy);
        }
    }
}
