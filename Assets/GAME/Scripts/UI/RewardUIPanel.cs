using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Combat.Core;
using Game.Combat.Model;

namespace Game.UI
{
    /// <summary>
    /// MP3 플레이어 형태의 보상 선택 UI를 관리합니다.
    /// </summary>
    public sealed class RewardUIPanel : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private CombatEntryPoint combatEntryPoint;

        [Header("UI References")]
        [Tooltip("MP3 UI 전체 최상위 오브젝트")]
        [SerializeField] private GameObject rewardPanelRoot;

        [Tooltip("보상 항목들이 자식으로 생성될 Content 오브젝트 (Scroll View 내부)")]
        [SerializeField] private Transform contentContainer;

        [Tooltip("프로젝트 창에서 할당할 보상 항목 프리팹 (초록 글씨 한 줄)")]
        [SerializeField] private RewardItemUI rewardItemPrefab;

        // 생성된 리스트 항목들을 추적하고 정리하기 위한 리스트
        private readonly List<RewardItemUI> _spawnedItems = new List<RewardItemUI>();

        private void Awake()
        {
            if (rewardPanelRoot != null) rewardPanelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (combatEntryPoint != null)
                combatEntryPoint.OnCombatEnded += HandleCombatEnded;
        }

        private void OnDisable()
        {
            if (combatEntryPoint != null)
                combatEntryPoint.OnCombatEnded -= HandleCombatEnded;
        }

        private void HandleCombatEnded(CombatResult result)
        {
            if (!result.IsWin) return;

            // 1. 플레이어 조작 잠금
            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.UIOnly);

            // 2. MP3 화면 리스트 생성
            GenerateRewardList(result);

            // 3. UI 표시
            if (rewardPanelRoot != null)
                rewardPanelRoot.SetActive(true);
        }

        private void GenerateRewardList(CombatResult result)
        {
            // 기존에 남아있던 보상 목록 청소 (오브젝트 풀링을 쓰면 더 좋지만 일단 파괴 방식으로 진행)
            foreach (var item in _spawnedItems)
            {
                if (item != null) Destroy(item.gameObject);
            }
            _spawnedItems.Clear();

            // 임시 보상 선택지 생성 (나중에는 드랍 테이블이나 스킬 SO 데이터를 활용하면 됩니다)
            List<string> options = new List<string>
            {
                $"경험치 집중 ({result.TotalExp * 2} EXP)",
                $"전리품 집중 ({result.TotalGold * 2} G)",
                "새로운 스킬 해금: 연속 베기",
                "체력 100% 회복",
                "신비한 조각 획득"
            };

            // 선택지 개수만큼 프리팹 복제
            foreach (var opt in options)
            {
                var newItem = Instantiate(rewardItemPrefab, contentContainer);
                // 항목 이름과, 클릭 시 실행될 메서드(OnRewardSelected)를 전달
                newItem.Setup(opt, OnRewardSelected);
                _spawnedItems.Add(newItem);
            }
        }

        /// <summary>
        /// 플레이어가 스크롤에서 특정 보상을 클릭했을 때 실행
        /// </summary>
        private void OnRewardSelected(string selectedReward)
        {
            Debug.Log($"[Reward] 플레이어가 선택한 보상: {selectedReward}");
            // TODO: 실제 플레이어에게 selectedReward에 해당하는 보상 지급 로직

            // UI 닫기
            if (rewardPanelRoot != null) rewardPanelRoot.SetActive(false);

            // 상태 복구
            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.Exploration);
        }
    }
}