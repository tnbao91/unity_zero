namespace Zero.Gameplay.Events
{
    /// <summary>
    /// Published when the player exits the level (to menu, etc).
    /// </summary>
    public readonly struct LevelExited
    {
        public string LevelId { get; }

        public LevelExited(string levelId)
        {
            LevelId = levelId;
        }
    }
}
