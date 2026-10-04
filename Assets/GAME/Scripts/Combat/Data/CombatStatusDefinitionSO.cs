using UnityEngine;

namespace Game.Combat.Data
{
    public enum CombatStatusPolarity
    {
        Buff,
        Debuff
    }

    public enum CombatStatusStackPolicy
    {
        Refresh,
        Replace,
        Stack
    }

    public enum CombatStatusDurationKind
    {
        UntilRemoved,
        Explicit
    }

    [CreateAssetMenu(menuName = "Game/Combat/Status Definition")]
    public sealed class CombatStatusDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string statusId;
        [SerializeField] private string displayName;
        [SerializeField] private CombatStatusPolarity polarity;

        [Header("Stacking")]
        [SerializeField] private CombatStatusStackPolicy stackPolicy;
        [Min(1), SerializeField] private int maximumStacks = 1;

        [Header("Duration Metadata")]
        [SerializeField] private CombatStatusDurationKind durationKind;
        [Min(0), SerializeField] private int explicitDuration;

        public string StatusId => string.IsNullOrWhiteSpace(statusId) ? string.Empty : statusId.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? StatusId : displayName.Trim();
        public CombatStatusPolarity Polarity => polarity;
        public CombatStatusStackPolicy StackPolicy => stackPolicy;
        public int MaximumStacks => Mathf.Max(1, maximumStacks);
        public CombatStatusDurationKind DurationKind => durationKind;
        public int ExplicitDuration => durationKind == CombatStatusDurationKind.Explicit
            ? Mathf.Max(0, explicitDuration)
            : 0;

        private void OnValidate()
        {
            statusId = statusId?.Trim();
            displayName = displayName?.Trim();
            maximumStacks = Mathf.Max(1, maximumStacks);
            explicitDuration = Mathf.Max(0, explicitDuration);
        }
    }
}
