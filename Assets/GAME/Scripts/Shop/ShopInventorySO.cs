using System.Collections.Generic;
using UnityEngine;

namespace Game.Shop
{
    [CreateAssetMenu(menuName = "GAME/Shop/Shop Inventory", fileName = "ShopInventory")]
    public sealed class ShopInventorySO : ScriptableObject
    {
        [SerializeField] private List<ShopItemEntry> items = new();

        public IReadOnlyList<ShopItemEntry> Items => items;
    }
}
