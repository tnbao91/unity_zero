using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.Scene
{
    public static class SceneServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.RegisterType(typeof(AddressableSceneService), new[] { typeof(ISceneService) }, Lifetime.Singleton, Resolution.Lazy);
        }
    }
}
