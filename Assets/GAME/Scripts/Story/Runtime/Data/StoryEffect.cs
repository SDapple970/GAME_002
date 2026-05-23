// Assets/GAME/Scripts/Story/Runtime/Data/StoryEffect.cs
using Game.Story;
using Game.Systems.Persona;
using UnityEngine;

namespace Game.Story.Data
{
    public enum StoryEffectType
    {
        SetBoolFlag,
        SetIntFlag,
        AddIntFlag,
        AddPersonaXp,
        SetChapter,
        SetMainProgress,
        AdvanceMainProgress,
        MarkEventCompleted,
        ClearEventCompleted
    }

    [System.Serializable]
    public sealed class StoryEffect
    {
        [SerializeField] private StoryEffectType type;
        [SerializeField] private string key;
        [SerializeField] private bool boolValue;
        [SerializeField] private int intValue;
        [SerializeField] private PersonaStat personaStat;
        [SerializeField] private int xpAmount;

        public void Apply()
        {
            switch (type)
            {
                case StoryEffectType.SetBoolFlag:
                    if (!CanUseFlagEffect()) return;
                    StoryFlagManager.Instance.SetBool(key, boolValue);
                    return;
                case StoryEffectType.SetIntFlag:
                    if (!CanUseFlagEffect()) return;
                    StoryFlagManager.Instance.SetInt(key, intValue);
                    return;
                case StoryEffectType.AddIntFlag:
                    if (!CanUseFlagEffect()) return;
                    StoryFlagManager.Instance.AddInt(key, intValue);
                    return;
                case StoryEffectType.AddPersonaXp:
                    if (PersonaStatusManager.Instance == null)
                    {
                        Debug.LogWarning($"[StoryEffect] PersonaStatusManager missing for stat='{personaStat}' xp={xpAmount}.");
                        return;
                    }

                    if (xpAmount <= 0)
                    {
                        Debug.LogWarning($"[StoryEffect] Ignored non-positive Persona XP amount={xpAmount}.");
                        return;
                    }

                    PersonaStatusManager.Instance.AddXp(personaStat, xpAmount);
                    return;
                case StoryEffectType.SetChapter:
                    if (!CanUseProgressEffect()) return;
                    StoryProgressManager.Instance.SetChapter(intValue);
                    return;
                case StoryEffectType.SetMainProgress:
                    if (!CanUseProgressEffect()) return;
                    StoryProgressManager.Instance.SetMainProgress(intValue);
                    return;
                case StoryEffectType.AdvanceMainProgress:
                    if (!CanUseProgressEffect()) return;
                    StoryProgressManager.Instance.AdvanceMainProgress(intValue <= 0 ? 1 : intValue);
                    return;
                case StoryEffectType.MarkEventCompleted:
                    if (!CanUseProgressEffect()) return;
                    StoryProgressManager.Instance.MarkEventCompleted(key);
                    return;
                case StoryEffectType.ClearEventCompleted:
                    if (!CanUseProgressEffect()) return;
                    StoryProgressManager.Instance.ClearEventCompleted(key);
                    return;
                default:
                    return;
            }
        }

        private bool CanUseFlagEffect()
        {
            if (StoryFlagManager.Instance == null)
            {
                Debug.LogWarning($"[StoryEffect] StoryFlagManager missing for key='{key}'.");
                return false;
            }

            if (!string.IsNullOrEmpty(key)) return true;

            Debug.LogWarning("[StoryEffect] Empty flag key was ignored.");
            return false;
        }

        private bool CanUseProgressEffect()
        {
            if (StoryProgressManager.Instance != null) return true;

            Debug.LogWarning($"[StoryEffect] StoryProgressManager missing for type='{type}' key='{key}'.");
            return false;
        }
    }
}
