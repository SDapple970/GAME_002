using UnityEngine;

namespace Game.Enemies
{
    /// <summary>Binds a field enemy to authored source data; it owns no acquisition runtime state.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemySourceComponent : MonoBehaviour
    {
        [SerializeField] private EnemyDefinitionSO definition;

        public EnemyDefinitionSO Definition => definition;
    }
}
