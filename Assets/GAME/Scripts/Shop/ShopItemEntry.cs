using System;
using UnityEngine;

namespace Game.Shop
{
    [Serializable]
    public sealed class ShopItemEntry
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField] private int price;
        [SerializeField] private int quantity = 1;
        [SerializeField] private bool unlocked = true;

        public string ItemId => itemId;
        public string DisplayName => !string.IsNullOrWhiteSpace(displayName) ? displayName : itemId;
        public int Price => Mathf.Max(0, price);
        public int Quantity => Mathf.Max(1, quantity);
        public bool Unlocked => unlocked;
    }
}
