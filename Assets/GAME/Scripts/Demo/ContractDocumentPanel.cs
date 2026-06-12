using System.Collections;
using Game.Quest;
using Game.Story;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Demo
{
    public sealed class ContractDocumentPanel : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private string openTriggerName = "Open";
        [SerializeField] private string stampTriggerName = "Stamp";
        [SerializeField] private float sceneChangeDelayAfterStamp = 0.8f;

        [Header("Text")]
        [SerializeField] private Text titleText;
        [SerializeField] private TMP_Text titleTmpText;
        [SerializeField] private Text objectiveText;
        [SerializeField] private TMP_Text objectiveTmpText;
        [SerializeField] private Text monsterDescriptionText;
        [SerializeField] private TMP_Text monsterDescriptionTmpText;
        [SerializeField] private Text rescueNpcDescriptionText;
        [SerializeField] private TMP_Text rescueNpcDescriptionTmpText;

        [Header("Images")]
        [SerializeField] private Image monsterImage;
        [SerializeField] private Image rescueNpcImage;

        [Header("Buttons")]
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button closeButton;

        [Header("State")]
        [SerializeField] private ContractDataSO currentContract;

        private bool _buttonsBound;
        private bool _acceptInProgress;
        private Coroutine _acceptRoutine;

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

        public void Open(ContractDataSO contractData)
        {
            currentContract = contractData;
            _acceptInProgress = false;

            if (acceptButton != null)
                acceptButton.interactable = true;

            if (closeButton != null)
                closeButton.interactable = true;

            Refresh(contractData);

            if (panelRoot != null)
                panelRoot.SetActive(true);
            else
                gameObject.SetActive(true);

            SetTrigger(openTriggerName);
        }

        public void Close()
        {
            _acceptInProgress = false;

            if (_acceptRoutine != null)
            {
                StopCoroutine(_acceptRoutine);
                _acceptRoutine = null;
            }

            if (acceptButton != null)
                acceptButton.interactable = true;

            if (closeButton != null)
                closeButton.interactable = true;

            if (panelRoot != null)
                panelRoot.SetActive(false);
            else
                gameObject.SetActive(false);
        }

        private void Refresh(ContractDataSO contractData)
        {
            if (contractData == null)
            {
                SetText(titleText, titleTmpText, "No Contract");
                SetText(objectiveText, objectiveTmpText, string.Empty);
                SetText(monsterDescriptionText, monsterDescriptionTmpText, string.Empty);
                SetText(rescueNpcDescriptionText, rescueNpcDescriptionTmpText, string.Empty);
                SetImage(monsterImage, null);
                SetImage(rescueNpcImage, null);
                return;
            }

            SetText(titleText, titleTmpText, contractData.ContractTitle);
            SetText(objectiveText, objectiveTmpText, contractData.ObjectiveText);
            SetText(monsterDescriptionText, monsterDescriptionTmpText, contractData.MonsterDescription);
            SetText(rescueNpcDescriptionText, rescueNpcDescriptionTmpText, contractData.RescueNpcDescription);
            SetImage(monsterImage, contractData.MonsterPortrait);
            SetImage(rescueNpcImage, contractData.RescueNpcPortrait);
        }

        private void HandleAcceptClicked()
        {
            if (_acceptInProgress)
                return;

            if (currentContract == null)
            {
                Debug.LogWarning("[ContractDocumentPanel] Accept ignored. Contract data is missing.", this);
                return;
            }

            _acceptInProgress = true;

            if (acceptButton != null)
                acceptButton.interactable = false;

            if (closeButton != null)
                closeButton.interactable = false;

            _acceptRoutine = StartCoroutine(Co_AcceptContract());
        }

        private IEnumerator Co_AcceptContract()
        {
            SetTrigger(stampTriggerName);
            StartQuestIfAssigned(currentContract);

            if (sceneChangeDelayAfterStamp > 0f)
                yield return new WaitForSeconds(sceneChangeDelayAfterStamp);

            TravelToContractScene(currentContract);
            _acceptRoutine = null;
        }

        private void StartQuestIfAssigned(ContractDataSO contractData)
        {
            if (contractData == null || contractData.StartQuest == null)
                return;

            QuestManager questManager = QuestManager.Instance != null
                ? QuestManager.Instance
                : FindFirstObjectByType<QuestManager>();

            if (questManager != null)
                questManager.StartQuest(contractData.StartQuest);
            else
                Debug.LogWarning("[ContractDocumentPanel] QuestManager is missing.", this);
        }

        private void TravelToContractScene(ContractDataSO contractData)
        {
            if (contractData == null || string.IsNullOrWhiteSpace(contractData.TargetSceneName))
            {
                Debug.LogWarning("[ContractDocumentPanel] Target scene name is empty.", this);
                return;
            }

            SceneTravelService.TravelTo(contractData.TargetSceneName, contractData.TargetSpawnPointId);
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

        private void SetTrigger(string triggerName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(triggerName))
                return;

            animator.SetTrigger(triggerName);
        }

        private static void SetText(Text text, TMP_Text tmpText, string value)
        {
            if (text != null)
                text.text = value;

            if (tmpText != null)
                tmpText.text = value;
        }

        private static void SetImage(Image image, Sprite sprite)
        {
            if (image == null)
                return;

            image.sprite = sprite;
            image.enabled = sprite != null;
        }
    }
}
