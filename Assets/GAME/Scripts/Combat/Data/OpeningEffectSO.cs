using UnityEngine;

namespace Game.Combat.Adapters
{
    [CreateAssetMenu(menuName = "Game/Combat/Opening Effect")]
    public sealed class OpeningEffectSO : ScriptableObject
    {
        [Header("Inspiration")]
        public int inspirationDelta = 0; // +면 회복, -면 소모

        [Header("Stagger (apply to all enemies)")]
        public int addEnemyStagger = 0;

        [Header("Stagger (apply to all allies)")]
        public int addAllyStagger = 0;

        [Header("Notes")]
        [TextArea] public string memo;
    }
}
