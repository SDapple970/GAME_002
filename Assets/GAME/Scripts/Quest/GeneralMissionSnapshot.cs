using System;
using System.Collections.Generic;

namespace Game.Quest
{
    /// <summary>Read-only presentation data. QuestRuntime owns the underlying run.</summary>
    public sealed class GeneralMissionSnapshot
    {
        public string QuestId { get; internal set; }
        public string RunId { get; internal set; }
        public int Attempt { get; internal set; }
        public GeneralMissionKind Kind { get; internal set; }
        public QuestStatus Status { get; internal set; }
        public int Progress { get; internal set; }
        public int Required { get; internal set; }
        public int ExplorationPercent { get; internal set; }
        public int RescueHp { get; internal set; }
        public int RescueMaxHp { get; internal set; }
        public bool Following { get; internal set; }
        public bool Escaped { get; internal set; }
        public IReadOnlyList<string> SelectedTargetIds { get; internal set; } = Array.Empty<string>();
        public IReadOnlyList<string> CompletedTargetIds { get; internal set; } = Array.Empty<string>();
    }
}
