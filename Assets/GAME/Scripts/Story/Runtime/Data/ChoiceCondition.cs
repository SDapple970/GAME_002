// Assets/GAME/Scripts/Story/Runtime/Data/ChoiceCondition.cs
using Game.Story.Core;
using UnityEngine;

namespace Game.Story.Data
{
    [System.Serializable]
    public sealed class ChoiceCondition
    {
        [SerializeField] private StoryFlagCondition condition = new();

        public bool IsMet()
        {
            return condition == null || condition.Evaluate();
        }
    }
}
