using System;
using Game.NonCombat.Choice;
using UnityEngine;

namespace Game.NonCombat.Dialogue
{
    [Serializable]
    public sealed class DialogueChoice
    {
        [SerializeField] private string displayText;
        [SerializeField] private ChoiceCondition[] conditions;
        [SerializeField] private ChoiceOutcome[] outcomes;
        [SerializeField] private DialogueNodeSO nextNode;
        [SerializeField] private bool hideWhenUnavailable;

        public string DisplayText => displayText;
        public ChoiceCondition[] Conditions => conditions;
        public ChoiceOutcome[] Outcomes => outcomes;
        public DialogueNodeSO NextNode => nextNode;
        public bool HideWhenUnavailable => hideWhenUnavailable;
    }
}
