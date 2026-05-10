using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.Input
{
    public static class InputServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
#if ZERO_USE_MOCK_INPUT
            builder.RegisterType(typeof(MockInputService), new[] { typeof(IInputService) }, Lifetime.Singleton, Resolution.Lazy);
#else
            builder.RegisterType(typeof(UnityInputService), new[] { typeof(IInputService) }, Lifetime.Singleton, Resolution.Lazy);
#endif
        }
    }
}
