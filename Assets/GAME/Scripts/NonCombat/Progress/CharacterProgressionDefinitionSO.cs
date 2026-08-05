using UnityEngine;

namespace Game.NonCombat.Progress
{
    [CreateAssetMenu(menuName = "GAME/NonCombat/Character Progression Definition", fileName = "CharacterProgressionDefinition")]
    public sealed class CharacterProgressionDefinitionSO : ScriptableObject
    {
        [SerializeField] private string characterId;
        [Min(1)] [SerializeField] private int startingLevel = 1;
        [Min(1)] [SerializeField] private int maximumLevel = 99;
        [Tooltip("Entry 0 is EXP required from level 1 to 2. Missing entries use the last authored value.")]
        [SerializeField] private int[] experienceRequiredByLevel = { 100 };

        public string CharacterId => characterId;
        public int StartingLevel => Mathf.Clamp(startingLevel, 1, MaximumLevel);
        public int MaximumLevel => Mathf.Max(1, maximumLevel);

        public bool TryGetRequiredExperience(int currentLevel, out int required)
        {
            required = 0;
            if (currentLevel < 1 || currentLevel >= MaximumLevel || experienceRequiredByLevel == null || experienceRequiredByLevel.Length == 0)
                return false;
            int index = Mathf.Min(currentLevel - 1, experienceRequiredByLevel.Length - 1);
            required = experienceRequiredByLevel[index];
            return required > 0;
        }

        private void OnValidate()
        {
            characterId = string.IsNullOrWhiteSpace(characterId) ? characterId : characterId.Trim();
            maximumLevel = Mathf.Max(1, maximumLevel);
            startingLevel = Mathf.Clamp(startingLevel, 1, maximumLevel);
        }
    }
}
