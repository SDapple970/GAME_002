using Game.Combat.Core;
using Game.Combat.Model;

namespace Game.Combat.Actions
{
    public static class SkillRunner
    {
        public static bool TryExecute(
            CombatSession session,
            CombatSkillExecutionRequest request,
            out CombatSkillExecutionResult result)
        {
            result = null;
            if (!CanExecuteRequest(session, request))
                return false;

            System.Collections.Generic.List<CombatSkillTargetResult> targetResults =
                new System.Collections.Generic.List<CombatSkillTargetResult>(request.Targets.Count);
            bool revealsWeakness = request.Skill.Tag == SkillTag.Inspect;
            bool appliesCombatantDamage = request.Skill.Tag != SkillTag.Inspect &&
                                          request.Skill.Tag != SkillTag.ScanEnv &&
                                          request.Skill.Tag != SkillTag.Utility;

            for (int i = 0; i < request.Targets.Count; i++)
            {
                ICombatant target = request.Targets[i];
                int hpBefore = target.HP;

                if (revealsWeakness)
                    session.Knowledge.RevealWeakness(target.Id);
                else if (appliesCombatantDamage && request.Skill.BaseDamage > 0)
                    target.ApplyDamage(request.Skill.BaseDamage);

                targetResults.Add(new CombatSkillTargetResult(target, hpBefore, target.HP));
            }

            result = new CombatSkillExecutionResult(
                request.Actor,
                request.Skill,
                request.SourceOutcome,
                targetResults);
            return true;
        }

        private static bool CanExecuteRequest(
            CombatSession session,
            CombatSkillExecutionRequest request)
        {
            if (session == null || request?.Actor == null || request.Skill == null ||
                request.Targets == null || request.Actor.HP <= 0 || request.Actor.IsStunned ||
                !IsCurrentRosterMember(session, request.Actor) || !OwnsSkill(request.Actor, request.Skill))
            {
                return false;
            }

            System.Collections.Generic.HashSet<ICombatant> targets =
                new System.Collections.Generic.HashSet<ICombatant>(CombatantReferenceComparer.Instance);
            for (int i = 0; i < request.Targets.Count; i++)
            {
                ICombatant target = request.Targets[i];
                if (target == null || target.HP <= 0 || !IsCurrentRosterMember(session, target) ||
                    !targets.Add(target))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool OwnsSkill(ICombatant actor, ISkill skill)
        {
            if (actor.Skills == null)
                return false;

            for (int i = 0; i < actor.Skills.Count; i++)
            {
                if (object.ReferenceEquals(actor.Skills[i], skill))
                    return true;
            }

            return false;
        }

        private static bool IsCurrentRosterMember(CombatSession session, ICombatant combatant)
        {
            System.Collections.Generic.IReadOnlyList<ICombatant> roster = session.GetSide(combatant.Side);
            for (int i = 0; i < roster.Count; i++)
            {
                if (object.ReferenceEquals(roster[i], combatant))
                    return true;
            }

            return false;
        }

        private sealed class CombatantReferenceComparer : System.Collections.Generic.IEqualityComparer<ICombatant>
        {
            public static CombatantReferenceComparer Instance { get; } = new CombatantReferenceComparer();

            public bool Equals(ICombatant x, ICombatant y) => object.ReferenceEquals(x, y);

            public int GetHashCode(ICombatant obj) =>
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        public static void Resolve(
            CombatSession session,
            ICombatant actor,
            ISkill skill,
            ICombatant targetOrNull)
        {
            // 기절 상태면 행동 불가(고정 규칙)
            if (actor.IsStunned)
            {
                session.CurrentTurn.AddResolvedEvent(new ResolvedEvent($"[{actor.Id}] is stunned and cannot act."));
                return;
            }

            // 영감 소모 (기본 공격은 0으로 데이터에서 처리)
            if (!session.Inspiration.TrySpend(skill.InspirationCost))
            {
                session.CurrentTurn.AddResolvedEvent(new ResolvedEvent($"[{actor.Id}] failed: not enough Inspiration for {skill.Name}."));
                return;
            }

            // 무료 행동(Inspect/ScanEnv 등): 타겟이 없을 수 있음
            if (skill.Tag == SkillTag.Inspect)
            {
                if (targetOrNull != null)
                {
                    session.Knowledge.RevealWeakness(targetOrNull.Id);
                    session.CurrentTurn.AddResolvedEvent(new ResolvedEvent(
                        $"[{actor.Id}] inspects [{targetOrNull.Id}] → Weakness revealed: {targetOrNull.Weakness}"
                    ));
                }
                else
                {
                    session.CurrentTurn.AddResolvedEvent(new ResolvedEvent($"[{actor.Id}] uses {skill.Name} (no target)."));
                }
                return;
            }

            if (skill.Tag == SkillTag.ScanEnv || skill.Tag == SkillTag.Utility)
            {
                session.CurrentTurn.AddResolvedEvent(new ResolvedEvent($"[{actor.Id}] uses {skill.Name}."));
                return;
            }

            if (targetOrNull == null)
            {
                session.CurrentTurn.AddResolvedEvent(new ResolvedEvent($"[{actor.Id}] failed: no target for {skill.Name}."));
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

            session.CurrentTurn.AddResolvedEvent(new ResolvedEvent(
                $"[{actor.Id}] hits [{targetOrNull.Id}] with {skill.Name} (DMG:{skill.BaseDamage}, STG:+{stagger}"
                + ((hitWeakness && knownWeakness) ? ", WEAK" : "")
                + ")."
            ));
        }
    }
}
