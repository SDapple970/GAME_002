using Game.Common.Identity;

namespace Game.Quest
{
    /// <summary>Immutable completion of one accepted repeatable quest run.</summary>
    public readonly struct QuestRunCompletion
    {
        public string QuestId { get; }
        public string RunId { get; }
        public string Title { get; }
        public int Gold { get; }
        public int Exp { get; }
        public int MissionDayCost { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(QuestId) && !string.IsNullOrWhiteSpace(RunId);
        public string ActionId => $"run:{RunId}";
        public string Key => new GameplayOutcomeIdentity(
            GameplayOutcomeSourceType.QuestCompletion, $"quest:{QuestId}", ActionId).CanonicalId;

        public QuestRunCompletion(string questId, string runId, string title, int gold, int exp, int missionDayCost)
        {
            QuestId = questId;
            RunId = runId;
            Title = title;
            Gold = System.Math.Max(0, gold);
            Exp = System.Math.Max(0, exp);
            MissionDayCost = System.Math.Max(0, missionDayCost);
        }
    }
}
