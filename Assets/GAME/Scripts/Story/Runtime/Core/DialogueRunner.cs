// Assets/GAME/Scripts/Story/Runtime/Core/DialogueRunner.cs
using System;
using System.Collections.Generic;
using Game.Core;
using Game.Story.Data;
using Game.Story.UI;
using UnityEngine;

namespace Game.Story.Core
{
    public sealed class DialogueRunner : MonoBehaviour
    {
        public static DialogueRunner Instance { get; private set; }

        [SerializeField] private DialogueUIPanel dialogueUIPanel;
        [SerializeField] private ChoiceUIPanel choiceUIPanel;
        [SerializeField] private bool useCutsceneState = true;

        private DialogueDefinitionSO _currentDialogue;
        private int _lineIndex;
        private bool _isRunning;

        public bool IsRunning => _isRunning;
        public DialogueDefinitionSO CurrentDialogue => _currentDialogue;

        public event Action<DialogueDefinitionSO> OnDialogueStarted;
        public event Action<DialogueLine> OnLineChanged;
        public event Action<IReadOnlyList<ChoiceDefinition>> OnChoicesShown;
        public event Action<DialogueDefinitionSO> OnDialogueEnded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[DialogueRunner] Multiple DialogueRunner instances exist. Keeping the latest scene instance reference.");
            }

            Instance = this;
            dialogueUIPanel?.Bind(this);
        }

        public void StartDialogue(DialogueDefinitionSO dialogue)
        {
            if (dialogue == null)
            {
                Debug.LogWarning("[DialogueRunner] Cannot start a null dialogue.");
                return;
            }

            if (_isRunning)
            {
                Debug.LogWarning("[DialogueRunner] Dialogue is already running.");
                return;
            }

            BeginDialogue(dialogue, true);
        }

        public void Advance()
        {
            if (!_isRunning) return;
            if (choiceUIPanel != null && choiceUIPanel.IsOpen) return;

            _lineIndex++;

            IReadOnlyList<DialogueLine> lines = _currentDialogue?.Lines;
            if (lines != null && _lineIndex < lines.Count)
            {
                DialogueLine line = lines[_lineIndex];
                if (dialogueUIPanel != null)
                {
                    dialogueUIPanel.ShowLine(line);
                }
                else
                {
                    Debug.LogWarning("[DialogueRunner] DialogueUIPanel is not assigned.");
                }

                if (line != null && !string.IsNullOrEmpty(line.OptionalFlagToSet))
                {
                    if (StoryFlagManager.Instance != null)
                    {
                        StoryFlagManager.Instance.SetBool(line.OptionalFlagToSet, line.OptionalFlagValue);
                    }
                    else
                    {
                        Debug.LogWarning("[DialogueRunner] StoryFlagManager is missing. Optional line flag was not set.");
                    }
                }

                OnLineChanged?.Invoke(line);
                return;
            }

            IReadOnlyList<ChoiceDefinition> choices = _currentDialogue?.Choices;
            if (choices != null && choices.Count > 0)
            {
                if (choiceUIPanel != null)
                {
                    choiceUIPanel.ShowChoices(choices, this);
                }
                else
                {
                    Debug.LogWarning("[DialogueRunner] ChoiceUIPanel is not assigned.");
                }

                OnChoicesShown?.Invoke(choices);
                return;
            }

            EndDialogue();
        }

        public void SelectChoice(ChoiceDefinition choice)
        {
            if (choice == null) return;

            if (!choice.AreConditionsMet())
            {
                Debug.LogWarning("[DialogueRunner] Choice conditions are not met.");
                return;
            }

            choice.ApplyResults();
            choiceUIPanel?.Hide();

            if (choice.NextDialogue != null)
            {
                BeginDialogue(choice.NextDialogue, false);
                return;
            }

            if (choice.CloseAfterSelect)
            {
                EndDialogue();
            }
        }

        public void EndDialogue()
        {
            if (!_isRunning) return;

            DialogueDefinitionSO endedDialogue = _currentDialogue;
            dialogueUIPanel?.Hide();
            choiceUIPanel?.Hide();
            OnDialogueEnded?.Invoke(endedDialogue);

            if (endedDialogue != null && endedDialogue.ReturnToExplorationWhenFinished && GameStateMachine.Instance != null)
            {
                GameStateMachine.Instance.SetState(GameState.Exploration);
            }

            _currentDialogue = null;
            _lineIndex = -1;
            _isRunning = false;
        }

        private void BeginDialogue(DialogueDefinitionSO dialogue, bool changeState)
        {
            _currentDialogue = dialogue;
            _lineIndex = -1;
            _isRunning = true;

            if (changeState && GameStateMachine.Instance != null)
            {
                GameStateMachine.Instance.SetState(useCutsceneState ? GameState.Cutscene : GameState.UIOnly);
            }

            if (dialogueUIPanel != null)
            {
                dialogueUIPanel.Bind(this);
                dialogueUIPanel.Show();
            }
            else
            {
                Debug.LogWarning("[DialogueRunner] DialogueUIPanel is not assigned.");
            }

            choiceUIPanel?.Hide();
            OnDialogueStarted?.Invoke(dialogue);
            Advance();
        }
    }
}
