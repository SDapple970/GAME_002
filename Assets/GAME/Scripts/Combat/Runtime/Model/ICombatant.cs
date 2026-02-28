using System.Collections.Generic;

namespace Game.Combat.Model
{
    public interface ICombatant
    {
        CombatantId Id { get; }
        Side Side { get; }

        int HP { get; }
        int MaxHP { get; }

        KeywordMask Weakness { get; }
        KeywordMask Resist { get; }

        int Stagger { get; }
        int StaggerMax { get; }
        bool IsStunned { get; }

        IReadOnlyList<ISkill> Skills { get; }

        void ApplyDamage(int amount);
        void AddStagger(int amount);
        void SetStunned(bool value);

        // MVP 단순화: 기절이 끝나면 그로기 리셋 정책을 여기로 위임
        void ResetStaggerIfNeededOnStunEnd();
    }
}
