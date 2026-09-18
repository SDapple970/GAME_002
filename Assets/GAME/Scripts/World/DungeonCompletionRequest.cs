namespace Game.World
{
    /// <summary>
    /// Immutable description of a dungeon completion that a presentation or exit
    /// integration may consume without taking ownership of quest progression.
    /// </summary>
    public readonly struct DungeonCompletionRequest
    {
        public DungeonCompletionRequest(
            string dungeonId,
            string completionQuestId,
            string destinationSceneName,
            string destinationSpawnPointId)
        {
            DungeonId = dungeonId;
            CompletionQuestId = completionQuestId;
            DestinationSceneName = destinationSceneName;
            DestinationSpawnPointId = destinationSpawnPointId;
        }

        public string DungeonId { get; }
        public string CompletionQuestId { get; }
        public string DestinationSceneName { get; }
        public string DestinationSpawnPointId { get; }
    }
}
