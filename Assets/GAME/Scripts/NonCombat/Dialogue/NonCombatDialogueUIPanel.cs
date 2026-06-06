using System;
using Game.NonCombat.Choice;
using UnityEngine;
using UnityEngine.UI;

namespace Game.NonCombat.Dialogue
{
    public sealed class NonCombatDialogueUIPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text speakerText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Transform choiceRoot;
        [SerializeField] private Button choiceButtonPrefab;

        public event Action<DialogueChoice> ChoiceSelected;

        private void Awake()
        {
            if (choiceButtonPrefab != null && choiceRoot != null && choiceButtonPrefab.transform.parent == choiceRoot)
                choiceButtonPrefab.gameObject.SetActive(false);

            Hide();
        }

        public void Show(DialogueNodeSO node, ChoiceRunner choiceRunner)
        {
            if (node == null) return;
            if (panelRoot != null) panelRoot.SetActive(true);
            if (speakerText != null) speakerText.text = node.SpeakerName;
            if (bodyText != null) bodyText.text = node.BodyText;

            RebuildChoices(node, choiceRunner);
        }

        public void Hide()
        {
            ClearChoices();
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void RebuildChoices(DialogueNodeSO node, ChoiceRunner choiceRunner)
        {
            ClearChoices();
            if (choiceRoot == null || choiceButtonPrefab == null)
                return;

            DialogueChoice[] choices = node.Choices;
            if (choices == null || choices.Length == 0)
            {
                Button continueButton = Instantiate(choiceButtonPrefab, choiceRoot);
                continueButton.gameObject.SetActive(true);
                SetButtonText(continueButton, node.NextNodeWhenNoChoice != null ? "Continue" : "End");
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(() => ChoiceSelected?.Invoke(null));
                return;
            }

            for (int i = 0; i < choices.Length; i++)
            {
                DialogueChoice choice = choices[i];
                bool available = choiceRunner == null || choiceRunner.AreConditionsMet(choice.Conditions);
                if (!available && choice.HideWhenUnavailable)
                    continue;

                Button button = Instantiate(choiceButtonPrefab, choiceRoot);
                button.gameObject.SetActive(true);
                SetButtonText(button, string.IsNullOrEmpty(choice.DisplayText) ? "(Choice)" : choice.DisplayText);
                button.interactable = available;
                button.onClick.RemoveAllListeners();
                DialogueChoice captured = choice;
                button.onClick.AddListener(() => ChoiceSelected?.Invoke(captured));
            }
        }

        private void ClearChoices()
        {
            if (choiceRoot == null) return;

            for (int i = choiceRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = choiceRoot.GetChild(i);
                if (choiceButtonPrefab != null && child == choiceButtonPrefab.transform)
                    continue;

                Destroy(child.gameObject);
            }
        }

        private static void SetButtonText(Button button, string text)
        {
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
                label.text = text;
        }
    }
}
