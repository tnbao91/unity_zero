using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.Localization
{
    public static class LocalizationServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            // Real impl by default. Swap to MockLocalizationService for EditMode tests
            // or single-language game variants by editing this binding only.
            builder.RegisterType(typeof(UnityLocalizationService), new[] { typeof(IL10nService) }, Lifetime.Singleton, Resolution.Lazy);
        }
    }
}
