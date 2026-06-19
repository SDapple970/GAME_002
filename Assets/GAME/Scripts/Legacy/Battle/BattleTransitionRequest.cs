// 위치: GAME/Scripts/Battle/BattleTransitionRequest.cs
using UnityEngine;

namespace Game.Battle
{
    // 선공/후공 상태를 정의하는 Enum
    public enum EncounterAdvantage
    {
        None,           // 기본 조우 (또는 이벤트)
        PlayerFirst,    // 플레이어가 필드 공격으로 먼저 타격함
        EnemyFirst      // 적이 플레이어에게 먼저 닿음
    }

    public readonly struct BattleTransitionRequest
    {
        public readonly Vector3 EncounterWorldPos;
        public readonly string BattleSceneName;
        public readonly EncounterAdvantage Advantage; // 추가된 조우 상태 데이터

        public BattleTransitionRequest(Vector3 pos, string battleSceneName, EncounterAdvantage advantage = EncounterAdvantage.None)
        {
            EncounterWorldPos = pos;
            BattleSceneName = battleSceneName;
            Advantage = advantage;
        }
    }
}