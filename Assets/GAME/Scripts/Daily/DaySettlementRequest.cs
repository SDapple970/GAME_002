using System;
using Game.Reward;

namespace Game.Daily
{
    [Serializable]
    public sealed class DaySettlementRequest
    {
        public string settlementId;
        public string questId;
        public string missionId;
        public RewardSourceType rewardSourceType;
        public string rewardSourceId;
        public int rewardGold;
        public int rewardExp;
        public string rewardItemId;
        public int rewardItemCount;
        public bool rewardDuplicateBlocked;

        public static DaySettlementRequest ForQuest(string questId, RewardGrantResult rewardResult)
        {
            return new DaySettlementRequest
            {
                settlementId = string.IsNullOrWhiteSpace(questId) ? null : $"quest:{questId}",
                questId = questId,
                rewardSourceType = rewardResult.SourceType,
                rewardSourceId = rewardResult.SourceId,
                rewardGold = rewardResult.Gold,
                rewardExp = rewardResult.Exp,
                rewardItemId = rewardResult.ItemId,
                rewardItemCount = rewardResult.ItemCount,
                rewardDuplicateBlocked = rewardResult.DuplicateBlocked
            };
        }

        public static DaySettlementRequest ForMission(string missionId)
        {
            return new DaySettlementRequest
            {
                settlementId = string.IsNullOrWhiteSpace(missionId) ? null : $"mission:{missionId}",
                missionId = missionId
            };
        }
    }
}
