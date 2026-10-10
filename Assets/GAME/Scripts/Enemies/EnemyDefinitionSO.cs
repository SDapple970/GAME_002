using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>Authored enemy type identity and explicit acquisition mapping, independent of combat loadout.</summary>
    [CreateAssetMenu(menuName = "Game/Enemies/Enemy Definition")]
    public sealed class EnemyDefinitionSO : ScriptableObject
    {
        [SerializeField] private string persistentKey;
        [SerializeField] private string[] acquirableSkillPersistentKeys = Array.Empty<string>();

        public string PersistentKey => persistentKey;
        public IReadOnlyList<string> AcquirableSkillPersistentKeys =>
            Array.AsReadOnly(acquirableSkillPersistentKeys ?? Array.Empty<string>());
    }
}
