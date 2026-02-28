using UnityEngine;

namespace Game.Combat.Adapters
{
    public sealed class CombatHpComponent : MonoBehaviour
    {
        [SerializeField] private int hp = 10;
        [SerializeField] private int maxHp = 10;

        public int HP
        {
            get => hp;
            set => hp = Mathf.Clamp(value, 0, maxHp > 0 ? maxHp : int.MaxValue);
        }

        public int MaxHP
        {
            get => maxHp;
            set => maxHp = Mathf.Max(1, value);
        }
    }
}