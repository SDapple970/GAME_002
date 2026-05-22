// Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionConfirmUI.cs
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Story.Interaction
{
    public sealed class StoryInteractionConfirmUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        private Action _onYes;
        private Action _onNo;

        private void Awake()
        {
            if (rootGroup != null || root != null)
            {
                Hide();
            }
        }

        private void OnEnable()
        {
            SubscribeButtons();
        }

        private void OnDisable()
        {
            UnsubscribeButtons();
        }

        public void Configure(CanvasGroup group, GameObject rootObject, TMP_Text message, Button yes, Button no)
        {
            rootGroup = group;
            root = rootObject;
            messageText = message;
            yesButton = yes;
            noButton = no;
            SubscribeButtons();
            Hide();
        }

        public void Show(string message, Action onYes, Action onNo)
        {
            _onYes = onYes;
            _onNo = onNo;

            if (messageText != null)
            {
                messageText.text = string.IsNullOrEmpty(message) ? "사용할까요?" : message;
            }
            else
            {
                Debug.LogWarning("[StoryInteractionConfirmUI] Message text is not assigned.");
            }

            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
            _onYes = null;
            _onNo = null;
        }

        private void SubscribeButtons()
        {
            if (yesButton != null)
            {
                yesButton.onClick.RemoveListener(HandleYesClicked);
                yesButton.onClick.AddListener(HandleYesClicked);
            }

            if (noButton != null)
            {
                noButton.onClick.RemoveListener(HandleNoClicked);
                noButton.onClick.AddListener(HandleNoClicked);
            }
        }

        private void UnsubscribeButtons()
        {
            if (yesButton != null)
            {
                yesButton.onClick.RemoveListener(HandleYesClicked);
            }

            if (noButton != null)
            {
                noButton.onClick.RemoveListener(HandleNoClicked);
            }
        }

        private void SetVisible(bool visible)
        {
            if (rootGroup != null)
            {
                rootGroup.alpha = visible ? 1f : 0f;
                rootGroup.interactable = visible;
                rootGroup.blocksRaycasts = visible;
                return;
            }

            if (root != null)
            {
                root.SetActive(visible);
                return;
            }

            Debug.LogWarning("[StoryInteractionConfirmUI] Root group or root GameObject is not assigned.");
        }

        private void HandleYesClicked()
        {
            _onYes?.Invoke();
        }

        private void HandleNoClicked()
        {
            _onNo?.Invoke();
        }
    }
}
