using System.Collections.Generic;
using UnityEngine;
using Game.Combat.Model;

namespace Game.Combat.Adapters
{
    /// <summary>
    /// 필드에서 전투로 넘어갈 때 필요한 최소 정보(결합 최소화용).
    /// 필드 오브젝트 참조는 여기서만 들고, 전투 코어는 ICombatant 어댑터로만 본다.
    /// </summary>
    public sealed class CombatStartRequest
    {
        public readonly StartReason Reason;
        public readonly Side InitiativeSide;
        public readonly int InspirationMax;
        public readonly int InspirationStart;

        public readonly OpeningEffectSO OpeningEffectOrNull;

        // 필드 객체 참조(전투 시작 시 어댑터가 래핑)
        public readonly List<GameObject> AllyFieldObjects = new();
        public readonly List<GameObject> EnemyFieldObjects = new();

        public CombatStartRequest(
            StartReason reason,
            Side initiativeSide,
            int inspirationMax,
            int inspirationStart,
            OpeningEffectSO openingEffectOrNull)
        {
            Reason = reason;
            InitiativeSide = initiativeSide;
            InspirationMax = inspirationMax;
            InspirationStart = inspirationStart;
            OpeningEffectOrNull = openingEffectOrNull;
        }
    }
}
