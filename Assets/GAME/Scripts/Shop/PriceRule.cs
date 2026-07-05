using System;
using UnityEngine;

namespace Game.Shop
{
    [Serializable]
    public sealed class PriceRule
    {
        [SerializeField] private float multiplier = 1f;
        [SerializeField] private int flatAdjustment;

        public int Apply(int basePrice)
        {
            float scaled = Mathf.Max(0, basePrice) * Mathf.Max(0f, multiplier);
            return Mathf.Max(0, Mathf.RoundToInt(scaled) + flatAdjustment);
        }
    }
}
