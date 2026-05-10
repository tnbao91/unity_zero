using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.Services.ReceiptValidator
{
    public static class ReceiptValidatorServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.RegisterType(typeof(StubReceiptValidator), new[] { typeof(IReceiptValidator) }, Lifetime.Singleton, Resolution.Lazy);
        }
    }
}
