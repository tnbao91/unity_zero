namespace Zero.Gameplay.Events
{
    /// <summary>
    /// Published when a level is successfully completed.
    /// </summary>
    public readonly struct LevelCompleted
    {
        public string LevelId { get; }
        public int Score { get; }

        public LevelCompleted(string levelId, int score = 0)
        {
            LevelId = levelId;
            Score = score;
        }
    }
}
