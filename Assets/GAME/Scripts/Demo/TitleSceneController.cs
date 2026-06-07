using UnityEngine;
using UnityEngine.UI;

namespace Game.Demo
{
    public sealed class TitleSceneController : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private ContractDocumentPanel contractPanel;
        [SerializeField] private ContractDataSO contractData;

        private bool _buttonBound;

        private void Awake()
        {
            if (contractPanel != null)
                contractPanel.Close();
        }

        private void OnEnable()
        {
            BindButton();

            if (contractPanel != null)
                contractPanel.Close();
        }

        private void OnDisable()
        {
            UnbindButton();
        }

        private void HandleStartClicked()
        {
            if (contractPanel == null)
            {
                Debug.LogWarning("[TitleSceneController] ContractDocumentPanel is missing.", this);
                return;
            }

            contractPanel.Open(contractData);
        }

        private void BindButton()
        {
            if (_buttonBound)
                return;

            if (startButton != null)
                startButton.onClick.AddListener(HandleStartClicked);

            _buttonBound = true;
        }

        private void UnbindButton()
        {
            if (!_buttonBound)
                return;

            if (startButton != null)
                startButton.onClick.RemoveListener(HandleStartClicked);

            _buttonBound = false;
        }
    }
}
