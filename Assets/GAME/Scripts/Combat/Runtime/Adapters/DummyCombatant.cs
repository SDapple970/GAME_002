using System.Collections.Generic;
using Game.Combat.Model;

namespace Game.Combat.Adapters
{
    // MVP 테스트용 더미 전투원(필드 연결 전 임시)
    public sealed class DummyCombatant : ICombatant
    {
        private readonly List<ISkill> _skills = new();

        public CombatantId Id { get; }
        public Side Side { get; }

        public int HP { get; private set; }
        public int MaxHP { get; }

        public KeywordMask Weakness { get; }
        public KeywordMask Resist { get; }

        public int Stagger { get; private set; }
        public int StaggerMax { get; }
        public bool IsStunned { get; private set; }

        public IReadOnlyList<ISkill> Skills => _skills;

        public DummyCombatant(int id, Side side, int hp, KeywordMask weakness, int staggerMax)
        {
            Id = new CombatantId(id);
            Side = side;
            HP = hp;
            MaxHP = hp;
            Weakness = weakness;
            Resist = KeywordMask.None;
            StaggerMax = staggerMax;
        }

        public void AddSkill(ISkill skill) => _skills.Add(skill);

        public void ApplyDamage(int amount)
        {
            if (amount <= 0) return;
            HP -= amount;
            if (HP < 0) HP = 0;
        }

        public void AddStagger(int amount)
        {
            Stagger += amount;
            if (Stagger > StaggerMax) Stagger = StaggerMax;
        }

        public void SetStunned(bool value) => IsStunned = value;

        public void ResetStaggerIfNeededOnStunEnd()
        {
            // 정책: 기절이 끝나면 그로기 0으로 리셋(단순)
            Stagger = 0;
        }
    }
}
