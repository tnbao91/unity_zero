using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.Input
{
    public static class InputServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.RegisterType(typeof(MockInputService), new[] { typeof(IInputService) }, Lifetime.Singleton, Resolution.Lazy);
        }
    }
}
