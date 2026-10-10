using Game.Combat.Adapters;
using Game.Combat.Model;
using Game.Enemies;
using Game.NonCombat.Party;
using UnityEngine;

namespace Game.Combat.Integration
{
    /// <summary>Captures authored enemy sources and a request-bound recipient before combatant construction.</summary>
    public static class EnemyCombatProvenanceBridge
    {
        public static void PrepareRequest(CombatStartRequest request, Object diagnosticContext)
        {
            if (request == null) return;

            ResolveRecipient(request, diagnosticContext);
            request.IsPartySkillAcquisitionEligible = IsParticipatingParty(request);
            for (int i = 0; i < request.EnemyFieldObjects.Count; i++)
            {
                GameObject enemy = request.EnemyFieldObjects[i];
                if (enemy == null || request.TryGetEnemySourceSnapshot(enemy, out _)) continue;
                EnemySourceComponent component = enemy.GetComponent<EnemySourceComponent>();
                if (component == null) continue; // Existing un-authored enemies retain compatibility behavior.
                EnemyDefinitionSO definition = component.Definition;
                if (definition == null || string.IsNullOrWhiteSpace(definition.PersistentKey))
                {
                    Debug.LogWarning("[EnemyCombatProvenanceBridge] Enemy source has no definition or stable persistent key; acquisition provenance was omitted.", component);
                    continue;
                }

                request.SetEnemySourceSnapshot(enemy,
                    new EnemySourceSnapshot(definition.PersistentKey, definition.AcquirableSkillPersistentKeys));
            }
        }

        private static void ResolveRecipient(CombatStartRequest request, Object diagnosticContext)
        {
            string recipient = request.SkillAcquisitionRecipientCharacterId;
            if (recipient == null && request.AllyFieldObjects.Count == 1)
                request.TryGetAllyCharacterId(request.AllyFieldObjects[0], out recipient);
            if (recipient == null) return;

            for (int i = 0; i < request.AllyFieldObjects.Count; i++)
                if (request.TryGetAllyCharacterId(request.AllyFieldObjects[i], out string boundId) && boundId == recipient)
                {
                    request.SetSkillAcquisitionRecipient(recipient);
                    return;
                }

            request.SetSkillAcquisitionRecipient(null);
            Debug.LogWarning($"[EnemyCombatProvenanceBridge] Recipient '{recipient}' is not bound to a participating ally; acquisition was disabled for this combat.", diagnosticContext);
        }

        private static bool IsParticipatingParty(CombatStartRequest request)
        {
            PartyRuntime party = PartyRuntime.Instance;
            if (party == null || request.AllyFieldObjects.Count == 0) return false;
            for (int i = 0; i < request.AllyFieldObjects.Count; i++)
                if (!request.TryGetAllyCharacterId(request.AllyFieldObjects[i], out string id) || !party.Contains(id))
                    return false;
            return true;
        }
    }
}
