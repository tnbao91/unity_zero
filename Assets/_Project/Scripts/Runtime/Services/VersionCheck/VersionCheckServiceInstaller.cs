using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.VersionCheck
{
    public static class VersionCheckServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.RegisterType(typeof(VersionCheckService), new[] { typeof(IVersionCheckService) }, Lifetime.Singleton, Resolution.Lazy);
        }
    }
}
