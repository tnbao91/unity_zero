using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.Events
{
    public static class EventsServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.RegisterType(typeof(R3EventBus), new[] { typeof(IEventBus) }, Lifetime.Singleton, Resolution.Lazy);
        }
    }
}
