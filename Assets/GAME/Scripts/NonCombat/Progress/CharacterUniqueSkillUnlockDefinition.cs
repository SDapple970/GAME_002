using System;
using UnityEngine;

namespace Game.NonCombat.Progress
{
    public enum UniqueSkillUnlockCondition
    {
        Unconfigured = 0,
        Level = 1
    }

    [Serializable]
    public sealed class CharacterUniqueSkillUnlockDefinition
    {
        [SerializeField] private bool enabled = true;
        [SerializeField] private string persistentSkillKey;
        [SerializeField] private UniqueSkillUnlockCondition condition;
        [SerializeField] private int unlockLevel;

        public bool Enabled => enabled;
        public string PersistentSkillKey => persistentSkillKey;
        public UniqueSkillUnlockCondition Condition => condition;
        public int UnlockLevel => unlockLevel;
        public static bool IsSupportedLevel(int level) => level == 5 || level == 10 || level == 20 || level == 40;
    }
}
