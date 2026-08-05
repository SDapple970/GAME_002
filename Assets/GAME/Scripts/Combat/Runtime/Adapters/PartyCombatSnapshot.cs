using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat.Adapters
{
    public readonly struct PartyCombatantBinding
    {
        public readonly string CharacterId;
        public readonly GameObject FieldObject;
        public PartyCombatantBinding(string characterId, GameObject fieldObject) { CharacterId = characterId; FieldObject = fieldObject; }
    }

    public sealed class PartyCombatSnapshot
    {
        public string CharacterId { get; }
        public int Level { get; }
        public int InitialHp { get; }
        public int MaximumHp { get; }
        private readonly int[] _skillIds;
        public IReadOnlyList<int> SkillIds => Array.AsReadOnly(_skillIds);

        public PartyCombatSnapshot(string characterId, int level, int initialHp, int maximumHp, int[] skillIds)
        {
            CharacterId = characterId;
            Level = level;
            InitialHp = initialHp;
            MaximumHp = maximumHp;
            _skillIds = skillIds != null ? (int[])skillIds.Clone() : Array.Empty<int>();
        }
    }

    public sealed class PartyCombatBuildResult
    {
        public bool Success { get; }
        public string FailureReason { get; }
        public CombatStartRequest Request { get; }
        public PartyCombatSnapshot[] Snapshots { get; }

        internal PartyCombatBuildResult(bool success, string failureReason, CombatStartRequest request, PartyCombatSnapshot[] snapshots)
        { Success = success; FailureReason = failureReason; Request = request; Snapshots = snapshots ?? Array.Empty<PartyCombatSnapshot>(); }
    }
}
