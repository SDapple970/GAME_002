using System;
using System.Collections.Generic;

namespace Game.Reward
{
    public readonly struct RewardGrantRequest
    {
        public readonly RewardSourceType SourceType;
        public readonly string SourceId;
        public readonly int Gold;
        public readonly int Exp;
        public readonly string ItemId;
        public readonly int ItemCount;
        public readonly string ActionId;
        public readonly string ProgressionTargetId;
        // Null preserves the legacy single-target fallback; empty explicitly grants no EXP.
        public readonly IReadOnlyList<string> ExperienceRecipientIds;

        public RewardGrantRequest(
            RewardSourceType sourceType,
            string sourceId,
            int gold = 0,
            int exp = 0,
            string itemId = null,
            int itemCount = 0,
            string actionId = null,
            string progressionTargetId = null,
            IEnumerable<string> experienceRecipientIds = null)
        {
            SourceType = sourceType;
            SourceId = sourceId;
            Gold = gold;
            Exp = exp;
            ItemId = itemId;
            ItemCount = itemCount;
            ActionId = actionId;
            ProgressionTargetId = progressionTargetId;
            if (experienceRecipientIds == null)
                ExperienceRecipientIds = null;
            else
            {
                SortedSet<string> unique = new(StringComparer.Ordinal);
                foreach (string id in experienceRecipientIds)
                    if (!string.IsNullOrWhiteSpace(id)) unique.Add(id.Trim());
                ExperienceRecipientIds = new List<string>(unique).AsReadOnly();
            }
        }
    }
}
