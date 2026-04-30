namespace Zero.Gameplay.Events
{
    /// <summary>
    /// Published when a level begins.
    /// </summary>
    public readonly struct LevelStarted
    {
        public string LevelId { get; }

        public LevelStarted(string levelId)
        {
            LevelId = levelId;
        }
    }
}
