using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.Audio
{
    public static class AudioServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.RegisterType(typeof(MockAudioService), new[] { typeof(IAudioService) }, Lifetime.Singleton, Resolution.Lazy);
        }
    }
}
