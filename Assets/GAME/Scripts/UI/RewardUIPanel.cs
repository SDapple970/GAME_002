// Scripts/UI/RewardUIPanel.cs
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Combat.Core;
using Game.Combat.Model; // CombatResult 사용을 위해 추가

namespace Game.UI
{
    public sealed class RewardUIPanel : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private CombatEntryPoint combatEntryPoint;

        [Header("UI References")]
        [SerializeField] private GameObject rewardPanelRoot;
        [SerializeField] private Text rewardText;
        [SerializeField] private Button confirmButton;

        private void Awake()
        {
            if (rewardPanelRoot != null) rewardPanelRoot.SetActive(false);
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmButtonClicked);
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

        /// <summary>
        /// 전투가 종료되었을 때 호출됩니다. (CombatResult 데이터를 받아옵니다)
        /// </summary>
        private void HandleCombatEnded(CombatResult result)
        {
            // 패배했다면 보상 팝업을 띄우지 않고 리턴 (패배 처리는 다른 스크립트에서 담당)
            if (!result.IsWin) return;

            // 1. 플레이어 조작 잠금
            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.UIOnly);

            // 2. 전달받은 실제 보상 데이터로 UI 텍스트 세팅
            if (rewardText != null)
            {
                rewardText.text = $"전투 승리!\n\n획득 경험치: {result.TotalExp} EXP\n획득 골드: {result.TotalGold} G";
            }

            // 3. UI 표시
            if (rewardPanelRoot != null)
                rewardPanelRoot.SetActive(true);
        }

        private void OnConfirmButtonClicked()
        {
            // TODO: 실제 플레이어 인벤토리에 result.TotalGold, result.TotalExp 등을 더해주는 로직 호출

            if (rewardPanelRoot != null) rewardPanelRoot.SetActive(false);

            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.Exploration);
        }
    }
}