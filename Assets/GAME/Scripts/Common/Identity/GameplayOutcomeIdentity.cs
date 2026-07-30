using System;

namespace Game.Common.Identity
{
    public enum GameplayOutcomeSourceType
    {
        Unknown = 0,
        Combat = 1,
        QuestCompletion = 2,
        MissionCompletion = 3,
        Interaction = 4,
        Story = 5,
        Choice = 6,
        Loot = 7,
        Tutorial = 8
    }

    public readonly struct GameplayOutcomeIdentity : IEquatable<GameplayOutcomeIdentity>
    {
        public GameplayOutcomeSourceType SourceType { get; }
        public string SourceId { get; }
        public string ActionId { get; }

        public bool IsValid => SourceType != GameplayOutcomeSourceType.Unknown &&
                               !string.IsNullOrWhiteSpace(SourceId);

        public string CanonicalId => IsValid
            ? $"{(int)SourceType}|{Encode(SourceId)}|{Encode(ActionId)}"
            : string.Empty;

        public GameplayOutcomeIdentity(
            GameplayOutcomeSourceType sourceType,
            string sourceId,
            string actionId = null)
        {
            SourceType = sourceType;
            SourceId = Normalize(sourceId);
            ActionId = Normalize(actionId);
        }

        public static bool TryCreate(
            GameplayOutcomeSourceType sourceType,
            string sourceId,
            string actionId,
            out GameplayOutcomeIdentity identity)
        {
            identity = new GameplayOutcomeIdentity(sourceType, sourceId, actionId);
            return identity.IsValid;
        }

        public bool Equals(GameplayOutcomeIdentity other)
        {
            return SourceType == other.SourceType &&
                   string.Equals(SourceId, other.SourceId, StringComparison.Ordinal) &&
                   string.Equals(ActionId, other.ActionId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayOutcomeIdentity other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)SourceType;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(SourceId ?? string.Empty);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ActionId ?? string.Empty);
                return hash;
            }
        }

        public override string ToString() => CanonicalId;

        public static bool operator ==(GameplayOutcomeIdentity left, GameplayOutcomeIdentity right) => left.Equals(right);
        public static bool operator !=(GameplayOutcomeIdentity left, GameplayOutcomeIdentity right) => !left.Equals(right);

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string Encode(string value)
        {
            string normalized = value ?? string.Empty;
            return $"{normalized.Length}:{normalized}";
        }
    }
}
