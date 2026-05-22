// Assets/GAME/Scripts/Story/Runtime/Data/DialogueDefinitionSO.cs
using System.Collections.Generic;
using UnityEngine;

namespace Game.Story.Data
{
    [CreateAssetMenu(menuName = "GAME_002/Story/Dialogue Definition")]
    public sealed class DialogueDefinitionSO : ScriptableObject
    {
        [SerializeField] private string dialogueId;
        [SerializeField] private string displayName;
        [SerializeField] private List<DialogueLine> lines = new();
        [SerializeField] private List<ChoiceDefinition> choices = new();
        [SerializeField] private bool returnToExplorationWhenFinished = true;

        public string DialogueId => dialogueId;
        public string DisplayName => displayName;
        public IReadOnlyList<DialogueLine> Lines => lines;
        public IReadOnlyList<ChoiceDefinition> Choices => choices;
        public bool ReturnToExplorationWhenFinished => returnToExplorationWhenFinished;
    }
}
