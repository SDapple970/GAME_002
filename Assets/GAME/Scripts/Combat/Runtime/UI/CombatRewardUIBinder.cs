using System.Collections.Generic;
using Game.Combat.Core;
using Game.Combat.Model;
using Game.Core;
using Game.DemoMission.Runtime;
using Game.Reward;
using Game.UI;
using UnityEngine;

namespace Game.Combat.UI
{
    public sealed class CombatRewardUIBinder : MonoBehaviour
    {
        internal enum CompletionLifecycle
        {
            Idle,
            Processing,
            AwaitingClose,
            Completed
        }

        private static readonly Dictionary<int, CombatRewardUIBinder> OwnersByEntryInstanceId = new();

        [SerializeField] private CombatEntryPoint entryPoint;
        [SerializeField] private RewardUIPanel rewardPanel;
        [SerializeField] private RewardService rewardService;
        [SerializeField] private bool countEnemyDefeatOnVictory = true;
        [SerializeField] private bool restoreExplorationAfterRewardClosed = true;

        private CombatEntryPoint _subscribedEntryPoint;
        private RewardUIPanel _subscribedRewardPanel;
        private CombatResult _pendingResult;
        private RewardGrantResult _pendingGrantResult;
        private string _activeCompletionId;
        private string _lastCompletedCompletionId;
        private CompletionLifecycle _lifecycle;
        private bool _ownsEntryPoint;

        private bool _missingEntryPointWarned;
        private bool _missingRewardPanelWarned;
        private bool _missingRewardServiceWarned;
        private bool _missingGameFlowControllerWarned;
        private bool _ambiguousEntryPointWarned;
        private bool _ambiguousRewardPanelWarned;
        private bool _ambiguousRewardServiceWarned;
        private bool _duplicateOwnerWarned;
        private bool _differentCompletionWarned;

        private int _processedCompletionCount;
        private int _panelShowCount;
        private int _rewardStateRequestCount;
        private int _closeCompletionCount;

        internal CompletionLifecycle Lifecycle => _lifecycle;
        internal string ActiveCompletionId => _activeCompletionId;
        internal bool OwnsEntryPoint => _ownsEntryPoint;
        internal int ProcessedCompletionCount => _processedCompletionCount;
        internal int PanelShowCount => _panelShowCount;
        internal int RewardStateRequestCount => _rewardStateRequestCount;
        internal int CloseCompletionCount => _closeCompletionCount;

        private void Awake()
        {
            AutoBindReferences();
            WarnIfMissingReferences();
        }

        private void OnEnable()
        {
            AutoBindReferences();
            if (TryClaimEntryPointOwnership())
            {
                SubscribeToEntryPoint();
                SubscribeToRewardPanel();
                RecoverAwaitingClose();
            }

            WarnIfMissingReferences();
        }

        private void OnDisable()
        {
            UnsubscribeFromEntryPoint();
            UnsubscribeFromRewardPanel();
            ReleaseEntryPointOwnership();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEntryPoint();
            UnsubscribeFromRewardPanel();
            ReleaseEntryPointOwnership();
        }

        private void AutoBindReferences()
        {
            if (entryPoint == null)
                entryPoint = FindUnique<CombatEntryPoint>(ref _ambiguousEntryPointWarned);

            if (rewardPanel == null)
                rewardPanel = FindUnique<RewardUIPanel>(ref _ambiguousRewardPanelWarned);

            if (rewardService == null)
            {
                rewardService = RewardService.Instance != null
                    ? RewardService.Instance
                    : FindUnique<RewardService>(ref _ambiguousRewardServiceWarned);
            }
        }

        private T FindUnique<T>(ref bool ambiguityWarned) where T : Component
        {
            T[] candidates = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (candidates.Length == 1)
                return candidates[0];

            if (candidates.Length > 1 && !ambiguityWarned)
            {
                ambiguityWarned = true;
                Debug.LogWarning($"[CombatRewardUIBinder] Multiple {typeof(T).Name} candidates were found. Assign the intended reference in the Inspector.", this);
            }

            return null;
        }

        private bool TryClaimEntryPointOwnership()
        {
            if (entryPoint == null)
                return false;

            int key = entryPoint.GetInstanceID();
            if (OwnersByEntryInstanceId.TryGetValue(key, out CombatRewardUIBinder owner))
            {
                if (owner == null)
                {
                    OwnersByEntryInstanceId.Remove(key);
                }
                else if (owner != this)
                {
                    if (!_duplicateOwnerWarned)
                    {
                        _duplicateOwnerWarned = true;
                        Debug.LogWarning(
                            $"[CombatRewardUIBinder] Duplicate completion owner blocked for entry '{entryPoint.name}'. " +
                            $"Active='{owner.name}', Duplicate='{name}'.",
                            this);
                    }

                    _ownsEntryPoint = false;
                    return false;
                }
            }

            OwnersByEntryInstanceId[key] = this;
            _ownsEntryPoint = true;
            return true;
        }

        private void ReleaseEntryPointOwnership()
        {
            if (!_ownsEntryPoint || entryPoint == null)
            {
                _ownsEntryPoint = false;
                return;
            }

            int key = entryPoint.GetInstanceID();
            if (OwnersByEntryInstanceId.TryGetValue(key, out CombatRewardUIBinder owner) && owner == this)
                OwnersByEntryInstanceId.Remove(key);

            _ownsEntryPoint = false;
        }

        private void SubscribeToEntryPoint()
        {
            if (_subscribedEntryPoint == entryPoint)
                return;

            UnsubscribeFromEntryPoint();
            _subscribedEntryPoint = entryPoint;
            if (_subscribedEntryPoint != null)
                _subscribedEntryPoint.OnCombatEnded += HandleCombatEnded;
        }

        private void UnsubscribeFromEntryPoint()
        {
            if (_subscribedEntryPoint != null)
                _subscribedEntryPoint.OnCombatEnded -= HandleCombatEnded;

            _subscribedEntryPoint = null;
        }

        private void SubscribeToRewardPanel()
        {
            if (_subscribedRewardPanel == rewardPanel)
                return;

            UnsubscribeFromRewardPanel();
            _subscribedRewardPanel = rewardPanel;
            if (_subscribedRewardPanel != null)
                _subscribedRewardPanel.OnClosed += HandleRewardClosed;
        }

        private void UnsubscribeFromRewardPanel()
        {
            if (_subscribedRewardPanel != null)
                _subscribedRewardPanel.OnClosed -= HandleRewardClosed;

            _subscribedRewardPanel = null;
        }

        private void HandleCombatEnded(CombatResult result)
        {
            if (!_ownsEntryPoint || result == null)
                return;

            RewardGrantRequest request = RewardService.CreateCombatRewardRequest(result, null);
            string completionId = request.SourceId;

            if (_lifecycle == CompletionLifecycle.Processing)
                return;

            if (_lifecycle == CompletionLifecycle.AwaitingClose)
            {
                if (_activeCompletionId != completionId)
                    WarnDifferentCompletionRejected(completionId);
                return;
            }

            if (_lastCompletedCompletionId == completionId)
                return;

            _lifecycle = CompletionLifecycle.Processing;
            _activeCompletionId = completionId;
            _pendingResult = result;
            _pendingGrantResult = RewardGrantResult.Empty;
            _processedCompletionCount++;

            NotifyDemoMissionCompatibility(result);

            if (rewardService != null)
                _pendingGrantResult = rewardService.GrantReward(request);
            else
                WarnMissingRewardService();

            // Bind local content before the state router exposes the global Reward root.
            if (rewardPanel != null)
            {
                SubscribeToRewardPanel();
                rewardPanel.Show(result, _pendingGrantResult);
                _panelShowCount++;
            }
            else
            {
                WarnIfMissingRewardPanel();
            }

            TryEnterRewardState(result);
            _lifecycle = CompletionLifecycle.AwaitingClose;

            if (rewardPanel == null)
                CompleteRewardFlow();
        }

        private void HandleRewardClosed()
        {
            if (!_ownsEntryPoint || _lifecycle != CompletionLifecycle.AwaitingClose)
                return;

            CompleteRewardFlow();
        }

        private void CompleteRewardFlow()
        {
            if (_lifecycle != CompletionLifecycle.AwaitingClose)
                return;

            _lifecycle = CompletionLifecycle.Completed;
            _lastCompletedCompletionId = _activeCompletionId;
            _activeCompletionId = null;
            _pendingResult = null;
            _pendingGrantResult = RewardGrantResult.Empty;
            _closeCompletionCount++;

            if (!restoreExplorationAfterRewardClosed)
                return;

            GameFlowController flow = GameFlowController.Instance;
            if (flow != null)
                flow.TryHandleRewardClosed();
            else
                WarnMissingGameFlowController();
        }

        private void TryEnterRewardState(CombatResult result)
        {
            GameFlowController flow = GameFlowController.Instance;
            if (flow == null)
            {
                WarnMissingGameFlowController();
                return;
            }

            _rewardStateRequestCount++;
            flow.TryHandleCombatResult(result);
        }

        private void RecoverAwaitingClose()
        {
            if (_lifecycle != CompletionLifecycle.AwaitingClose)
                return;

            if (rewardService == null)
            {
                rewardService = RewardService.Instance != null
                    ? RewardService.Instance
                    : FindUnique<RewardService>(ref _ambiguousRewardServiceWarned);
            }

            if (rewardPanel == null)
            {
                rewardPanel = FindUnique<RewardUIPanel>(ref _ambiguousRewardPanelWarned);
                if (rewardPanel == null)
                {
                    WarnIfMissingRewardPanel();
                    CompleteRewardFlow();
                    return;
                }
            }

            SubscribeToRewardPanel();
            if (!rewardPanel.IsOpen && _pendingResult != null)
            {
                rewardPanel.Show(_pendingResult, _pendingGrantResult);
                _panelShowCount++;
            }
        }

        private void NotifyDemoMissionCompatibility(CombatResult result)
        {
            if (!countEnemyDefeatOnVictory || !IsVictory(result) || DemoMissionRuntime.Instance == null)
                return;

            string completionId = RewardService.CreateCombatRewardRequest(result, null).SourceId;
            HashSet<int> uniqueEnemyIds = new HashSet<int>();
            for (int i = 0; i < result.DefeatedEnemyIds.Count; i++)
            {
                if (uniqueEnemyIds.Add(result.DefeatedEnemyIds[i]))
                {
                    DemoMissionRuntime.Instance.RegisterEnemyDefeated(
                        $"combat:{completionId}:enemy:{result.DefeatedEnemyIds[i]}");
                }
            }
        }

        private static bool IsVictory(CombatResult result)
        {
            return result != null &&
                   (result.EndReason != CombatEndReason.None
                       ? result.EndReason == CombatEndReason.Victory
                       : result.IsWin);
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
            Debug.LogWarning("[CombatRewardUIBinder] RewardUIPanel is missing. The completion will use the immediate close fallback.", this);
        }

        private void WarnMissingRewardService()
        {
            if (_missingRewardServiceWarned)
                return;

            _missingRewardServiceWarned = true;
            Debug.LogWarning("[CombatRewardUIBinder] RewardService is missing. This completion is consumed without currency or inventory mutation.", this);
        }

        private void WarnMissingGameFlowController()
        {
            if (_missingGameFlowControllerWarned)
                return;

            _missingGameFlowControllerWarned = true;
            Debug.LogWarning("[CombatRewardUIBinder] GameFlowController is missing. Reward presentation remains local and no global state transition can be requested.", this);
        }

        private void WarnDifferentCompletionRejected(string completionId)
        {
            if (_differentCompletionWarned)
                return;

            _differentCompletionWarned = true;
            Debug.LogWarning(
                $"[CombatRewardUIBinder] Completion rejected while another reward is awaiting close. active={_activeCompletionId}, rejected={completionId}",
                this);
        }

        internal static void ResetOwnershipForTests()
        {
            OwnersByEntryInstanceId.Clear();
        }
    }
}
