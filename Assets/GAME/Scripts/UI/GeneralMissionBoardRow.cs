using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class GeneralMissionBoardRow : MonoBehaviour
    {
        [SerializeField] private GeneralMissionBoard board;
        [SerializeField] private int missionIndex;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button acceptButton;
        private void OnEnable()
        {
            if (board != null) board.OnChanged += Refresh;
            if (acceptButton != null) acceptButton.onClick.AddListener(Accept);
            Refresh();
        }
        private void OnDisable()
        {
            if (board != null) board.OnChanged -= Refresh;
            if (acceptButton != null) acceptButton.onClick.RemoveListener(Accept);
        }
        private void Accept() { if (board != null) board.Accept(missionIndex); }
        private void Refresh() { if (statusText != null && board != null) statusText.text = board.GetProgressText(missionIndex); }
    }
}
