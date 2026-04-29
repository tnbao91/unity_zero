using Reflex.Core;
using Reflex.Enums;
using Zero.Core;

namespace Zero.UI
{
    /// <summary>
    /// Reflex installer for UIService.
    /// Called by ProjectScopeInstaller to register the UI service.
    /// </summary>
    public static class UIServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.RegisterType<UIService>()
                .As<IUIService>()
                .Lifetime(Lifetime.Singleton)
                .Resolution(Resolution.Lazy);
        }
    }
}
