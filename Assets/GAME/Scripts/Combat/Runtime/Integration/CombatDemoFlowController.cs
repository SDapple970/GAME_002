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

        private bool _canonicalRouting;
        private bool _canonicalWorldLifecycle;

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

            ResolveRoutingMode();
        }

        private void OnEnable()
        {
            ResolveRoutingMode();

            if (entryPoint != null)
            {
                entryPoint.OnCombatStarted -= OnCombatStarted;
                entryPoint.OnCombatEnded -= OnCombatEnded;
                entryPoint.OnCombatStarted += OnCombatStarted;
                entryPoint.OnCombatEnded += OnCombatEnded;
            }

            if (rewardPanel != null)
            {
                rewardPanel.OnClosed -= OnRewardClosed;
                rewardPanel.OnClosed += OnRewardClosed;
            }
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
            ResolveRoutingMode();

            if (!_canonicalRouting && combatCanvasRoot != null)
                combatCanvasRoot.SetActive(true);

            if (!_canonicalWorldLifecycle)
            {
                fieldLock?.Lock();
                cameraController?.EnterCombat(session);
            }

            if (!_canonicalRouting && planningHUD != null)
            {
                planningHUD.Bind(session);
                planningHUD.Show();
            }

            // CombatEntryPoint owns global combat state synchronization.
        }

        private void OnCombatEnded(CombatResult result)
        {
            if (!_canonicalWorldLifecycle)
                cameraController?.HoldResultFrame();

            if (!_canonicalRouting)
                planningHUD?.Hide();

            if (!_canonicalRouting && rewardPanel != null)
                rewardPanel.Show(result);
            else if (!_canonicalRouting)
                OnRewardClosed();
        }

        private void OnRewardClosed()
        {
            if (!_canonicalRouting)
                rewardPanel?.Hide();

            if (!_canonicalWorldLifecycle)
            {
                cameraController?.ExitToExplorationFollow();
                fieldLock?.Unlock();
            }

            if (!_canonicalRouting && combatCanvasRoot != null)
                combatCanvasRoot.SetActive(false);

            // Reward flow owns the return to Exploration.
        }

        private void ResolveRoutingMode()
        {
            _canonicalRouting = FindFirstObjectByType<UIScreenRouter>(FindObjectsInactive.Include) != null &&
                                FindFirstObjectByType<CombatUIRootController>(FindObjectsInactive.Include) != null;
            _canonicalWorldLifecycle = CombatWorldLifecycleAdapter.FindFor(entryPoint) != null;
        }
    }
}
