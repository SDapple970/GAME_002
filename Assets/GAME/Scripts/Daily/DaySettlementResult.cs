using System;
using Game.Reward;

namespace Game.Daily
{
    [Serializable]
    public sealed class DaySettlementResult
    {
        public string settlementId;
        public DaySettlementSourceType sourceType;
        public string questId;
        public string missionId;
        public string completedQuestOrMissionId;
        public string displayTitle;
        public RewardSourceType rewardSourceType;
        public string rewardSourceId;
        public int rewardGold;
        public int rewardExp;
        public string rewardItemId;
        public int rewardItemCount;
        public bool rewardDuplicateBlocked;
        public bool completed;
        public int completedDay;
        public int completedWeek;
        public string completedChapterId;
        public DayPhase completedPhase;
        public DayPhase nextRecommendedPhase;

        public bool HasRewardSummary => rewardGold > 0 || rewardExp > 0 || rewardItemCount > 0 || rewardDuplicateBlocked;

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
    }
}
