// Scripts/Battle/BattleTransitionRequest.cs
using UnityEngine;

namespace Game.Battle
{
    public readonly struct BattleTransitionRequest
    {
        public readonly Vector3 EncounterWorldPos;
        public readonly string BattleSceneName; // 전투 씬 이름(없으면 동일 씬에서 UI만 띄우는 방식도 가능)

        public BattleTransitionRequest(Vector3 pos, string battleSceneName)
        {
            EncounterWorldPos = pos;
            BattleSceneName = battleSceneName;
        }
    }
}
