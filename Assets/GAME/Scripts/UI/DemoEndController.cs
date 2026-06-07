using Game.Story;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class DemoEndController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject panelRoot;

        [Header("Text")]
        [SerializeField] private Text messageText;
        [SerializeField] private TMP_Text messageTmpText;
        [SerializeField] private string message = "Demo End";

        [Header("Buttons")]
        [SerializeField] private Button restartDemoButton;
        [SerializeField] private Button quitGameButton;
        [SerializeField] private Button returnButton;

        [Header("Return Destination")]
        [SerializeField] private string returnSceneName;
        [SerializeField] private string returnSpawnPointId;
        [SerializeField] private bool useSceneTravelServiceForReturn = true;

        [Header("Restart Destination")]
        [SerializeField] private string restartSceneName;

        [Header("State")]
        [SerializeField] private bool pauseGameOnShow = true;

        private bool _buttonsBound;

        private void Awake()
        {
            BindButtons();
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            BindButtons();
        }

        private void OnDisable()
        {
            UnbindButtons();
        }

        public void ShowDemoEnd()
        {
            SetText(messageText, messageTmpText, message);

            if (panelRoot != null)
                panelRoot.SetActive(true);
            else
                gameObject.SetActive(true);

            if (pauseGameOnShow)
                Time.timeScale = 0f;
        }

        private void RestartDemo()
        {
            RestoreTimeScale();
            string sceneName = string.IsNullOrEmpty(restartSceneName)
                ? SceneManager.GetActiveScene().name
                : restartSceneName;

            SceneManager.LoadScene(sceneName);
        }

        private void Return()
        {
            RestoreTimeScale();

            if (panelRoot != null)
                panelRoot.SetActive(false);

            if (string.IsNullOrWhiteSpace(returnSceneName))
            {
                Debug.LogWarning("[DemoEndController] Return scene name is empty.", this);
                return;
            }

            if (useSceneTravelServiceForReturn)
                SceneTravelService.TravelTo(returnSceneName, returnSpawnPointId);
            else
                SceneManager.LoadScene(returnSceneName);
        }

        private void QuitGame()
        {
            RestoreTimeScale();
            Application.Quit();

#if UNITY_EDITOR
            Debug.Log("[DemoEndController] Quit Game requested.", this);
#endif
        }

        private void BindButtons()
        {
            if (_buttonsBound)
                return;

            if (restartDemoButton != null)
                restartDemoButton.onClick.AddListener(RestartDemo);

            if (quitGameButton != null)
                quitGameButton.onClick.AddListener(QuitGame);

            if (returnButton != null)
                returnButton.onClick.AddListener(Return);

            _buttonsBound = true;
        }

        private void UnbindButtons()
        {
            if (!_buttonsBound)
                return;

            if (restartDemoButton != null)
                restartDemoButton.onClick.RemoveListener(RestartDemo);

            if (quitGameButton != null)
                quitGameButton.onClick.RemoveListener(QuitGame);

            if (returnButton != null)
                returnButton.onClick.RemoveListener(Return);

            _buttonsBound = false;
        }

        private static void RestoreTimeScale()
        {
            if (Time.timeScale == 0f)
                Time.timeScale = 1f;
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
