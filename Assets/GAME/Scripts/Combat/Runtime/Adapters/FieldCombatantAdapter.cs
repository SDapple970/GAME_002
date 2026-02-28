using System.Collections.Generic;
using UnityEngine;
using Game.Combat.Model;

namespace Game.Combat.Adapters
{
    /// <summary>
    /// 필드 GameObject를 전투 ICombatant로 래핑.
    /// 기존 필드 코드를 수정하지 않고 HP를 읽고/쓰게 만드는 목적.
    /// </summary>
    public sealed class FieldCombatantAdapter : ICombatant
    {
        private readonly List<ISkill> _skills = new();
        private readonly HpAccessor _hp;

        public CombatantId Id { get; }
        public Side Side { get; }
        public GameObject FieldObject { get; }

        public int HP => _hp.GetHp();
        public int MaxHP => _hp.GetMaxHpOrCurrent();

        // 약점/저항은 아직 필드에 저장 구조가 없으니, 일단 기본값(추후 컴포넌트로 확장)
        public KeywordMask Weakness { get; private set; } = KeywordMask.None;
        public KeywordMask Resist { get; private set; } = KeywordMask.None;

        public int Stagger { get; private set; }
        public int StaggerMax { get; private set; }
        public bool IsStunned { get; private set; }

        public IReadOnlyList<ISkill> Skills => _skills;

        public FieldCombatantAdapter(int id, Side side, GameObject fieldObject, HpAccessor hpAccessor, int staggerMax)
        {
            Id = new CombatantId(id);
            Side = side;
            FieldObject = fieldObject;
            _hp = hpAccessor;
            StaggerMax = staggerMax;
        }

        public void SetWeakness(KeywordMask weakness) => Weakness = weakness;
        public void SetResist(KeywordMask resist) => Resist = resist;

        public void SetSkills(IEnumerable<ISkill> skills)
        {
            _skills.Clear();
            if (skills == null) return;
            foreach (var s in skills)
            {
                if (s != null) _skills.Add(s);
            }
        }

        public void ApplyDamage(int amount)
        {
            if (amount <= 0) return;
            int v = HP - amount;
            if (v < 0) v = 0;
            _hp.SetHp(v);
        }

        public void AddStagger(int amount)
        {
            if (amount <= 0) return;
            Stagger += amount;
            if (Stagger > StaggerMax) Stagger = StaggerMax;
        }

        public void SetStunned(bool value) => IsStunned = value;

        public void ResetStaggerIfNeededOnStunEnd()
        {
            // MVP 정책: 기절이 끝나면 그로기 0
            Stagger = 0;
        }
    }
}
