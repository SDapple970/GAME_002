using System;
using Game.Systems.Persona;
using UnityEngine;

namespace Game.NonCombat.Choice
{
    public enum ChoiceOutcomeType
    {
        SetFlag,
        ClearFlag,
        AddPersonaExp,
        AddGold,
        RemoveGold,
        AddItem,
        RemoveItem,
        SetChapter,
        CompleteObjective
    }

    [Serializable]
    public sealed class ChoiceOutcome
    {
        [SerializeField] private ChoiceOutcomeType type;
        [SerializeField] private string id;
        [SerializeField] private PersonaStat personaStat;
        [SerializeField] private int amount;

        public ChoiceOutcomeType Type => type;
        public string Id => id;
        public PersonaStat PersonaStat => personaStat;
        public int Amount => amount;
    }
}
