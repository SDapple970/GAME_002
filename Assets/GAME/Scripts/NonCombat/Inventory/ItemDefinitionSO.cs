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

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
    }
}
