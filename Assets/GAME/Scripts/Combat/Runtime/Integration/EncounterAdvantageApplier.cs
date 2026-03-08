// 위치: GAME/Scripts/Combat/Integration/EncounterAdvantageApplier.cs
using UnityEngine;
using Game.Battle;
using Game.Combat.Core;
using Game.Combat.Model;

namespace Game.Combat.Integration
{
    /// <summary>
    /// 필드에서 넘어온 조우 데이터(선공/후공)를 읽어 전투 세션(아군/적군)에 보너스나 페널티를 적용합니다.
    /// </summary>
    public sealed class EncounterAdvantageApplier : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("전투 시작 이벤트를 구독할 CombatEntryPoint")]
        [SerializeField] private CombatEntryPoint entryPoint;

        [Header("Player First (선공 보너스)")]
        [Tooltip("플레이어 선공 시 적 전체에게 입힐 체력 데미지")]
        [SerializeField] private int playerFirstDamage = 2;
        [Tooltip("플레이어 선공 시 적 전체에게 가할 스태거(그로기) 수치")]
        [SerializeField] private int playerFirstStagger = 2;

        [Header("Enemy First (후공/기습 페널티)")]
        [Tooltip("적에게 선공당했을 때 아군 전체가 입을 체력 데미지")]
        [SerializeField] private int enemyFirstDamage = 1;
        [Tooltip("적에게 선공당했을 때 아군 전체가 받을 스태거(그로기) 수치")]
        [SerializeField] private int enemyFirstStagger = 1;

        private void OnEnable()
        {
            if (entryPoint != null)
                entryPoint.OnCombatStarted += HandleCombatStarted;
        }

        private void OnDisable()
        {
            if (entryPoint != null)
                entryPoint.OnCombatStarted -= HandleCombatStarted;
        }

        private void HandleCombatStarted(CombatSession session)
        {
            // 1. 저장해둔 조우 데이터 가져오기
            var req = BattleTransitionController.LastEncounterRequest;
            Debug.Log($"[AdvantageApplier] 전투 시작! 적용된 어드밴티지: {req.Advantage}");

            // 2. 어드밴티지 상태에 따른 효과(데미지/스태거) 적용
            switch (req.Advantage)
            {
                case EncounterAdvantage.PlayerFirst:
                    ApplyPlayerFirstBonus(session);
                    break;

                case EncounterAdvantage.EnemyFirst:
                    ApplyEnemyFirstPenalty(session);
                    break;

                case EncounterAdvantage.None:
                default:
                    Debug.Log("[AdvantageApplier] 일반 조우 상태로 전투를 시작합니다.");
                    break;
            }
        }

        private void ApplyPlayerFirstBonus(CombatSession session)
        {
            Debug.Log($"<color=green>플레이어 선공!</color> 적군의 시작 체력이 {playerFirstDamage}, 그로기가 {playerFirstStagger} 감소합니다.");

            // 모든 적군에게 데미지와 스태거 적용
            foreach (var enemy in session.Enemies)
            {
                enemy.ApplyDamage(playerFirstDamage);
                enemy.AddStagger(playerFirstStagger);
            }
        }

        private void ApplyEnemyFirstPenalty(CombatSession session)
        {
            Debug.Log($"<color=red>적 기습(후공)!</color> 아군의 시작 체력이 {enemyFirstDamage}, 그로기가 {enemyFirstStagger} 감소합니다.");

            // 모든 아군에게 데미지와 스태거 적용
            foreach (var ally in session.Allies)
            {
                ally.ApplyDamage(enemyFirstDamage);
                ally.AddStagger(enemyFirstStagger);
            }
        }
    }
}