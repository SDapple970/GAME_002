using UnityEngine;

namespace Game.Combat.Adapters
{
    public sealed class CombatSkillLoadoutComponent : MonoBehaviour
    {
        [SerializeField] private int[] skillIds;

        public int[] SkillIds => skillIds;
    }
}