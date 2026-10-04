using Game.Combat.Data;

namespace Game.Combat.Model
{
    public sealed class CombatItemOption
    {
        public CombatItemOption(
            string itemId,
            string displayName,
            int count,
            CombatItemTargetRule targetRule)
        {
            ItemId = itemId;
            DisplayName = displayName;
            Count = count;
            TargetRule = targetRule;
        }

        public string ItemId { get; }
        public string DisplayName { get; }
        public int Count { get; }
        public CombatItemTargetRule TargetRule { get; }
    }
}
