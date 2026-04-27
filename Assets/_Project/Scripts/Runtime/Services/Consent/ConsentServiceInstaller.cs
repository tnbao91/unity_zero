using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.Consent
{
    public static class ConsentServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.RegisterType(typeof(MockConsentService), new[] { typeof(IConsentService) }, Lifetime.Singleton, Resolution.Lazy);
        }
    }
}
