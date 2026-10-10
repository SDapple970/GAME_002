using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Combat.Actions;
using Game.Combat.Core;
using Game.Combat.Model;

namespace Game.Combat.Adapters
{
    public sealed class FieldCombatantFactory : ICombatantFactory
    {
        private readonly SkillBook _book;
        private readonly int _defaultStaggerMaxAllies;
        private readonly int _defaultStaggerMaxEnemies;
        private readonly SkillId _fallbackSkillId;

        public FieldCombatantFactory(SkillBook book, int defaultStaggerMaxAllies = 6, int defaultStaggerMaxEnemies = 8, int fallbackSkillId = 1)
        {
            _book = book;
            _defaultStaggerMaxAllies = defaultStaggerMaxAllies;
            _defaultStaggerMaxEnemies = defaultStaggerMaxEnemies;
            _fallbackSkillId = new SkillId(fallbackSkillId);
        }

        public void PopulateCombatants(CombatSession session, CombatStartRequest req)
        {
            session.SkillAcquisitionRecipientCharacterId = req.SkillAcquisitionRecipientCharacterId;
            session.IsPartySkillAcquisitionEligible = req.IsPartySkillAcquisitionEligible;
            int id = 1;
            CreateSide(session, req, req.AllyFieldObjects, Side.Allies, _defaultStaggerMaxAllies, ref id);
            // Snapshot the sole field ally's identity at entry. A later Party/default-target change
            // must not redirect this combat's EXP; multi-ally distribution remains an authored policy.
            if (session.Allies.Count == 1 && req.AllyFieldObjects.Count == 1 &&
                req.TryGetAllyCharacterId(req.AllyFieldObjects[0], out string characterId))
                session.ProgressionTargetCharacterId = characterId;

            int eid = Math.Max(100, id);
            CreateSide(session, req, req.EnemyFieldObjects, Side.Enemies, _defaultStaggerMaxEnemies, ref eid);
        }

        private void CreateSide(CombatSession session, CombatStartRequest request, List<GameObject> fieldObjects, Side side, int staggerMax, ref int idCounter)
        {
            if (fieldObjects == null) return;

            for (int i = 0; i < fieldObjects.Count; i++)
            {
                var go = fieldObjects[i];
                if (go == null) continue;

                var hpAcc = HpAccessor.TryCreate(go);
                if (hpAcc == null || !hpAcc.IsValid)
                {
                    Debug.LogWarning($"[FieldCombatantFactory] No HP found on {go.name}. (Need int hp/HP field or property)");
                    continue;
                }

                var combatant = new FieldCombatantAdapter(idCounter++, side, go, hpAcc, staggerMax);

                var list = new List<ISkill>(3);
                AddLoadoutSkills(go, side, request, list);

                if (list.Count == 0 && side == Side.Allies)
                {
                    var s1 = _book.Get(new SkillId(1));
                    if (IsAllowedCompatibilitySkill(s1, side)) list.Add(s1);

                    var s2 = _book.Get(new SkillId(2));
                    if (IsAllowedCompatibilitySkill(s2, side)) list.Add(s2);

                    var s3 = _book.Get(new SkillId(10)); // Inspect
                    if (IsAllowedCompatibilitySkill(s3, side)) list.Add(s3);
                }
                else if (list.Count == 0)
                {
                    var fallback = _book.Get(_fallbackSkillId);
                    if (IsAllowedCompatibilitySkill(fallback, side)) list.Add(fallback);
                }

                if (side == Side.Allies && request.TryGetAllyLoadoutSnapshot(go, out CombatSkillLoadoutSnapshot combined) &&
                    combined.IncludeCompatibilityLoadout)
                    for (int skillIndex = 0; skillIndex < combined.Skills.Count; skillIndex++)
                    {
                        ISkill skill = combined.Skills[skillIndex];
                        if (!list.Exists(existing => existing.Id.Value == skill.Id.Value)) list.Add(skill);
                    }

                combatant.SetSkills(list);
                if (list.Count == 0)
                    Debug.LogWarning($"[FieldCombatantFactory] {go.name} has no combat skills. Add CombatSkillLoadoutComponent or register fallback SkillDefinitionSO assets.");

                if (side == Side.Allies) session.Allies.Add(combatant);
                else
                {
                    session.Enemies.Add(combatant);
                    if (request.TryGetEnemySourceSnapshot(go, out EnemySourceSnapshot source))
                        session.RegisterEnemySource(combatant, source);
                }

                var kw = go.GetComponent<CombatKeywordComponent>();
                if (kw != null)
                {
                    combatant.SetWeakness(kw.Weakness);
                    combatant.SetResist(kw.Resist);
                }
                Debug.Log($"[KW-INJECT] {go.name} Weak={combatant.Weakness}, Resist={combatant.Resist}");
            }
        }

        private void AddLoadoutSkills(GameObject go, Side side, CombatStartRequest request, List<ISkill> skills)
        {
            if (side == Side.Allies && request != null &&
                request.TryGetAllyLoadoutSnapshot(go, out CombatSkillLoadoutSnapshot snapshot) && !snapshot.IncludeCompatibilityLoadout)
            {
                for (int i = 0; i < snapshot.Skills.Count; i++)
                    if (snapshot.Skills[i] != null) skills.Add(snapshot.Skills[i]);
                return;
            }

            var loadout = go.GetComponent<CombatSkillLoadoutComponent>();
            if (loadout == null || loadout.SkillIds == null)
                return;

            for (int i = 0; i < loadout.SkillIds.Length; i++)
            {
                var skill = _book.Get(new SkillId(loadout.SkillIds[i]));
                if (IsAllowedCompatibilitySkill(skill, side))
                {
                    skills.Add(skill);
                    continue;
                }

                if (skill == null) Debug.LogWarning($"[FieldCombatantFactory] SkillId {loadout.SkillIds[i]} on {go.name} is not registered.");
            }
        }

        private static bool IsAllowedCompatibilitySkill(ISkill skill, Side side)
        {
            if (skill == null) return false;
            if (skill is not SoSkill authored) return true;
            return side == Side.Allies
                ? authored.Definition.OwnershipCategory == Game.Combat.Data.SkillOwnershipCategory.Compatibility
                : authored.Definition.OwnershipCategory != Game.Combat.Data.SkillOwnershipCategory.Unique;
        }
    }
}
