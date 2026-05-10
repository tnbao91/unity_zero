namespace Zero.Gameplay.Events
{
    /// <summary>
    /// Published when a level is restarted from within the play session.
    /// </summary>
    public readonly struct LevelRestarted
    {
        public string LevelId { get; }

        public LevelRestarted(string levelId)
        {
            LevelId = levelId;
        }
    }
}
