// Assets/GAME/Scripts/Story/Runtime/Data/ChoiceResult.cs
using Game.Story.Core;
using Game.Systems.Persona;
using UnityEngine;

namespace Game.Story.Data
{
    public enum ChoiceResultType
    {
        None,
        SetBoolFlag,
        SetIntFlag,
        AddIntFlag,
        AddPersonaXp
    }

    [System.Serializable]
    public sealed class ChoiceResult
    {
        [SerializeField] private ChoiceResultType resultType;
        [SerializeField] private string flagKey;
        [SerializeField] private bool boolValue;
        [SerializeField] private int intValue;
        [SerializeField] private PersonaStat personaStat;
        [SerializeField] private int personaXpAmount;

        public void Apply()
        {
            switch (resultType)
            {
                case ChoiceResultType.None:
                    return;
                case ChoiceResultType.SetBoolFlag:
                    if (!CanUseFlagResult()) return;
                    StoryFlagManager.Instance.SetBool(flagKey, boolValue);
                    return;
                case ChoiceResultType.SetIntFlag:
                    if (!CanUseFlagResult()) return;
                    StoryFlagManager.Instance.SetInt(flagKey, intValue);
                    return;
                case ChoiceResultType.AddIntFlag:
                    if (!CanUseFlagResult()) return;
                    StoryFlagManager.Instance.AddInt(flagKey, intValue);
                    return;
                case ChoiceResultType.AddPersonaXp:
                    if (PersonaStatusManager.Instance == null)
                    {
                        Debug.LogWarning("[ChoiceResult] PersonaStatusManager is missing.");
                        return;
                    }

                    if (personaXpAmount <= 0) return;
                    PersonaStatusManager.Instance.AddXp(personaStat, personaXpAmount);
                    return;
                default:
                    return;
            }
        }

        private bool CanUseFlagResult()
        {
            if (StoryFlagManager.Instance == null)
            {
                Debug.LogWarning("[ChoiceResult] StoryFlagManager is missing.");
                return false;
            }

            if (!string.IsNullOrEmpty(flagKey)) return true;

            Debug.LogWarning("[ChoiceResult] Flag key is required for this result.");
            return false;
        }
    }
}
