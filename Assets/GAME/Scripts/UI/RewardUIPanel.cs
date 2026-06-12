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
            SetText(titleText, titleLegacyText, isWin ? "임무 완료" : "임무 실패");
            SetText(resultText, resultLegacyText, BuildResultText(result));

            if (isWin)
            {
                AddRewardRow("Gold 100");
                AddRewardRow("Potion 1");
            }

            if (root != null)
                root.SetActive(true);
        }

        public void Hide()
        {
            ClearRewardRows();

            if (root != null)
                root.SetActive(false);
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
            ApplyReward(_pendingResult);
            _pendingResult = null;

            Hide();
            OnClosed?.Invoke();
        }

        private void AddRewardRow(string text)
        {
            if (rewardRowRoot == null)
                return;

            GameObject row = new GameObject(text, typeof(RectTransform));
            row.transform.SetParent(rewardRowRoot, false);

            TMP_Text rowText = row.AddComponent<TextMeshProUGUI>();
            rowText.text = text;
            rowText.fontSize = 22f;
            rowText.color = Color.white;
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
            if (result == null)
                return;

            RewardApplier applier = rewardApplier != null ? rewardApplier : RewardApplier.Instance;
            if (applier != null)
                applier.ApplyCombatResult(result);
        }

        private static string BuildResultText(CombatResult result)
        {
            if (result == null)
                return string.Empty;

            if (result.IsWin)
                return "Gold 100\nPotion 1";

            return $"전투 종료 사유: {result.EndReason}";
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
