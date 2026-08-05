using UnityEngine;

namespace Game.NonCombat.Inventory
{
    [CreateAssetMenu(menuName = "GAME/NonCombat/Item Definition", fileName = "ItemDefinition")]
    public sealed class ItemDefinitionSO : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [TextArea(2, 5)]
        [SerializeField] private string description;
        [SerializeField] private Sprite icon;
        [Tooltip("Zero means unlimited for compatibility with existing item assets.")]
        [Min(0)] [SerializeField] private int maximumStackCount;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public int MaximumStackCount => Mathf.Max(0, maximumStackCount);

        private void OnValidate()
        {
            itemId = string.IsNullOrWhiteSpace(itemId) ? itemId : itemId.Trim();
            maximumStackCount = Mathf.Max(0, maximumStackCount);
        }
    }
}
