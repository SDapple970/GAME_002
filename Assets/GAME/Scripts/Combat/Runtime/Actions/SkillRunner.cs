using Game.Combat.Core;
using Game.Combat.Model;

namespace Game.Combat.Actions
{
    public static class SkillRunner
    {
        public static void Resolve(
            CombatSession session,
            ICombatant actor,
            ISkill skill,
            ICombatant targetOrNull)
        {
            // 기절 상태면 행동 불가(고정 규칙)
            if (actor.IsStunned)
            {
                session.CurrentTurn.Events.Add(new ResolvedEvent($"[{actor.Id}] is stunned and cannot act."));
                return;
            }

            // 영감 소모 (기본 공격은 0으로 데이터에서 처리)
            if (!session.Inspiration.TrySpend(skill.InspirationCost))
            {
                session.CurrentTurn.Events.Add(new ResolvedEvent($"[{actor.Id}] failed: not enough Inspiration for {skill.Name}."));
                return;
            }

            // 무료 행동(Inspect/ScanEnv 등): 타겟이 없을 수 있음
            if (skill.Tag == SkillTag.Inspect)
            {
                if (targetOrNull != null)
                {
                    session.Knowledge.RevealWeakness(targetOrNull.Id);
                    session.CurrentTurn.Events.Add(new ResolvedEvent(
                        $"[{actor.Id}] inspects [{targetOrNull.Id}] → Weakness revealed: {targetOrNull.Weakness}"
                    ));
                }
                else
                {
                    session.CurrentTurn.Events.Add(new ResolvedEvent($"[{actor.Id}] uses {skill.Name} (no target)."));
                }
                return;
            }

            if (skill.Tag == SkillTag.ScanEnv || skill.Tag == SkillTag.Utility)
            {
                session.CurrentTurn.Events.Add(new ResolvedEvent($"[{actor.Id}] uses {skill.Name}."));
                return;
            }

            if (targetOrNull == null)
            {
                session.CurrentTurn.Events.Add(new ResolvedEvent($"[{actor.Id}] failed: no target for {skill.Name}."));
                return;
            }

            // 데미지
            if (skill.BaseDamage > 0)
                targetOrNull.ApplyDamage(skill.BaseDamage);

            // 그로기 상승 (약점이면 보너스)
            bool hitWeakness = (targetOrNull.Weakness & skill.Keywords) != 0;

            int stagger = skill.BaseStagger;
            if (hitWeakness)
                stagger += skill.WeaknessStaggerBonus;

            StaggerSystem.AddStagger(targetOrNull, stagger);

            // ✅ WEAK 표시는 "공개된 경우"에만
            bool knownWeakness = session.Knowledge != null && session.Knowledge.IsWeaknessRevealed(targetOrNull.Id);

            session.CurrentTurn.Events.Add(new ResolvedEvent(
                $"[{actor.Id}] hits [{targetOrNull.Id}] with {skill.Name} (DMG:{skill.BaseDamage}, STG:+{stagger}"
                + ((hitWeakness && knownWeakness) ? ", WEAK" : "")
                + ")."
            ));
        }
    }
}