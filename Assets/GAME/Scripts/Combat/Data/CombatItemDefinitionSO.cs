using UnityEngine;

namespace Game.Combat.Data
{
    public enum CombatItemTargetRule
    {
        Self,
        SingleAlly
    }

    public enum CombatItemEffectKind
    {
        RestoreHp,
        ApplyStatus
    }

    [CreateAssetMenu(menuName = "Game/Combat/Item Definition")]
    public sealed class CombatItemDefinitionSO : ScriptableObject
    {
        [Header("Inventory Mapping")]
        [SerializeField] private string itemId;
        [SerializeField] private string displayNameOverride;
        [SerializeField] private CombatItemTargetRule targetRule;

        [Header("Combat Effect")]
        [SerializeField] private CombatItemEffectKind effectKind;
        [Min(0), SerializeField] private int hpRecovery;
        [SerializeField] private CombatStatusDefinitionSO appliedStatus;

        public string ItemId => string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayNameOverride)
            ? ItemId
            : displayNameOverride.Trim();
        public CombatItemTargetRule TargetRule => targetRule;
        public CombatItemEffectKind EffectKind => effectKind;
        public int HpRecovery => Mathf.Max(0, hpRecovery);
        public CombatStatusDefinitionSO AppliedStatus => appliedStatus;

        public bool HasConfiguredEffect => effectKind == CombatItemEffectKind.RestoreHp
            ? HpRecovery > 0
            : appliedStatus != null && !string.IsNullOrEmpty(appliedStatus.StatusId);

        private void OnValidate()
        {
            itemId = itemId?.Trim();
            displayNameOverride = displayNameOverride?.Trim();
            hpRecovery = Mathf.Max(0, hpRecovery);
        }
    }
}
