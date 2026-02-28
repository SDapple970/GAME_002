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
            int id = 1;
            CreateSide(session, req.AllyFieldObjects, Side.Allies, _defaultStaggerMaxAllies, ref id);

            int eid = 100;
            CreateSide(session, req.EnemyFieldObjects, Side.Enemies, _defaultStaggerMaxEnemies, ref eid);
        }

        private void CreateSide(CombatSession session, List<GameObject> fieldObjects, Side side, int staggerMax, ref int idCounter)
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

                // 스킬 시스템이 아직 필드에 없으니 MVP는 fallback 스킬 1개만 부여
                var list = new List<ISkill>(3);

                if (side == Side.Allies)
                {
                    var s1 = _book.Get(new SkillId(1));
                    if (s1 != null) list.Add(s1);

                    var s2 = _book.Get(new SkillId(2));
                    if (s2 != null) list.Add(s2);

                    var s3 = _book.Get(new SkillId(10)); // Inspect
                    if (s3 != null) list.Add(s3);
                }
                else
                {
                    var fallback = _book.Get(_fallbackSkillId);
                    if (fallback != null) list.Add(fallback);
                }

                combatant.SetSkills(list);

                if (side == Side.Allies) session.Allies.Add(combatant);
                else session.Enemies.Add(combatant);

                var kw = go.GetComponent<CombatKeywordComponent>();
                if (kw != null)
                {
                    combatant.SetWeakness(kw.Weakness);
                    combatant.SetResist(kw.Resist);
                }
                Debug.Log($"[KW-INJECT] {go.name} Weak={combatant.Weakness}, Resist={combatant.Resist}");
            }
        }
    }
}
