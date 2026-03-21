// GAME/Scripts/Common/Damage/SimpleDamageable.cs
using UnityEngine;

namespace Game.Common
{
    [DisallowMultipleComponent]
    public sealed class SimpleDamageable : MonoBehaviour, IDamageable
    {
        [SerializeField] private int hp = 3;

        public void TakeDamage(int amount)
        {
            if (amount <= 0)
                return;

            hp -= amount;

            if (hp <= 0)
                Destroy(gameObject);
        }
    }
}