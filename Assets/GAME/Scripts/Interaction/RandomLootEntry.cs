using System;

namespace Game.Interaction
{
    [Serializable]
    public sealed class RandomLootEntry
    {
        public string itemId;
        public string displayName;
        public int amount = 1;
        public float weight = 1f;
        public bool isNothing;
    }
}
