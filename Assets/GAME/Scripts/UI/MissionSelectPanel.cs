using System;
using System.Collections.Generic;
using Game.Office;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class MissionSelectPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject root;
        [SerializeField] private Transform missionRowRoot;
        [SerializeField] private Button missionButtonTemplate;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text emptyText;
        [SerializeField] private Button closeButton;

        [Header("Legacy / Optional")]
        [SerializeField] private Text titleLegacyText;
        [SerializeField] private Text emptyLegacyText;

        [Header("Mission Flow")]
        [SerializeField] private MissionSelectFlow missionSelectFlow;
        [SerializeField] private bool subscribeToMissionSelectFlow;
        [SerializeField] private bool forwardSelectionToFlow;

        public event Action<string> OnMissionSelected;
        public event Action OnClosed;

        private readonly List<GameObject> _spawnedRows = new();
        private bool _closeButtonBound;

        private void Awake()
        {
            ResolveReferences();
            Hide();
        }

        private void OnEnable()
        {
            BindCloseButton();
            ResolveReferences();

            if (subscribeToMissionSelectFlow && missionSelectFlow != null)
                missionSelectFlow.OnMissionListReady += Show;
        }

        private void OnDisable()
        {
            if (missionSelectFlow != null)
                missionSelectFlow.OnMissionListReady -= Show;

            UnbindCloseButton();
        }

        public void Show(IReadOnlyList<MissionEntry> missions)
        {
            ClearRows();
            SetText(titleText, titleLegacyText, "Mission Select");

            bool hasMissions = missions != null && missions.Count > 0;
            SetEmptyVisible(!hasMissions);

            if (hasMissions)
            {
                for (int i = 0; i < missions.Count; i++)
                    AddMissionRow(missions[i]);
            }

            if (root != null)
                root.SetActive(true);
            else
                gameObject.SetActive(true);
        }

        public void ShowFromFlow()
        {
            ResolveReferences();
            if (missionSelectFlow != null)
                Show(missionSelectFlow.GetAvailableMissions());
            else
                Show(null);
        }

        public void Hide()
        {
            ClearRows();

            if (root != null)
                root.SetActive(false);
            else
                gameObject.SetActive(false);
        }

        private void AddMissionRow(MissionEntry mission)
        {
            if (mission == null || missionRowRoot == null)
                return;

            Button button = CreateMissionButton(mission.Title);
            if (button == null)
                return;

            string missionId = mission.MissionId;
            button.onClick.AddListener(() => SelectMission(missionId));
            _spawnedRows.Add(button.gameObject);
        }

        private Button CreateMissionButton(string label)
        {
            Button button;
            if (missionButtonTemplate != null)
            {
                button = Instantiate(missionButtonTemplate, missionRowRoot);
                button.gameObject.SetActive(true);
            }
            else
            {
                GameObject row = new GameObject(string.IsNullOrWhiteSpace(label) ? "Mission" : label, typeof(RectTransform));
                row.transform.SetParent(missionRowRoot, false);
                button = row.AddComponent<Button>();

                TMP_Text rowText = row.AddComponent<TextMeshProUGUI>();
                rowText.text = label;
                rowText.fontSize = 22f;
                rowText.color = Color.white;
                rowText.alignment = TextAlignmentOptions.Center;
                rowText.raycastTarget = false;
            }

            SetButtonLabel(button, label);
            return button;
        }

        private void SelectMission(string missionId)
        {
            OnMissionSelected?.Invoke(missionId);

            if (forwardSelectionToFlow)
            {
                ResolveReferences();
                missionSelectFlow?.SelectMission(missionId);
            }
        }

        private void Close()
        {
            OnClosed?.Invoke();
            Hide();
        }

        private void ClearRows()
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
            if (missionSelectFlow == null)
                missionSelectFlow = FindFirstObjectByType<MissionSelectFlow>(FindObjectsInactive.Include);
        }

        private void BindCloseButton()
        {
            if (_closeButtonBound)
                return;

            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            _closeButtonBound = true;
        }

        private void UnbindCloseButton()
        {
            if (!_closeButtonBound)
                return;

            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);

            _closeButtonBound = false;
        }

        private void SetEmptyVisible(bool visible)
        {
            SetText(emptyText, emptyLegacyText, visible ? "No missions available." : string.Empty);
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
                return;

            TMP_Text tmpText = button.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
                tmpText.text = label;

            Text legacyText = button.GetComponentInChildren<Text>();
            if (legacyText != null)
                legacyText.text = label;
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
