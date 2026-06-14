using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Core;

namespace Game.DemoMission
{
    public sealed class DemoEndPanelController : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button titleButton;
        [SerializeField] private string titleMessage = "Title\uB85C";
        [SerializeField] private string titleSceneName = "Title";
        [SerializeField] private bool debugLogs = true;

        private bool _buttonBound;

        private void Awake()
        {
            if (root == null)
                root = gameObject;

            ApplyText();
            Hide();
            BindButton();
        }

        private void OnEnable()
        {
            BindButton();
        }

        private void OnDisable()
        {
            UnbindButton();
        }

        private void OnDestroy()
        {
            UnbindButton();
        }

        public void Show()
        {
            if (root == null)
                root = gameObject;

            root.SetActive(true);
            ApplyText();
            BindButton();

            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.UIOnly);

            Log("Demo end panel shown.");
        }

        public void Hide()
        {
            if (root != null)
                root.SetActive(false);
        }

        private void OnTitleButtonClicked()
        {
            if (string.IsNullOrWhiteSpace(titleSceneName))
            {
                Debug.LogWarning("[DemoEndPanelController] Title scene name is empty.", this);
                return;
            }

            Log($"Loading title scene '{titleSceneName}'.");
            SceneManager.LoadScene(titleSceneName);
        }

        private void ApplyText()
        {
            if (titleText != null)
                titleText.text = titleMessage;
        }

        private void BindButton()
        {
            if (_buttonBound)
                return;

            if (titleButton != null)
            {
                titleButton.onClick.AddListener(OnTitleButtonClicked);
                _buttonBound = true;
            }
            else
            {
                Debug.LogWarning("[DemoEndPanelController] Title button is not assigned.", this);
            }
        }

        private void UnbindButton()
        {
            if (!_buttonBound)
                return;

            if (titleButton != null)
                titleButton.onClick.RemoveListener(OnTitleButtonClicked);

            _buttonBound = false;
        }

        private void Log(string message)
        {
            if (!debugLogs)
                return;

            Debug.Log($"[DemoEndPanelController] {message}", this);
        }
    }
}
