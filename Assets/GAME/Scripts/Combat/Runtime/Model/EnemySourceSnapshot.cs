using System;
using System.Collections.Generic;

namespace Game.Combat.Model
{
    /// <summary>Immutable authored enemy provenance; no scene or definition asset survives this boundary.</summary>
    public sealed class EnemySourceSnapshot
    {
        public string SourceKey { get; }
        public IReadOnlyList<string> AcquirableSkillPersistentKeys { get; }

        public EnemySourceSnapshot(string sourceKey, IEnumerable<string> acquirableSkillPersistentKeys)
        {
            SourceKey = Normalize(sourceKey);
            List<string> keys = new();
            HashSet<string> seen = new(StringComparer.Ordinal);
            if (acquirableSkillPersistentKeys != null)
                foreach (string rawKey in acquirableSkillPersistentKeys)
                {
                    string key = Normalize(rawKey);
                    if (key != null && seen.Add(key)) keys.Add(key);
                }
            AcquirableSkillPersistentKeys = keys.AsReadOnly();
        }

        private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
