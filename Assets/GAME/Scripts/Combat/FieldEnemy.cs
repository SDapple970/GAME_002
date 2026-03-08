// 위치: GAME/Scripts/Battle/FieldEnemy.cs 생성
using System;
using UnityEngine;

namespace Game.Battle
{
    [RequireComponent(typeof(Collider2D))]
    public class FieldEnemy : MonoBehaviour, IDamageable
    {
        public static event Action<BattleTransitionRequest> OnBattleRequested;

        [Header("Encounter Settings")]
        [SerializeField] private string battleSceneName = "Battle";

        [Header("AI Settings")]
        [SerializeField] private Transform playerTarget; // 추적할 플레이어
        [SerializeField] private float aggroRange = 5f;  // 추적 시작 거리
        [SerializeField] private float moveSpeed = 2.5f; // 추적 속도

        private bool _isEncounterTriggered; // 중복 조우 방지용 플래그

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            col.isTrigger = true; // 충돌 감지를 위해 트리거 켜기
        }

        private void Update()
        {
            if (_isEncounterTriggered || playerTarget == null) return;

            // 1. 플레이어와의 거리 계산
            float distance = Vector2.Distance(transform.position, playerTarget.position);

            // 2. 어그로 범위 내에 들어오면 플레이어 쪽으로 이동 (선형 이동)
            if (distance <= aggroRange)
            {
                transform.position = Vector2.MoveTowards(transform.position, playerTarget.position, moveSpeed * Time.deltaTime);
            }
        }

        // 적이 플레이어의 몸체에 먼저 닿았을 때 (적 선공 - EnemyFirst)
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isEncounterTriggered) return;
            if (!other.CompareTag("Player")) return; // 플레이어의 태그가 "Player"인지 확인 필수!

            TriggerBattle(EncounterAdvantage.EnemyFirst);
        }

        // 플레이어의 OverworldAttack2D에 맞았을 때 (플레이어 선공 - PlayerFirst)
        public void TakeDamage(int amount)
        {
            if (_isEncounterTriggered) return;

            // 필드 공격력이 몇이든 바로 전투로 돌입
            TriggerBattle(EncounterAdvantage.PlayerFirst);
        }

        private void TriggerBattle(EncounterAdvantage advantage)
        {
            _isEncounterTriggered = true;
            Debug.Log($"[FieldEnemy] 전투 발생! 어드밴티지: {advantage}");

            // 전환 컨트롤러에 이벤트 발송
            OnBattleRequested?.Invoke(new BattleTransitionRequest(transform.position, battleSceneName, advantage));

            // 🌟 [수정 포인트] 심리스 전투를 위해 아래 코드를 지우거나 주석 처리해 줘!
            // gameObject.SetActive(false); 
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 인스펙터에서 어그로 범위를 노란색 원으로 시각화
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, aggroRange);
        }
#endif
    }
}