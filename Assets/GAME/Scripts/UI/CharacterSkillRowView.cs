using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class CharacterSkillRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionLabel;

        public void Bind(string displayName, string status, string action, bool interactable, Action callback)
        {
            nameLabel.text = displayName;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;
            nameLabel.maxVisibleLines = 2;
            statusLabel.text = status;
            statusLabel.overflowMode = TextOverflowModes.Ellipsis;
            statusLabel.maxVisibleLines = 2;
            actionLabel.text = action;
            actionButton.onClick.RemoveAllListeners();
            actionButton.gameObject.SetActive(callback != null);
            actionButton.interactable = interactable;
            if (callback != null) actionButton.onClick.AddListener(() => callback());
        }
    }
}
