using Reflex.Core;
using Reflex.Enums;
using UnityEngine;
using Zero.Core;

namespace Zero.Services.VersionCheck
{
    public static class VersionCheckServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            // Factory binding because the ctor takes a `string localVersion` that
            // Reflex can't auto-resolve (string isn't a registered contract).
            builder.RegisterFactory<IVersionCheckService>(
                c => new VersionCheckService(
                    c.Resolve<IRemoteConfigService>(),
                    c.Resolve<ILogService>(),
                    Application.version),
                new[] { typeof(IVersionCheckService) },
                Lifetime.Singleton,
                Resolution.Lazy);
        }
    }
}
