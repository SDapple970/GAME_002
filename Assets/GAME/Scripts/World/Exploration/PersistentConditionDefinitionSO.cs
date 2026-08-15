using UnityEngine;

namespace Game.World.Exploration
{
    public enum PersistentConditionCategory
    {
        Disease = 0,
        Quirk = 10
    }

    [CreateAssetMenu(menuName = "GAME/Exploration/Persistent Condition", fileName = "PersistentCondition")]
    public sealed class PersistentConditionDefinitionSO : ScriptableObject
    {
        [SerializeField] private string conditionId;
        [SerializeField] private string displayName;
        [TextArea, SerializeField] private string description;
        [SerializeField] private PersistentConditionCategory category;

        public string ConditionId => PersistentConditionIdentity.Normalize(conditionId);
        public string DisplayName => displayName;
        public string Description => description;
        public PersistentConditionCategory Category => category;
    }

    internal static class PersistentConditionIdentity
    {
        internal static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
