using Reflex.Core;
using Reflex.Enums;

namespace Zero.Gameplay
{
    /// <summary>
    /// Reflex installer for Gameplay layer services.
    /// Registers GameStateMachine and LevelLoader as singletons.
    /// </summary>
    public static class GameplayServiceInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.RegisterType(
                typeof(GameStateMachine),
                new[] { typeof(IGameStateMachine) },
                Lifetime.Singleton,
                Resolution.Lazy);
        }
    }
}
