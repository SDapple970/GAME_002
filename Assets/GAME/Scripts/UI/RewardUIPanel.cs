using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Game.Combat.Model;
using Game.NonCombat.Reward;

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

        private readonly List<GameObject> _spawnedRows = new();
        private CombatResult _pendingResult;
        private bool _closeButtonBound;
        private Coroutine _fieldRewardRoutine;

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
        }

        public void Show(CombatResult result)
        {
            BindCloseButton();
            _pendingResult = result;
            ClearRewardRows();

            bool isWin = result != null && result.IsWin;
            SetText(titleText, titleLegacyText, isWin ? victoryTitle : defeatTitle);
            SetText(resultText, resultLegacyText, showResultText ? BuildResultText(isWin) : string.Empty);

            string[] rewardLines = isWin ? victoryRewardLines : defeatRewardLines;
            if (rewardLines != null)
                for (int i = 0; i < rewardLines.Length; i++)
                    AddRewardRow(rewardLines[i]);

            if (root != null)
                root.SetActive(true);
        }

        public void Hide()
        {
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

            if (_fieldRewardRoutine != null)
                StopCoroutine(_fieldRewardRoutine);

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
            _pendingResult = null;

            Hide();
            OnClosed?.Invoke();
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

        private void ClearRewardRows()
        {
            for (int i = 0; i < _spawnedRows.Count; i++)
            {
                if (_spawnedRows[i] != null)
                    Destroy(_spawnedRows[i]);
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
