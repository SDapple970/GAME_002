using System;
using System.Collections.Generic;
using Game.Daily;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class DaySettlementPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text rewardSummaryText;
        [SerializeField] private Transform rewardRowRoot;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button closeButton;

        [Header("Legacy / Optional")]
        [SerializeField] private Text titleLegacyText;
        [SerializeField] private Text rewardSummaryLegacyText;

        [Header("Settlement Flow")]
        [SerializeField] private DaySettlementFlow daySettlementFlow;
        [SerializeField] private bool subscribeToSettlementFlow;
        [SerializeField] private bool completeSettlementOnConfirm;

        public event Action OnConfirmed;
        public event Action OnClosed;

        private readonly List<GameObject> _spawnedRows = new();
        private bool _buttonsBound;
        private bool _shownWithoutDataWarned;

        private void Awake()
        {
            ResolveReferences();
            Hide();
        }

        private void OnEnable()
        {
            BindButtons();
            ResolveReferences();

            if (subscribeToSettlementFlow && daySettlementFlow != null)
                daySettlementFlow.OnSettlementReady += Show;
        }

        private void OnDisable()
        {
            if (daySettlementFlow != null)
                daySettlementFlow.OnSettlementReady -= Show;

            UnbindButtons();
        }

        public void Show(DaySettlementRequest request)
        {
            if (request == null)
            {
                WarnShownWithoutData();
                return;
            }

            ShowInternal(
                ResolveTitle(request.displayTitle, request.questId, request.missionId, request.settlementId),
                request.rewardDuplicateBlocked,
                request.rewardGold,
                request.rewardExp,
                request.rewardItemId,
                request.rewardItemCount);
        }

        public void Show(DaySettlementResult result)
        {
            if (result == null)
            {
                WarnShownWithoutData();
                return;
            }

            ShowInternal(
                ResolveTitle(result.displayTitle, result.questId, result.missionId, result.settlementId),
                result.rewardDuplicateBlocked,
                result.rewardGold,
                result.rewardExp,
                result.rewardItemId,
                result.rewardItemCount);
        }

        public void Hide()
        {
            ClearRewardRows();

            if (root != null)
                root.SetActive(false);
        }

        private void Confirm()
        {
            OnConfirmed?.Invoke();

            if (completeSettlementOnConfirm)
            {
                ResolveReferences();
                daySettlementFlow?.CompleteSettlement();
            }

            Hide();
        }

        private void Close()
        {
            OnClosed?.Invoke();
            Hide();
        }

        private void ShowInternal(
            string title,
            bool duplicateBlocked,
            int gold,
            int exp,
            string itemId,
            int itemCount)
        {
            BindButtons();
            ClearRewardRows();

            SetText(titleText, titleLegacyText, title);

            string summary = BuildRewardSummary(duplicateBlocked, gold, exp, itemId, itemCount);
            SetText(rewardSummaryText, rewardSummaryLegacyText, summary);
            AddRewardRows(duplicateBlocked, gold, exp, itemId, itemCount);

            if (root != null)
                root.SetActive(true);
        }

        private void AddRewardRows(bool duplicateBlocked, int gold, int exp, string itemId, int itemCount)
        {
            if (rewardRowRoot == null)
                return;

            if (duplicateBlocked)
            {
                AddRewardRow("Reward already granted.");
                return;
            }

            if (gold <= 0 && exp <= 0 && (string.IsNullOrWhiteSpace(itemId) || itemCount <= 0))
            {
                AddRewardRow("No rewards granted.");
                return;
            }

            if (gold > 0)
                AddRewardRow($"Gold {gold}");

            if (exp > 0)
                AddRewardRow($"EXP {exp}");

            if (!string.IsNullOrWhiteSpace(itemId) && itemCount > 0)
                AddRewardRow($"{itemId} x{itemCount}");
        }

        private void AddRewardRow(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || rewardRowRoot == null)
                return;

            GameObject row = new GameObject(text, typeof(RectTransform));
            row.transform.SetParent(rewardRowRoot, false);

            TMP_Text rowText = row.AddComponent<TextMeshProUGUI>();
            rowText.text = text;
            rowText.fontSize = 22f;
            rowText.color = Color.white;
            rowText.alignment = TextAlignmentOptions.Center;
            rowText.raycastTarget = false;

            _spawnedRows.Add(row);
        }

        private void ClearRewardRows()
        {
            for (int i = 0; i < _spawnedRows.Count; i++)
            {
                if (_spawnedRows[i] != null)
                    Destroy(_spawnedRows[i]);
            }

            _spawnedRows.Clear();
        }

        private void ResolveReferences()
        {
            if (daySettlementFlow == null)
                daySettlementFlow = DaySettlementFlow.Instance != null
                    ? DaySettlementFlow.Instance
                    : FindFirstObjectByType<DaySettlementFlow>(FindObjectsInactive.Include);
        }

        private void BindButtons()
        {
            if (_buttonsBound)
                return;

            if (confirmButton != null)
                confirmButton.onClick.AddListener(Confirm);

            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            _buttonsBound = true;
        }

        private void UnbindButtons()
        {
            if (!_buttonsBound)
                return;

            if (confirmButton != null)
                confirmButton.onClick.RemoveListener(Confirm);

            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);

            _buttonsBound = false;
        }

        private void WarnShownWithoutData()
        {
            if (_shownWithoutDataWarned)
                return;

            _shownWithoutDataWarned = true;
            Debug.LogWarning("[DaySettlementPanel] Show called without settlement data.", this);
        }

        private static string ResolveTitle(string displayTitle, string questId, string missionId, string settlementId)
        {
            if (!string.IsNullOrWhiteSpace(displayTitle))
                return displayTitle;

            if (!string.IsNullOrWhiteSpace(questId))
                return $"Quest Complete: {questId}";

            if (!string.IsNullOrWhiteSpace(missionId))
                return $"Mission Complete: {missionId}";

            if (!string.IsNullOrWhiteSpace(settlementId))
                return $"Settlement: {settlementId}";

            return "Day Settlement";
        }

        private static string BuildRewardSummary(bool duplicateBlocked, int gold, int exp, string itemId, int itemCount)
        {
            if (duplicateBlocked)
                return "Reward already granted.";

            List<string> parts = new();
            if (gold > 0)
                parts.Add($"Gold {gold}");
            if (exp > 0)
                parts.Add($"EXP {exp}");
            if (!string.IsNullOrWhiteSpace(itemId) && itemCount > 0)
                parts.Add($"{itemId} x{itemCount}");

            return parts.Count > 0 ? string.Join(", ", parts) : "No rewards granted.";
        }

        private static void SetText(TMP_Text tmpText, Text legacyText, string value)
        {
            if (tmpText != null)
                tmpText.text = value;

            if (legacyText != null)
                legacyText.text = value;
        }
    }
}
