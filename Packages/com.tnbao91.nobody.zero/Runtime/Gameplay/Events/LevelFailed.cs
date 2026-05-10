namespace Zero.Gameplay.Events
{
    /// <summary>
    /// Published when a level is failed.
    /// </summary>
    public readonly struct LevelFailed
    {
        public string LevelId { get; }
        public string Reason { get; }

        public LevelFailed(string levelId, string reason = "")
        {
            LevelId = levelId;
            Reason = reason;
        }
    }
}
