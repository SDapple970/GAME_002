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

        [Header("Legacy / Optional")]
        [SerializeField] private Text titleLegacyText;
        [SerializeField] private Text resultLegacyText;
        [SerializeField] private Text simpleMessageText;
        [SerializeField] private float fieldRewardAutoHideSeconds = 1.5f;
        [SerializeField] private RewardApplier rewardApplier;

        [Header("Combat Result")]
        [SerializeField] private string victoryTitle = "전투 승리";
        [SerializeField] private string defeatTitle = "전투 패배";
        [SerializeField] private string[] victoryRewardLines = { "Gold 100", "Potion 1" };
        [SerializeField] private string[] defeatRewardLines = { "획득 보상 없음" };
        [SerializeField] private bool showResultText = false;
        [SerializeField] private string victoryResultText = "";
        [SerializeField] private string defeatResultText = "";

        public event Action OnClosed;

        public bool IsOpen => _presentationOpen;
        public bool HasActiveCombatResult => _presentationOpen && _pendingResult != null;

        private readonly List<GameObject> _spawnedRows = new();
        private CombatResult _pendingResult;
        private bool _closeButtonBound;
        private Coroutine _fieldRewardRoutine;
        private bool _presentationOpen;
        private bool _closeRaised;

        private void Awake()
        {
            if (rewardApplier == null)
                rewardApplier = RewardApplier.Instance;

            Hide();
        }

        private void OnEnable()
        {
            BindCloseButton();
        }

        private void OnDisable()
        {
            UnbindCloseButton();
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
            StopFieldRewardMessage();
            _pendingResult = result;
            _presentationOpen = true;
            _closeRaised = false;
            ClearRewardRows();

            bool isWin = IsVictory(result);
            SetText(titleText, titleLegacyText, isWin ? victoryTitle : defeatTitle);
            SetText(resultText, resultLegacyText, showResultText ? BuildResultText(isWin) : string.Empty);

            if (preferGrantResult)
                AddGrantResultRows(isWin, grantResult);
            else
                AddConfiguredRewardRows(isWin);

            if (root != null)
                root.SetActive(true);
        }

        public void Hide()
        {
            StopFieldRewardMessage();
            _pendingResult = null;
            _presentationOpen = false;
            _closeRaised = true;
            ClearRewardRows();

            if (root != null)
                root.SetActive(false);

            if (simpleMessageText != null)
                simpleMessageText.text = string.Empty;
        }

        public bool TryShowFieldRewardMessage(string message)
        {
            if (string.IsNullOrEmpty(message) || simpleMessageText == null)
                return false;

            if (_presentationOpen)
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
            if (!_presentationOpen || _closeRaised)
                return;

            _closeRaised = true;
            _presentationOpen = false;
            _pendingResult = null;

            StopFieldRewardMessage();
            ClearRewardRows();
            if (root != null)
                root.SetActive(false);
            if (simpleMessageText != null)
                simpleMessageText.text = string.Empty;

            OnClosed?.Invoke();
        }

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

            if (root != null)
                root.SetActive(true);

            yield return new WaitForSeconds(Mathf.Max(0.1f, fieldRewardAutoHideSeconds));

            simpleMessageText.text = string.Empty;

            if (_pendingResult == null && root != null)
                root.SetActive(false);

            _fieldRewardRoutine = null;
        }
    }
}
