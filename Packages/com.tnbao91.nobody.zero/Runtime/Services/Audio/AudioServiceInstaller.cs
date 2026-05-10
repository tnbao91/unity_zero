using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.Audio
{
    public static class AudioServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
#if ZERO_USE_MOCK_AUDIO
            builder.RegisterType(typeof(MockAudioService), new[] { typeof(IAudioService) }, Lifetime.Singleton, Resolution.Lazy);
#else
            builder.RegisterType(typeof(AudioMixerService), new[] { typeof(IAudioService) }, Lifetime.Singleton, Resolution.Lazy);
#endif
        }
    }
}
