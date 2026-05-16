using System;
using Game.Systems.Persona;
using UnityEngine;

namespace Game.NonCombat.Choice
{
    public enum ChoiceConditionType
    {
        Always,
        HasFlag,
        MissingFlag,
        PersonaStatAtLeast,
        GoldAtLeast,
        HasItem,
        ChapterAtLeast
    }

    [Serializable]
    public sealed class ChoiceCondition
    {
        [SerializeField] private ChoiceConditionType type;
        [SerializeField] private string id;
        [SerializeField] private PersonaStat personaStat;
        [SerializeField] private int amount;

        public ChoiceConditionType Type => type;
        public string Id => id;
        public PersonaStat PersonaStat => personaStat;
        public int Amount => amount;
    }
}
