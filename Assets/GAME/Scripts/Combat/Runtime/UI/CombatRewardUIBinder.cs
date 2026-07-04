using UnityEngine;
using System.Runtime.CompilerServices;
using Game.Combat.Core;
using Game.Combat.Model;
using Game.Core;
using Game.DemoMission.Runtime;
using Game.Reward;
using Game.UI;

namespace Game.Combat.UI
{
    public sealed class CombatRewardUIBinder : MonoBehaviour
    {
        [SerializeField] private CombatEntryPoint entryPoint;
        [SerializeField] private RewardUIPanel rewardPanel;
        [SerializeField] private RewardService rewardService;
        [SerializeField] private bool countEnemyDefeatOnVictory = true;
        [SerializeField] private bool restoreExplorationAfterRewardClosed = true;

        private bool _entrySubscribed;
        private bool _rewardSubscribed;
        private bool _missingEntryPointWarned;
        private bool _missingRewardPanelWarned;
        private bool _missingRewardServiceWarned;
        private bool _invalidCombatRewardWarned;

        private void Awake()
        {
            AutoBindReferences();
            WarnIfMissingReferences();
        }

        private void OnEnable()
        {
            AutoBindReferences();
            SubscribeToEntryPoint();
            SubscribeToRewardPanel();
            WarnIfMissingReferences();
        }

        private void OnDisable()
        {
            UnsubscribeFromEntryPoint();
            UnsubscribeFromRewardPanel();
        }

        private void AutoBindReferences()
        {
            if (entryPoint == null)
                entryPoint = FindFirstObjectByType<CombatEntryPoint>();

            if (rewardPanel == null)
                rewardPanel = FindFirstObjectByType<RewardUIPanel>(FindObjectsInactive.Include);

            if (rewardService == null)
                rewardService = RewardService.Instance != null
                    ? RewardService.Instance
                    : FindFirstObjectByType<RewardService>();
        }

        private void SubscribeToEntryPoint()
        {
            if (_entrySubscribed)
                return;

            if (entryPoint == null)
                return;

            entryPoint.OnCombatEnded += HandleCombatEnded;
            _entrySubscribed = true;
        }

        private void UnsubscribeFromEntryPoint()
        {
            if (!_entrySubscribed || entryPoint == null)
            {
                _entrySubscribed = false;
                return;
            }

            entryPoint.OnCombatEnded -= HandleCombatEnded;
            _entrySubscribed = false;
        }

        private void SubscribeToRewardPanel()
        {
            if (_rewardSubscribed)
                return;

            if (rewardPanel == null)
                return;

            rewardPanel.OnClosed += HandleRewardClosed;
            _rewardSubscribed = true;
        }

        private void UnsubscribeFromRewardPanel()
        {
            if (!_rewardSubscribed || rewardPanel == null)
            {
                _rewardSubscribed = false;
                return;
            }

            rewardPanel.OnClosed -= HandleRewardClosed;
            _rewardSubscribed = false;
        }

        private void HandleCombatEnded(CombatResult result)
        {
            if (result == null)
                return;

            if (countEnemyDefeatOnVictory && result.IsWin)
                DemoMissionRuntime.GetOrCreate().RegisterEnemyDefeated();

            RewardGrantResult grantResult = RewardGrantResult.Empty;
            if (rewardService != null)
            {
                RewardGrantRequest request = BuildCombatRewardRequest(result);
                grantResult = rewardService.GrantReward(request);
            }
            else
            {
                WarnMissingRewardService();
            }

            if (GameFlowController.Instance != null)
                GameFlowController.Instance.HandleCombatResult(result);
            else if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.Reward);

            if (rewardPanel != null)
            {
                rewardPanel.Show(result, grantResult);
            }
            else
            {
                WarnIfMissingRewardPanel();
            }
        }

        private RewardGrantRequest BuildCombatRewardRequest(CombatResult result)
        {
            if (result == null)
                return new RewardGrantRequest(RewardSourceType.Combat, "combat:null");

            if (result.TotalGold < 0 || result.TotalExp < 0)
                WarnInvalidCombatRewardData(result);

            string sourceId = $"combat:{RuntimeHelpers.GetHashCode(result)}";
            return new RewardGrantRequest(
                RewardSourceType.Combat,
                sourceId,
                Mathf.Max(0, result.TotalGold),
                Mathf.Max(0, result.TotalExp));
        }

        private void HandleRewardClosed()
        {
            if (!restoreExplorationAfterRewardClosed)
                return;

            if (GameFlowController.Instance != null)
                GameFlowController.Instance.HandleRewardClosed();
            else if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.Exploration);
        }

        private void WarnIfMissingReferences()
        {
            WarnIfMissingEntryPoint();
            WarnIfMissingRewardPanel();
        }

        private void WarnIfMissingEntryPoint()
        {
            if (entryPoint != null || _missingEntryPointWarned)
                return;

            _missingEntryPointWarned = true;
            Debug.LogWarning("[CombatRewardUIBinder] CombatEntryPoint is missing. Reward UI will not receive combat end events.", this);
        }

        private void WarnIfMissingRewardPanel()
        {
            if (rewardPanel != null || _missingRewardPanelWarned)
                return;

            _missingRewardPanelWarned = true;
            Debug.LogWarning("[CombatRewardUIBinder] RewardUIPanel is missing. Combat rewards cannot be shown.", this);
        }

        private void WarnMissingRewardService()
        {
            if (_missingRewardServiceWarned)
                return;

            _missingRewardServiceWarned = true;
            Debug.LogWarning("[CombatRewardUIBinder] RewardService is missing. Combat result reward was not granted.", this);
        }

        private void WarnInvalidCombatRewardData(CombatResult result)
        {
            if (_invalidCombatRewardWarned)
                return;

            _invalidCombatRewardWarned = true;
            Debug.LogWarning($"[CombatRewardUIBinder] CombatResult reward values were negative and were clamped. gold={result.TotalGold}, exp={result.TotalExp}", this);
        }
    }
}
