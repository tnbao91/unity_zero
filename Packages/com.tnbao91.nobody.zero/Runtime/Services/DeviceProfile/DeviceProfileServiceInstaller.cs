using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.DeviceProfile
{
    public static class DeviceProfileServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.RegisterType(typeof(DeviceProfileService), new[] { typeof(IDeviceProfileService) }, Lifetime.Singleton, Resolution.Lazy);
        }
    }
}
