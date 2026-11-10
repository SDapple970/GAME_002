using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Game.Combat.Model;
using Game.NonCombat.Reward;
using Game.Reward;
using Game.Core;

namespace Game.UI
{
    public sealed class RewardUIPanel : MonoBehaviour
    {
        [Header("UI References")]
        [FormerlySerializedAs("rewardPanelRoot")]
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text resultText;
        [FormerlySerializedAs("contentContainer")]
        [SerializeField] private Transform rewardRowRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button titleButton;

        [Header("Legacy / Optional")]
        [SerializeField] private Text titleLegacyText;
        [SerializeField] private Text resultLegacyText;
        [SerializeField] private Text simpleMessageText;
        [SerializeField] private float fieldRewardAutoHideSeconds = 1.5f;
        [SerializeField] private RewardApplier rewardApplier;
        [SerializeField] private FieldRewardToast fieldRewardToast;

        [Header("Combat Result")]
        [SerializeField] private string victoryTitle = "전투 승리";
        [SerializeField] private string defeatTitle = "전투 패배";
        [SerializeField] private string[] victoryRewardLines = { "Gold 100", "Potion 1" };
        [SerializeField] private string[] defeatRewardLines = { "획득 보상 없음" };
        [SerializeField] private bool showResultText = false;
        [SerializeField] private string victoryResultText = "";
        [SerializeField] private string defeatResultText = "";

        public event Action OnClosed;
        public event Action OnRestartRequested;
        public event Action OnTitleRequested;

        public bool IsOpen => _presentationOpen;
        public bool HasActiveCombatResult => _presentationOpen && _pendingResult != null;

        private readonly List<GameObject> _spawnedRows = new();
        private CombatResult _pendingResult;
        private bool _closeButtonBound;
        private Coroutine _fieldRewardRoutine;
        private bool _presentationOpen;
        private bool _closeRaised;
        private bool _choicePending;

        private void Awake()
        {
            if (rewardApplier == null)
                rewardApplier = RewardApplier.Instance;

            if (!_presentationOpen)
                Hide();
        }

        private void OnEnable()
        {
            BindCloseButton();
            BindDefeatButtons();
        }

        private void OnDisable()
        {
            UnbindCloseButton();
            UnbindDefeatButtons();
            StopFieldRewardMessage();
        }

        public void Show(CombatResult result)
        {
            Show(result, RewardGrantResult.Empty, false);
        }

        public void Show(CombatResult result, RewardGrantResult grantResult)
        {
            Show(result, grantResult, true);
        }

        private void Show(CombatResult result, RewardGrantResult grantResult, bool preferGrantResult)
        {
            BindCloseButton();
            BindDefeatButtons();
            StopFieldRewardMessage();
            _pendingResult = result;
            _presentationOpen = true;
            _closeRaised = false;
            _choicePending = false;
            ClearRewardRows();

            bool isWin = IsVictory(result);
            bool defeat = IsDefeat(result);
            bool canRestart = GameFlowController.Instance != null && GameFlowController.Instance.CanRestartDungeon;
            if (closeButton != null) closeButton.gameObject.SetActive(!defeat);
            if (restartButton != null) { restartButton.gameObject.SetActive(defeat); restartButton.interactable = canRestart; }
            if (titleButton != null) { titleButton.gameObject.SetActive(defeat); titleButton.interactable = true; }
            SetText(titleText, titleLegacyText, isWin ? victoryTitle : defeatTitle);
            SetText(resultText, resultLegacyText, defeat && !canRestart ? "재시작 불가" : showResultText ? BuildResultText(isWin) : string.Empty);

            if (preferGrantResult)
                AddGrantResultRows(isWin, grantResult);
            else
                AddConfiguredRewardRows(isWin);

            if (CanToggleOwnRoot())
                root.SetActive(true);
        }

        public void Hide()
        {
            StopFieldRewardMessage();
            _pendingResult = null;
            _presentationOpen = false;
            _closeRaised = true;
            ClearRewardRows();

            if (CanToggleOwnRoot())
                root.SetActive(false);

            if (simpleMessageText != null)
                simpleMessageText.text = string.Empty;
        }

        public bool TryShowFieldRewardMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            if (_presentationOpen)
                return false;

            if (fieldRewardToast != null)
                return fieldRewardToast.Show(message);

            if (simpleMessageText == null)
                return false;

            StopFieldRewardMessage();

            _fieldRewardRoutine = StartCoroutine(Co_ShowFieldRewardMessage(message));
            return true;
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

        private void CloseRewardPanel()
        {
            if (!_presentationOpen || _closeRaised || IsDefeat(_pendingResult))
                return;

            _closeRaised = true;
            _presentationOpen = false;
            _pendingResult = null;

            StopFieldRewardMessage();
            ClearRewardRows();
            if (CanToggleOwnRoot())
                root.SetActive(false);
            if (simpleMessageText != null)
                simpleMessageText.text = string.Empty;

            OnClosed?.Invoke();
        }

        private void BindDefeatButtons()
        {
            if (restartButton != null) { restartButton.onClick.RemoveListener(RequestRestart); restartButton.onClick.AddListener(RequestRestart); }
            if (titleButton != null) { titleButton.onClick.RemoveListener(RequestTitle); titleButton.onClick.AddListener(RequestTitle); }
        }

        private void UnbindDefeatButtons()
        {
            if (restartButton != null) restartButton.onClick.RemoveListener(RequestRestart);
            if (titleButton != null) titleButton.onClick.RemoveListener(RequestTitle);
        }

        private void RequestRestart() => RequestDefeatChoice(OnRestartRequested, true);
        private void RequestTitle() => RequestDefeatChoice(OnTitleRequested, false);

        private void RequestDefeatChoice(Action requested, bool restarting)
        {
            if (!_presentationOpen || _choicePending || !IsDefeat(_pendingResult) || requested == null ||
                (restarting && (GameFlowController.Instance == null || !GameFlowController.Instance.CanRestartDungeon))) return;
            _choicePending = true;
            requested.Invoke();
        }

        public void ReleaseDefeatChoice() => _choicePending = false;

        private static bool IsDefeat(CombatResult result) => result != null && result.EndReason == CombatEndReason.Defeat;

        private void StopFieldRewardMessage()
        {
            if (_fieldRewardRoutine != null)
            {
                StopCoroutine(_fieldRewardRoutine);
                _fieldRewardRoutine = null;
            }

            if (simpleMessageText != null)
                simpleMessageText.text = string.Empty;
        }

        private void AddRewardRow(string text)
        {
            if (rewardRowRoot == null)
            {
                Debug.LogWarning($"{nameof(RewardUIPanel)} rewardRowRoot is not assigned.", this);
                return;
            }

            if (string.IsNullOrEmpty(text))
                return;

            GameObject row = new GameObject(text, typeof(RectTransform));
            row.transform.SetParent(rewardRowRoot, false);

            RectTransform rowRect = row.GetComponent<RectTransform>();
            if (rowRect != null)
                rowRect.sizeDelta = new Vector2(rowRect.sizeDelta.x, 32f);

            TMP_Text rowText = row.AddComponent<TextMeshProUGUI>();
            rowText.text = text;
            rowText.fontSize = 22f;
            rowText.color = Color.white;
            rowText.alignment = TextAlignmentOptions.Center;
            rowText.raycastTarget = false;

            _spawnedRows.Add(row);
        }

        private void AddConfiguredRewardRows(bool isWin)
        {
            string[] rewardLines = isWin ? victoryRewardLines : defeatRewardLines;
            if (rewardLines == null)
                return;

            for (int i = 0; i < rewardLines.Length; i++)
                AddRewardRow(rewardLines[i]);
        }

        private void AddGrantResultRows(bool isWin, RewardGrantResult grantResult)
        {
            if (!isWin)
            {
                AddConfiguredRewardRows(false);
                return;
            }

            if (grantResult.DuplicateBlocked)
            {
                AddRewardRow("Reward already granted.");
                return;
            }

            if (!grantResult.HasAnyReward)
            {
                AddRewardRow("No rewards granted.");
                return;
            }

            if (grantResult.Gold > 0)
                AddRewardRow($"Gold {grantResult.Gold}");

            if (grantResult.Exp > 0)
                AddRewardRow($"EXP {grantResult.Exp}");

            if (!string.IsNullOrEmpty(grantResult.ItemId) && grantResult.ItemCount > 0)
                AddRewardRow($"{grantResult.ItemId} x{grantResult.ItemCount}");
        }

        private void ClearRewardRows()
        {
            for (int i = 0; i < _spawnedRows.Count; i++)
            {
                if (_spawnedRows[i] != null)
                {
                    if (Application.isPlaying)
                        Destroy(_spawnedRows[i]);
                    else
                        DestroyImmediate(_spawnedRows[i]);
                }
            }

            _spawnedRows.Clear();
        }

        private void ApplyReward(CombatResult result)
        {
            // Legacy compatibility hook. Reward granting is owned by RewardService/RewardFlow.
        }

        private string BuildResultText(bool isWin)
        {
            return isWin ? victoryResultText : defeatResultText;
        }

        private static bool IsVictory(CombatResult result)
        {
            if (result == null)
                return false;

            return result.EndReason != CombatEndReason.None
                ? result.EndReason == CombatEndReason.Victory
                : result.IsWin;
        }

        private static void SetText(TMP_Text tmpText, Text legacyText, string value)
        {
            if (tmpText != null)
                tmpText.text = value;

            if (legacyText != null)
                legacyText.text = value;
        }

        private IEnumerator Co_ShowFieldRewardMessage(string message)
        {
            simpleMessageText.text = message;

            if (CanToggleOwnRoot())
                root.SetActive(true);

            yield return new WaitForSeconds(Mathf.Max(0.1f, fieldRewardAutoHideSeconds));

            simpleMessageText.text = string.Empty;

            if (_pendingResult == null && CanToggleOwnRoot())
                root.SetActive(false);

            _fieldRewardRoutine = null;
        }

        private bool CanToggleOwnRoot()
        {
            if (root == null) return false;
            GameUIRootController roots = FindFirstObjectByType<GameUIRootController>(FindObjectsInactive.Include);
            return roots == null || !roots.IsGlobalRoot(root);
        }
    }
}
