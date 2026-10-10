using System;
using System.Collections.Generic;
using Game.Combat.Data;
using Game.Combat.Model;
using Game.NonCombat.Party;
using Game.NonCombat.Progress;

namespace Game.Combat.Adapters
{
    /// <summary>Production boundary from persistent party identity into isolated combat-entry data.</summary>
    public sealed class CharacterPartyCombatAdapter
    {
        /// <summary>Validates the existing single-player field path without replacing encounter flow/configuration.</summary>
        public bool TryBindSinglePlayerRequest(CombatStartRequest request, UnityEngine.GameObject player,
            PartyRuntime party, CharacterProgressionService progression, out string message)
        {
            message = null;
            if (request == null || player == null || request.AllyFieldObjects.Count != 1 || request.AllyFieldObjects[0] != player)
            { message = "Single-player Party binding requires exactly the supplied player ally."; return false; }
            if (party == null || party.StartDefinition == null)
            {
                // Existing unconfigured scenes/tests keep their previous leader binding behavior.
                if (party != null) request.BindAllyCharacter(player, party.LeaderCharacterId);
                return true;
            }
            PartyCombatBuildResult result = BuildRequest(party, progression,
                new[] { new PartyCombatantBinding(party.LeaderCharacterId, player) }, request.EnemyFieldObjects,
                request.Reason, request.InitiativeSide, request.InspirationMax, request.InspirationStart, request.OpeningEffectOrNull);
            if (!result.Success) { message = result.FailureReason; return false; }
            if (result.Request.AllyFieldObjects.Count != 1 || result.Request.AllyFieldObjects[0] != player)
            { message = "The single-player field path requires the selected Party leader alone. Additional allies need explicit field bindings."; return false; }
            request.BindAllyCharacter(player, result.Snapshots[0].CharacterId);
            request.SetSkillAcquisitionRecipient(result.Request.SkillAcquisitionRecipientCharacterId);
            return true;
        }

        public PartyCombatBuildResult BuildRequest(
            PartyRuntime party,
            CharacterProgressionService progression,
            IReadOnlyList<PartyCombatantBinding> bindings,
            IReadOnlyList<UnityEngine.GameObject> enemies,
            StartReason reason,
            Side initiativeSide,
            int inspirationMax,
            int inspirationStart,
            OpeningEffectSO openingEffect = null)
        {
            if (party == null || progression == null)
                return Failed("PartyRuntime or CharacterProgressionService is missing.");

            Dictionary<string, UnityEngine.GameObject> byId = new(StringComparer.Ordinal);
            if (bindings != null)
                for (int i = 0; i < bindings.Count; i++)
                {
                    string id = CharacterIdentity.Normalize(bindings[i].CharacterId);
                    if (id != null && bindings[i].FieldObject != null && !byId.ContainsKey(id)) byId.Add(id, bindings[i].FieldObject);
                }

            CombatStartRequest request = new(reason, initiativeSide, inspirationMax, inspirationStart, openingEffect);
            List<PartyCombatSnapshot> snapshots = new();
            IReadOnlyList<string> lineup = party.CombatLineup;
            for (int i = 0; i < lineup.Count; i++)
            {
                string id = lineup[i];
                if (!progression.TryGetDefinition(id, out _) || !progression.TryGetState(id, out int level, out _))
                    return Failed($"Character '{id}' has no authored definition or progression state.");
                if (!byId.TryGetValue(id, out UnityEngine.GameObject fieldObject))
                    return Failed($"Character '{id}' has no explicit field binding.");
                HpAccessor hp = HpAccessor.TryCreate(fieldObject);
                if (hp == null || !hp.IsValid)
                    return Failed($"Character '{id}' has no valid HP source.");
                int currentHp = hp.GetHp(); int maximumHp = hp.GetMaxHpOrCurrent();
                if (maximumHp <= 0 || currentHp < 0 || currentHp > maximumHp)
                    return Failed($"Character '{id}' has invalid HP data.");
                CombatSkillLoadoutComponent loadout = fieldObject.GetComponent<CombatSkillLoadoutComponent>();
                snapshots.Add(new PartyCombatSnapshot(id, level, currentHp, maximumHp, loadout != null ? loadout.SkillIds : null));
                request.AllyFieldObjects.Add(fieldObject);
                request.BindAllyCharacter(fieldObject, id);
            }

            if (request.AllyFieldObjects.Count == 0) return Failed("Party combat lineup is empty.");
            if (byId.TryGetValue(party.LeaderCharacterId ?? string.Empty, out UnityEngine.GameObject leader) &&
                request.AllyFieldObjects.Contains(leader))
                request.SetSkillAcquisitionRecipient(party.LeaderCharacterId);
            if (enemies != null) for (int i = 0; i < enemies.Count; i++) if (enemies[i] != null) request.EnemyFieldObjects.Add(enemies[i]);
            return new PartyCombatBuildResult(true, null, request, snapshots.ToArray());
        }

        private static PartyCombatBuildResult Failed(string reason) => new(false, reason, null, null);
    }
}
