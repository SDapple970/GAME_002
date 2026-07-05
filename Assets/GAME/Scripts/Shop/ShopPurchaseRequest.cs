using System;

namespace Game.Shop
{
    [Serializable]
    public sealed class ShopPurchaseRequest
    {
        public string itemId;
        public int quantity = 1;
    }
}
