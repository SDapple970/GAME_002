using UnityEngine;
using Game.Combat.Model;

namespace Game.Combat.Adapters
{
    public sealed class CombatKeywordComponent : MonoBehaviour
    {
        [SerializeField] private KeywordMask weakness = KeywordMask.None;
        [SerializeField] private KeywordMask resist = KeywordMask.None;

        public KeywordMask Weakness => weakness;
        public KeywordMask Resist => resist;
    }
}