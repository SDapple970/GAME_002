using Game.Quest;
using Game.Story;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Office
{
    public sealed class CaseFileDocumentPanel : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject panelRoot;

        [Header("Text")]
        [SerializeField] private Text titleText;
        [SerializeField] private TMP_Text titleTmpText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private TMP_Text descriptionTmpText;
        [SerializeField] private Text clientText;
        [SerializeField] private TMP_Text clientTmpText;
        [SerializeField] private Text locationText;
        [SerializeField] private TMP_Text locationTmpText;
        [SerializeField] private Text objectiveText;
        [SerializeField] private TMP_Text objectiveTmpText;
        [SerializeField] private Text rewardText;
        [SerializeField] private TMP_Text rewardTmpText;

        [Header("Buttons")]
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button closeButton;

        private CaseFileDataSO _currentCaseFile;
        private bool _buttonsBound;

        private void Awake()
        {
            BindButtons();
            Close();
        }

        private void OnEnable()
        {
            BindButtons();
        }

        private void OnDisable()
        {
            UnbindButtons();
        }

        public void Open(CaseFileDataSO caseFile)
        {
            _currentCaseFile = caseFile;
            Refresh(caseFile);

            if (panelRoot != null)
                panelRoot.SetActive(true);
            else
                gameObject.SetActive(true);
        }

        public void Close()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
            else
                gameObject.SetActive(false);
        }

        private void Refresh(CaseFileDataSO caseFile)
        {
            if (caseFile == null)
            {
                SetText(titleText, titleTmpText, "No Case File");
                SetText(descriptionText, descriptionTmpText, string.Empty);
                SetText(clientText, clientTmpText, string.Empty);
                SetText(locationText, locationTmpText, string.Empty);
                SetText(objectiveText, objectiveTmpText, string.Empty);
                SetText(rewardText, rewardTmpText, string.Empty);
                return;
            }

            SetText(titleText, titleTmpText, caseFile.CaseTitle);
            SetText(descriptionText, descriptionTmpText, caseFile.Description);
            SetText(clientText, clientTmpText, caseFile.ClientName);
            SetText(locationText, locationTmpText, caseFile.LocationName);
            SetText(objectiveText, objectiveTmpText, caseFile.ObjectivePreview);
            SetText(rewardText, rewardTmpText, caseFile.RewardPreview);
        }

        private void HandleAcceptClicked()
        {
            if (_currentCaseFile == null)
            {
                Debug.LogWarning("[CaseFileDocumentPanel] Accept ignored. Case file is not assigned.", this);
                return;
            }

            if (!_currentCaseFile.Unlocked)
            {
                Debug.Log($"[CaseFileDocumentPanel] Case is locked: {_currentCaseFile.CaseTitle}", this);
                return;
            }

            ChapterProgressManager chapterManager = ChapterProgressManager.Instance != null
                ? ChapterProgressManager.Instance
                : FindFirstObjectByType<ChapterProgressManager>();

            if (chapterManager != null)
            {
                chapterManager.StartChapter(_currentCaseFile.ChapterId);
                chapterManager.SetStep(0);
            }
            else
            {
                Debug.LogWarning("[CaseFileDocumentPanel] ChapterProgressManager is missing.", this);
            }

            if (_currentCaseFile.StartQuest != null)
            {
                QuestManager questManager = QuestManager.Instance != null
                    ? QuestManager.Instance
                    : FindFirstObjectByType<QuestManager>();

                if (questManager != null)
                    questManager.StartQuest(_currentCaseFile.StartQuest);
                else
                    Debug.LogWarning("[CaseFileDocumentPanel] QuestManager is missing.", this);
            }

            Close();
            SceneTravelService.TravelTo(_currentCaseFile.TargetSceneName, _currentCaseFile.TargetSpawnPointId);
        }

        private void BindButtons()
        {
            if (_buttonsBound)
                return;

            if (acceptButton != null)
                acceptButton.onClick.AddListener(HandleAcceptClicked);

            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            _buttonsBound = true;
        }

        private void UnbindButtons()
        {
            if (!_buttonsBound)
                return;

            if (acceptButton != null)
                acceptButton.onClick.RemoveListener(HandleAcceptClicked);

            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);

            _buttonsBound = false;
        }

        private static void SetText(Text text, TMP_Text tmpText, string value)
        {
            if (text != null)
                text.text = value;

            if (tmpText != null)
                tmpText.text = value;
        }
    }
}
