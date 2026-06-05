using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Combat.Core;
using Game.Combat.Model;
using Game.NonCombat.Reward;

namespace Game.UI
{
    public sealed class RewardUIPanel : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private CombatEntryPoint combatEntryPoint;
        [SerializeField] private RewardApplier rewardApplier;

        [Header("UI References")]
        [SerializeField] private GameObject rewardPanelRoot;
        [SerializeField] private Transform contentContainer;
        [SerializeField] private RewardItemUI rewardItemPrefab;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text simpleMessageText;
        [SerializeField] private float fieldRewardAutoHideSeconds = 1.5f;

        private readonly List<RewardItemUI> _spawnedItems = new List<RewardItemUI>();
        private CombatResult _pendingResult;
        private bool _subscribedToEntryPoint;
        private bool _closeButtonBound;
        private Coroutine _fieldRewardRoutine;

        private void Awake()
        {
            AutoBindReferences();

            if (rewardPanelRoot != null)
                rewardPanelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            AutoBindReferences();
            SubscribeToEntryPoint();
            BindCloseButton();
        }

        private void OnDisable()
        {
            UnsubscribeFromEntryPoint();
            UnbindCloseButton();
        }

        private void HandleCombatEnded(CombatResult result)
        {
            if (result == null)
                return;

            if (!result.IsWin)
            {
                RestoreExploration();
                return;
            }

            if (!CanOpenRewardPanel())
            {
                ApplyReward(result);
                RestoreExploration();
                return;
            }

            _pendingResult = result;

            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.UIOnly);

            GenerateRewardList(result);
            rewardPanelRoot.SetActive(true);
        }

        public bool TryShowFieldRewardMessage(string message)
        {
            if (string.IsNullOrEmpty(message) || simpleMessageText == null)
                return false;

            if (_fieldRewardRoutine != null)
                StopCoroutine(_fieldRewardRoutine);

            _fieldRewardRoutine = StartCoroutine(Co_ShowFieldRewardMessage(message));
            return true;
        }

        private void AutoBindReferences()
        {
            if (combatEntryPoint == null)
                combatEntryPoint = FindFirstObjectByType<CombatEntryPoint>();

            if (rewardApplier == null)
                rewardApplier = RewardApplier.Instance;
        }

        private void SubscribeToEntryPoint()
        {
            if (_subscribedToEntryPoint)
                return;

            if (combatEntryPoint == null)
            {
                Debug.LogWarning("[RewardUIPanel] CombatEntryPoint is not assigned.", this);
                return;
            }

            combatEntryPoint.OnCombatEnded += HandleCombatEnded;
            _subscribedToEntryPoint = true;
        }

        private void UnsubscribeFromEntryPoint()
        {
            if (!_subscribedToEntryPoint || combatEntryPoint == null)
            {
                _subscribedToEntryPoint = false;
                return;
            }

            combatEntryPoint.OnCombatEnded -= HandleCombatEnded;
            _subscribedToEntryPoint = false;
        }

        private void BindCloseButton()
        {
            if (_closeButtonBound || closeButton == null)
                return;

            closeButton.onClick.AddListener(CloseRewardPanel);
            _closeButtonBound = true;
        }

        private void UnbindCloseButton()
        {
            if (!_closeButtonBound || closeButton == null)
            {
                _closeButtonBound = false;
                return;
            }

            closeButton.onClick.RemoveListener(CloseRewardPanel);
            _closeButtonBound = false;
        }

        private bool CanOpenRewardPanel()
        {
            if (rewardPanelRoot == null)
            {
                Debug.LogError("[RewardUIPanel] rewardPanelRoot is null.", this);
                return false;
            }

            if (contentContainer == null)
            {
                Debug.LogError("[RewardUIPanel] contentContainer is null.", this);
                return false;
            }

            if (rewardItemPrefab == null)
            {
                Debug.LogError("[RewardUIPanel] rewardItemPrefab is null.", this);
                return false;
            }

            return true;
        }

        private void GenerateRewardList(CombatResult result)
        {
            ClearRewardList();

            List<string> options = new List<string>
            {
                $"경험치 집중 ({result.TotalExp * 2} EXP)",
                $"전리품 집중 ({result.TotalGold * 2} G)",
                "새로운 스킬 해금: 연속 베기",
                "체력 100% 회복",
                "신비한 조각 획득"
            };

            foreach (string option in options)
            {
                RewardItemUI newItem = Instantiate(rewardItemPrefab, contentContainer);
                newItem.Setup(option, OnRewardSelected);
                _spawnedItems.Add(newItem);
            }
        }

        private void OnRewardSelected(string selectedReward)
        {
            Debug.Log($"[RewardUIPanel] 선택한 보상: {selectedReward}", this);
            CloseRewardPanel();
        }

        private void CloseRewardPanel()
        {
            ApplyReward(_pendingResult);
            _pendingResult = null;

            ClearRewardList();

            if (rewardPanelRoot != null)
                rewardPanelRoot.SetActive(false);

            RestoreExploration();
        }

        private void ApplyReward(CombatResult result)
        {
            if (result == null)
                return;

            RewardApplier applier = rewardApplier != null ? rewardApplier : RewardApplier.Instance;
            if (applier != null)
                applier.ApplyCombatResult(result);
            else
                Debug.LogWarning("[RewardUIPanel] RewardApplier is missing.", this);
        }

        private void ClearRewardList()
        {
            foreach (RewardItemUI item in _spawnedItems)
            {
                if (item != null)
                    Destroy(item.gameObject);
            }

            _spawnedItems.Clear();
        }

        private static void RestoreExploration()
        {
            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.Exploration);
        }

        private IEnumerator Co_ShowFieldRewardMessage(string message)
        {
            simpleMessageText.text = message;

            if (rewardPanelRoot != null)
                rewardPanelRoot.SetActive(true);

            yield return new WaitForSeconds(Mathf.Max(0.1f, fieldRewardAutoHideSeconds));

            simpleMessageText.text = string.Empty;

            if (_pendingResult == null && rewardPanelRoot != null)
                rewardPanelRoot.SetActive(false);

            _fieldRewardRoutine = null;
        }
    }
}
