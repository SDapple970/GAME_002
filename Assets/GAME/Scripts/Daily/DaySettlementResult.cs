using System;

namespace Game.Daily
{
    [Serializable]
    public sealed class DaySettlementResult
    {
        public string settlementId;
        public string questId;
        public string missionId;
        public int completedDay;
        public DayPhase completedPhase;
    }
}
