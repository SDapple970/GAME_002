using UnityEngine;
using Game.Combat.Core;
using Game.Combat.Model;
using Game.Core;
using Game.DemoMission.Runtime;
using Game.UI;

namespace Game.Combat.UI
{
    public sealed class CombatRewardUIBinder : MonoBehaviour
    {
        [SerializeField] private CombatEntryPoint entryPoint;
        [SerializeField] private RewardUIPanel rewardPanel;
        [SerializeField] private bool countEnemyDefeatOnVictory = true;
        [SerializeField] private bool restoreExplorationAfterRewardClosed = true;

        private bool _entrySubscribed;
        private bool _rewardSubscribed;
        private bool _missingEntryPointWarned;
        private bool _missingRewardPanelWarned;

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

            if (rewardPanel != null)
            {
                rewardPanel.Show(result);
            }
            else
            {
                WarnIfMissingRewardPanel();
            }
        }

        private void HandleRewardClosed()
        {
            if (!restoreExplorationAfterRewardClosed)
                return;

            if (GameStateMachine.Instance != null)
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
    }
}
