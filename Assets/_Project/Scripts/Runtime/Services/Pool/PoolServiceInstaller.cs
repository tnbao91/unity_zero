using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.Pool
{
    public static class PoolServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.RegisterType(typeof(ReflexPoolService), new[] { typeof(IPoolService) }, Lifetime.Singleton, Resolution.Lazy);
        }
    }
}
