using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.Log
{
    public static class LogServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.RegisterType(typeof(LogService), new[] { typeof(ILogService) }, Lifetime.Singleton, Resolution.Lazy);
        }
    }
}
