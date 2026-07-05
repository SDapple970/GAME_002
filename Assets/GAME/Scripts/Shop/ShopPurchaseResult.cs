using System;

namespace Game.Shop
{
    [Serializable]
    public sealed class ShopPurchaseResult
    {
        public bool success;
        public string itemId;
        public string displayName;
        public int purchasedQuantity;
        public int unitPrice;
        public int totalPrice;
        public string failureReason;

        public static ShopPurchaseResult Failed(string itemId, string reason)
        {
            return new ShopPurchaseResult
            {
                success = false,
                itemId = itemId,
                failureReason = reason
            };
        }
    }
}
