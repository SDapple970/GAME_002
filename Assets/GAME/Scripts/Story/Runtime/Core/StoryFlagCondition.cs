// Assets/GAME/Scripts/Story/Runtime/Core/StoryFlagCondition.cs
using Game.Systems.Persona;
using UnityEngine;

namespace Game.Story.Core
{
    public enum StoryConditionType
    {
        None,
        BoolFlagEquals,
        IntFlagAtLeast,
        IntFlagEquals,
        PersonaStatAtLeast
    }

    [System.Serializable]
    public sealed class StoryFlagCondition
    {
        [SerializeField] private StoryConditionType conditionType;
        [SerializeField] private string flagKey;
        [SerializeField] private bool expectedBool;
        [SerializeField] private int expectedInt;
        [SerializeField] private PersonaStat personaStat;
        [SerializeField] private int requiredLevel = 1;

        public bool Evaluate()
        {
            switch (conditionType)
            {
                case StoryConditionType.None:
                    return true;
                case StoryConditionType.BoolFlagEquals:
                    if (StoryFlagManager.Instance == null)
                    {
                        Debug.LogWarning("[StoryFlagCondition] StoryFlagManager is missing.");
                        return false;
                    }
                    return StoryFlagManager.Instance.GetBool(flagKey) == expectedBool;
                case StoryConditionType.IntFlagAtLeast:
                    if (StoryFlagManager.Instance == null)
                    {
                        Debug.LogWarning("[StoryFlagCondition] StoryFlagManager is missing.");
                        return false;
                    }
                    return StoryFlagManager.Instance.GetInt(flagKey) >= expectedInt;
                case StoryConditionType.IntFlagEquals:
                    if (StoryFlagManager.Instance == null)
                    {
                        Debug.LogWarning("[StoryFlagCondition] StoryFlagManager is missing.");
                        return false;
                    }
                    return StoryFlagManager.Instance.GetInt(flagKey) == expectedInt;
                case StoryConditionType.PersonaStatAtLeast:
                    if (PersonaStatusManager.Instance == null)
                    {
                        Debug.LogWarning("[StoryFlagCondition] PersonaStatusManager is missing.");
                        return false;
                    }
                    return PersonaStatusManager.Instance.GetLevel(personaStat) >= requiredLevel;
                default:
                    return false;
            }
        }
    }
}
