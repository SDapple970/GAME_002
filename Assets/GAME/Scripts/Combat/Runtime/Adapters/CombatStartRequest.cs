using System.Collections.Generic;
using UnityEngine;
using Game.Combat.Model;

namespace Game.Combat.Adapters
{
    /// <summary>
    /// 필드에서 전투로 넘어갈 때 필요한 최소 정보(결합 최소화용).
    /// 필드 오브젝트 참조는 여기서만 들고, 전투 코어는 ICombatant 어댑터로만 본다.
    /// </summary>
    public sealed class CombatStartRequest
    {
        private readonly Dictionary<GameObject, string> _allyCharacterIds = new();
        private readonly Dictionary<GameObject, CombatSkillLoadoutSnapshot> _allyLoadouts = new();
        private readonly Dictionary<GameObject, EnemySourceSnapshot> _enemySources = new();
        public string SkillAcquisitionRecipientCharacterId { get; private set; }
        public bool IsPartySkillAcquisitionEligible { get; internal set; }
        public readonly StartReason Reason;
        public readonly Side InitiativeSide;
        public readonly int InspirationMax;
        public readonly int InspirationStart;
        public readonly CombatFlowMode FlowMode;
        public readonly CombatRuntimeConfig RuntimeConfig;

        public readonly OpeningEffectSO OpeningEffectOrNull;

        // 필드 객체 참조(전투 시작 시 어댑터가 래핑)
        public readonly List<GameObject> AllyFieldObjects = new();
        public readonly List<GameObject> EnemyFieldObjects = new();

        // Non-serialized runtime encounter owner carried only through accepted startup.
        internal Object EncounterOwnerOrNull;

        public CombatStartRequest(
            StartReason reason,
            Side initiativeSide,
            int inspirationMax,
            int inspirationStart,
            OpeningEffectSO openingEffectOrNull)
            : this(
                reason,
                initiativeSide,
                inspirationMax,
                inspirationStart,
                openingEffectOrNull,
                CombatFlowMode.LegacyPlanning,
                CombatRuntimeConfig.Compatibility)
        {
        }

        public CombatStartRequest(
            StartReason reason,
            Side initiativeSide,
            int inspirationMax,
            int inspirationStart,
            OpeningEffectSO openingEffectOrNull,
            CombatFlowMode flowMode)
            : this(
                reason,
                initiativeSide,
                inspirationMax,
                inspirationStart,
                openingEffectOrNull,
                flowMode,
                CombatRuntimeConfig.Compatibility)
        {
        }

        public CombatStartRequest(
            StartReason reason,
            Side initiativeSide,
            int inspirationMax,
            int inspirationStart,
            OpeningEffectSO openingEffectOrNull,
            CombatFlowMode flowMode,
            CombatRuntimeConfig runtimeConfig)
        {
            Reason = reason;
            InitiativeSide = initiativeSide;
            InspirationMax = inspirationMax;
            InspirationStart = inspirationStart;
            OpeningEffectOrNull = openingEffectOrNull;
            FlowMode = flowMode == CombatFlowMode.StandoffClashChain
                ? flowMode
                : CombatFlowMode.LegacyPlanning;
            RuntimeConfig = runtimeConfig;
        }

        /// <summary>Associates an ally field object with its persistent party identity before combat integration resolves skills.</summary>
        public void BindAllyCharacter(GameObject fieldObject, string characterId)
        {
            if (fieldObject == null || string.IsNullOrWhiteSpace(characterId))
                return;

            _allyCharacterIds[fieldObject] = characterId.Trim();
        }

        public bool TryGetAllyCharacterId(GameObject fieldObject, out string characterId)
        {
            if (fieldObject != null && _allyCharacterIds.TryGetValue(fieldObject, out characterId))
                return true;

            characterId = null;
            return false;
        }

        /// <summary>Stores an already-resolved immutable loadout; no persistent state is carried into combat core.</summary>
        public void SetAllyLoadoutSnapshot(GameObject fieldObject, CombatSkillLoadoutSnapshot snapshot)
        {
            if (fieldObject == null || snapshot == null || snapshot.Skills.Count == 0)
                return;

            _allyLoadouts[fieldObject] = snapshot;
        }

        public bool TryGetAllyLoadoutSnapshot(GameObject fieldObject, out CombatSkillLoadoutSnapshot snapshot)
        {
            if (fieldObject != null && _allyLoadouts.TryGetValue(fieldObject, out snapshot))
                return true;

            snapshot = null;
            return false;
        }

        internal void ClearAllyLoadoutSnapshot(GameObject fieldObject)
        {
            if (fieldObject != null) _allyLoadouts.Remove(fieldObject);
        }

        /// <summary>Explicit leader recipient for callers with multiple bound allies. Blank clears the recipient.</summary>
        public void SetSkillAcquisitionRecipient(string characterId)
        {
            SkillAcquisitionRecipientCharacterId = string.IsNullOrWhiteSpace(characterId) ? null : characterId.Trim();
        }

        public void SetEnemySourceSnapshot(GameObject fieldObject, EnemySourceSnapshot snapshot)
        {
            if (fieldObject != null && snapshot != null && snapshot.SourceKey != null)
                _enemySources[fieldObject] = snapshot;
        }

        public bool TryGetEnemySourceSnapshot(GameObject fieldObject, out EnemySourceSnapshot snapshot)
        {
            if (fieldObject != null && _enemySources.TryGetValue(fieldObject, out snapshot))
                return true;
            snapshot = null;
            return false;
        }
    }
}
