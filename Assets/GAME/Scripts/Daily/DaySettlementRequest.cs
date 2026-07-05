using System;
using Game.Reward;

namespace Game.Daily
{
    [Serializable]
    public sealed class DaySettlementRequest
    {
        public string settlementId;
        public DaySettlementSourceType sourceType = DaySettlementSourceType.Manual;
        public string questId;
        public string missionId;
        public string displayTitle;
        public int day;
        public int week;
        public string chapterId;
        public DayPhase phase;
        public RewardSourceType rewardSourceType;
        public string rewardSourceId;
        public int rewardGold;
        public int rewardExp;
        public string rewardItemId;
        public int rewardItemCount;
        public bool rewardDuplicateBlocked;

        public bool HasRewardSummary => rewardGold > 0 || rewardExp > 0 || rewardItemCount > 0 || rewardDuplicateBlocked;

        public static DaySettlementRequest ForQuest(string questId, RewardGrantResult rewardResult, string displayTitle = null)
        {
            DaySettlementRequest request = new()
            {
                settlementId = string.IsNullOrWhiteSpace(questId) ? null : $"quest:{questId}",
                sourceType = DaySettlementSourceType.Quest,
                questId = questId,
                displayTitle = displayTitle
            };

            request.ApplyRewardGrantResult(rewardResult);
            return request;
        }

        public static DaySettlementRequest ForMission(string missionId, string displayTitle = null)
        {
            return ForMission(missionId, displayTitle, RewardGrantResult.Empty);
        }

        public static DaySettlementRequest ForMission(string missionId, string displayTitle, RewardGrantResult rewardResult)
        {
            DaySettlementRequest request = new()
            {
                settlementId = string.IsNullOrWhiteSpace(missionId) ? null : $"mission:{missionId}",
                sourceType = DaySettlementSourceType.DemoMission,
                missionId = missionId,
                displayTitle = displayTitle
            };

            request.ApplyRewardGrantResult(rewardResult);
            return request;
        }

        public RewardGrantResult ToRewardGrantResult()
        {
            return new RewardGrantResult(
                rewardSourceType,
                rewardSourceId,
                rewardGold,
                rewardExp,
                rewardItemId,
                rewardItemCount,
                rewardDuplicateBlocked);
        }

        public void ApplyRewardGrantResult(RewardGrantResult rewardResult)
        {
            rewardSourceType = rewardResult.SourceType;
            rewardSourceId = rewardResult.SourceId;
            rewardGold = rewardResult.Gold;
            rewardExp = rewardResult.Exp;
            rewardItemId = rewardResult.ItemId;
            rewardItemCount = rewardResult.ItemCount;
            rewardDuplicateBlocked = rewardResult.DuplicateBlocked;
        }
    }
}
