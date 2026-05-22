// Assets/GAME/Scripts/Story/Runtime/Data/ChoiceDefinition.cs
using System.Collections.Generic;
using UnityEngine;

namespace Game.Story.Data
{
    [System.Serializable]
    public sealed class ChoiceDefinition
    {
        [SerializeField] private string choiceText;
        [SerializeField] private List<ChoiceCondition> conditions = new();
        [SerializeField] private List<ChoiceResult> results = new();
        [SerializeField] private DialogueDefinitionSO nextDialogue;
        [SerializeField] private bool closeAfterSelect = true;

        public string ChoiceText => choiceText;
        public IReadOnlyList<ChoiceCondition> Conditions => conditions;
        public IReadOnlyList<ChoiceResult> Results => results;
        public DialogueDefinitionSO NextDialogue => nextDialogue;
        public bool CloseAfterSelect => closeAfterSelect;

        public bool AreConditionsMet()
        {
            if (conditions == null || conditions.Count == 0) return true;

            foreach (ChoiceCondition condition in conditions)
            {
                if (condition != null && !condition.IsMet()) return false;
            }

            return true;
        }

        public void ApplyResults()
        {
            if (results == null) return;

            foreach (ChoiceResult result in results)
            {
                result?.Apply();
            }
        }
    }
}
