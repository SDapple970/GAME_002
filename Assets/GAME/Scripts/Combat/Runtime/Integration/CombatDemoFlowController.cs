using UnityEngine;
using Game.Combat.Core;
using Game.Combat.Model;
using Game.Combat.UI;
using Game.Core;
using Game.UI;

namespace Game.Combat.Integration
{
    public sealed class CombatDemoFlowController : MonoBehaviour
    {
        [SerializeField] private CombatEntryPoint entryPoint;
        [SerializeField] private CombatCameraController cameraController;
        [SerializeField] private CombatFieldLock fieldLock;
        [SerializeField] private CombatPlanningHUD planningHUD;
        [SerializeField] private RewardUIPanel rewardPanel;
        [SerializeField] private GameObject combatCanvasRoot;

        private void Awake()
        {
            if (entryPoint == null)
                entryPoint = FindFirstObjectByType<CombatEntryPoint>();
            if (cameraController == null)
                cameraController = FindFirstObjectByType<CombatCameraController>();
            if (fieldLock == null)
                fieldLock = FindFirstObjectByType<CombatFieldLock>();
            if (planningHUD == null)
                planningHUD = FindFirstObjectByType<CombatPlanningHUD>();
            if (rewardPanel == null)
                rewardPanel = FindFirstObjectByType<RewardUIPanel>();
        }

        private void OnEnable()
        {
            if (entryPoint != null)
            {
                entryPoint.OnCombatStarted += OnCombatStarted;
                entryPoint.OnCombatEnded += OnCombatEnded;
            }

            if (rewardPanel != null)
                rewardPanel.OnClosed += OnRewardClosed;
        }

        private void OnDisable()
        {
            if (entryPoint != null)
            {
                entryPoint.OnCombatStarted -= OnCombatStarted;
                entryPoint.OnCombatEnded -= OnCombatEnded;
            }

            if (rewardPanel != null)
                rewardPanel.OnClosed -= OnRewardClosed;
        }

        private void OnCombatStarted(CombatSession session)
        {
            if (combatCanvasRoot != null)
                combatCanvasRoot.SetActive(true);

            fieldLock?.Lock();
            cameraController?.EnterCombat(session);

            if (planningHUD != null)
            {
                planningHUD.Bind(session);
                planningHUD.Show();
            }

            // CombatEntryPoint owns global combat state synchronization.
        }

        private void OnCombatEnded(CombatResult result)
        {
            planningHUD?.Hide();
            cameraController?.HoldResultFrame();

            if (rewardPanel != null)
                rewardPanel.Show(result);
            else
                OnRewardClosed();
        }

        private void OnRewardClosed()
        {
            rewardPanel?.Hide();
            cameraController?.ExitToExplorationFollow();
            fieldLock?.Unlock();

            if (combatCanvasRoot != null)
                combatCanvasRoot.SetActive(false);

            // Reward flow owns the return to Exploration.
        }
    }
}
