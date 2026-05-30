using UnityEngine;

namespace Game.Search
{
    public enum SearchRewardKind
    {
        SmallLoot,
        LargeLoot,
        Journal,
        Cat,
        Currency,
        Custom
    }

    public sealed class SearchRewardProposal
    {
        public SearchRewardProposal(
            string rewardId,
            string rewardName,
            string description,
            Sprite icon,
            SearchRewardKind kind,
            int amount)
        {
            RewardId = rewardId;
            RewardName = rewardName;
            Description = description;
            Icon = icon;
            Kind = kind;
            Amount = amount;
        }

        public string RewardId { get; }
        public string RewardName { get; }
        public string Description { get; }
        public Sprite Icon { get; }
        public SearchRewardKind Kind { get; }
        public int Amount { get; }
    }
}
