using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.Save
{
    public static class SaveServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.RegisterType(typeof(EncryptedJsonSaveService), new[] { typeof(ISaveService) }, Lifetime.Singleton, Resolution.Lazy);
        }
    }
}
