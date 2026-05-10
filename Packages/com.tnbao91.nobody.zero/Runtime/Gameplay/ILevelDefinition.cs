using UnityEngine;

namespace Zero.Gameplay
{
    /// <summary>
    /// Abstract base for level configuration ScriptableObjects.
    /// Consumers subclass and add genre-specific fields (grid size, enemy types, etc).
    /// </summary>
    public abstract class ILevelDefinition : ScriptableObject
    {
        /// <summary>
        /// Unique identifier for the level (e.g., "level_001").
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// Human-readable name for UI display (e.g., "Forest 1 — Easy").
        /// </summary>
        public abstract string DisplayName { get; }

        /// <summary>
        /// Addressables key to the level prefab (e.g., "levels/level_001").
        /// </summary>
        public abstract string AddressablePrefabKey { get; }
    }
}
