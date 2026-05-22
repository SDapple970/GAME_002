// Assets/GAME/Scripts/Story/Runtime/UI/ChoiceUIPanel.cs
using System.Collections.Generic;
using Game.Story.Core;
using Game.Story.Data;
using UnityEngine;

namespace Game.Story.UI
{
    public sealed class ChoiceUIPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private ChoiceButtonUI choiceButtonPrefab;

        private readonly List<ChoiceButtonUI> _spawnedButtons = new();

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        public void ShowChoices(IReadOnlyList<ChoiceDefinition> choices, DialogueRunner runner)
        {
            ClearButtons();

            if (contentRoot == null)
            {
                Debug.LogWarning("[ChoiceUIPanel] Content root is not assigned.");
                return;
            }

            if (choiceButtonPrefab == null)
            {
                Debug.LogWarning("[ChoiceUIPanel] Choice button prefab is not assigned.");
                return;
            }

            if (choices != null)
            {
                foreach (ChoiceDefinition choice in choices)
                {
                    ChoiceButtonUI button = Instantiate(choiceButtonPrefab, contentRoot);
                    bool interactable = choice != null && choice.AreConditionsMet();
                    button.Setup(choice, runner, interactable);
                    _spawnedButtons.Add(button);
                }
            }

            if (root != null)
            {
                root.SetActive(true);
            }
            else
            {
                Debug.LogWarning("[ChoiceUIPanel] Root is not assigned.");
            }

            IsOpen = true;
        }

        public void Hide()
        {
            if (root != null)
            {
                root.SetActive(false);
            }
            else
            {
                Debug.LogWarning("[ChoiceUIPanel] Root is not assigned.");
            }

            ClearButtons();
            IsOpen = false;
        }

        private void ClearButtons()
        {
            foreach (ChoiceButtonUI button in _spawnedButtons)
            {
                if (button != null)
                {
                    Destroy(button.gameObject);
                }
            }

            _spawnedButtons.Clear();
        }
    }
}
