using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Combat.Model;
using Game.Combat.Data;

namespace Game.Combat.Core
{
    /// <summary>
    /// 예약된 플랜들을 수집하여 [합(Clash)]과 [속도(Speed)] 기반으로 결과를 도출하고
    /// 비주얼 팀이 사용할 Playbook(대본)을 작성하는 핵심 엔진입니다.
    /// </summary>
    public static class CombatTurnResolver
    {
        // 내부 연산용 임시 데이터 클래스
        private class PendingAction
        {
            public ICombatant Actor;
            public PlannedAction Plan;
            public ISkill Skill;
            public ICombatant Target;
        }

        public static void ResolveTurn(CombatSession session)
        {
            session.CurrentTurn.Playbook.Clear();
            session.CurrentTurn.Events.Clear();

            // MVP 규칙: 슬롯 1번을 전원 처리한 뒤, 살아남은 자들의 슬롯 2번을 처리한다.
            ResolveSlotPhase(session, 1);
            ResolveSlotPhase(session, 2);
        }

        private static void ResolveSlotPhase(CombatSession session, int slotIndex)
        {
            List<PendingAction> actions = new();
            var allCombatants = session.Allies.Concat(session.Enemies).ToList();

            // 1. 해당 슬롯의 모든 행동 수집
            foreach (var actor in allCombatants)
            {
                if (actor.IsStunned || actor.HP <= 0) continue;

                if (session.CurrentTurn.TryGetPlan(actor.Id, out var plan))
                {
                    var slotAction = slotIndex == 1 ? plan.Slot1 : plan.Slot2;
                    if (slotAction.IsNone) continue;

                    ISkill skill = actor.Skills.FirstOrDefault(s => s.Id.Value == slotAction.SkillId.Value);
                    if (skill == null) continue;

                    ICombatant target = allCombatants.FirstOrDefault(c => c.Id.Value == slotAction.TargetCombatantId.Value);

                    actions.Add(new PendingAction { Actor = actor, Plan = slotAction, Skill = skill, Target = target });
                }
            }

            // 2. 합(Clash)과 일방 공격(Unopposed) 분류
            List<PendingAction> unopposed = new();
            List<System.Tuple<PendingAction, PendingAction>> clashes = new();
            HashSet<PendingAction> processed = new();

            foreach (var a in actions)
            {
                if (processed.Contains(a)) continue;

                // 서로 타겟팅 중이면 합(Clash) 발생
                if (a.Target != null && a.Plan.Targeting == TargetingRule.SingleEnemy)
                {
                    var mutual = actions.FirstOrDefault(b =>
                        b.Actor == a.Target &&
                        b.Target == a.Actor &&
                        b.Plan.Targeting == TargetingRule.SingleEnemy &&
                        !processed.Contains(b));

                    if (mutual != null)
                    {
                        clashes.Add(new System.Tuple<PendingAction, PendingAction>(a, mutual));
                        processed.Add(a);
                        processed.Add(mutual);
                        continue;
                    }
                }

                unopposed.Add(a);
                processed.Add(a);
            }

            // 3. [실행 1] 합(Clash) 먼저 처리 - 속도가 가장 빠른 합부터 처리
            var sortedClashes = clashes.OrderByDescending(c => Mathf.Max(c.Item1.Plan.PlannedSpeed, c.Item2.Plan.PlannedSpeed)).ToList();
            foreach (var clash in sortedClashes)
            {
                ExecuteClash(session, clash.Item1, clash.Item2);
            }

            // 4. [실행 2] 일방 공격 처리 - 속도 순
            var sortedUnopposed = unopposed.OrderByDescending(u => u.Plan.PlannedSpeed).ToList();
            foreach (var un in sortedUnopposed)
            {
                ExecuteUnopposed(session, un);
            }
        }

        private static void ExecuteClash(CombatSession session, PendingAction a, PendingAction b)
        {
            var ev = new Event_Clash
            {
                ActorA = a.Actor,
                SkillA = a.Skill,
                ActorB = b.Actor,
                SkillB = b.Skill
            };

            // 실행 직전 상태 체크
            if (a.Actor.HP <= 0 || a.Actor.IsStunned || b.Actor.HP <= 0 || b.Actor.IsStunned)
            {
                ev.LogMessage = $"[합 취소] 누군가 사망했거나 기절 상태입니다.";
                session.CurrentTurn.Playbook.Add(ev);
                return;
            }

            // 영감 소모 시도
            bool aCanAct = session.Inspiration.TrySpend(a.Skill.InspirationCost);
            bool bCanAct = session.Inspiration.TrySpend(b.Skill.InspirationCost);

            if (!aCanAct || !bCanAct)
            {
                ev.LogMessage = $"[합 취소] 영감이 부족합니다.";
                session.CurrentTurn.Playbook.Add(ev);
                return;
            }

            // 합 위력 계산 (MVP: 기본 데미지 + 변수)
            ev.PowerA = a.Skill.BaseDamage + Random.Range(1, 4);
            ev.PowerB = b.Skill.BaseDamage + Random.Range(1, 4);

            PendingAction winner = null;
            PendingAction loser = null;

            if (ev.PowerA > ev.PowerB) { winner = a; loser = b; ev.Winner = a.Actor; ev.Loser = b.Actor; }
            else if (ev.PowerB > ev.PowerA) { winner = b; loser = a; ev.Winner = b.Actor; ev.Loser = a.Actor; }

            if (winner != null)
            {
                ApplyDamageAndStagger(session, winner.Actor, winner.Skill, loser.Actor, out int dmg, out int stg, out bool weak);
                ev.DamageDealtToLoser = dmg;
                ev.StaggerDealtToLoser = stg;
                ev.HitWeakness = weak;
                ev.LogMessage = $"[합] {winner.Actor.Id} 님이 {loser.Actor.Id} 님을 상대로 승리했습니다! (위력: {Mathf.Max(ev.PowerA, ev.PowerB)} vs {Mathf.Min(ev.PowerA, ev.PowerB)})";
            }
            else
            {
                ev.LogMessage = $"[합] 무승부! 데미지가 발생하지 않았습니다.";
            }

            session.CurrentTurn.Playbook.Add(ev);
            session.CurrentTurn.Events.Add(new ResolvedEvent(ev.LogMessage));
        }

        private static void ExecuteUnopposed(CombatSession session, PendingAction un)
        {
            if (un.Target == null)
            {
                var utilEv = new Event_Utility { Actor = un.Actor, Skill = un.Skill };

                if (un.Actor.HP <= 0 || un.Actor.IsStunned || !session.Inspiration.TrySpend(un.Skill.InspirationCost))
                {
                    utilEv.IsCancelled = true;
                    utilEv.LogMessage = $"[{un.Actor.Id}] 유틸리티 행동이 취소되었습니다.";
                }
                else
                {
                    utilEv.LogMessage = $"[{un.Actor.Id}] 유틸리티 사용: {un.Skill.Name}";
                }
                session.CurrentTurn.Playbook.Add(utilEv);
                session.CurrentTurn.Events.Add(new ResolvedEvent(utilEv.LogMessage));
                return;
            }

            var ev = new Event_Unopposed
            {
                Actor = un.Actor,
                Target = un.Target,
                Skill = un.Skill
            };

            if (un.Actor.HP <= 0 || un.Actor.IsStunned)
            {
                ev.IsCancelled = true;
                ev.LogMessage = $"[{un.Actor.Id}] 행동이 취소되었습니다 (사망 또는 기절).";
                session.CurrentTurn.Playbook.Add(ev);
                return;
            }

            if (!session.Inspiration.TrySpend(un.Skill.InspirationCost))
            {
                ev.IsCancelled = true;
                ev.LackOfInspiration = true;
                ev.LogMessage = $"[{un.Actor.Id}] 행동 실패 (영감 부족).";
                session.CurrentTurn.Playbook.Add(ev);
                return;
            }

            if (un.Target.HP <= 0)
            {
                ev.IsCancelled = true;
                ev.LogMessage = $"[{un.Actor.Id}] 대상이 이미 사망했습니다.";
                session.CurrentTurn.Playbook.Add(ev);
                return;
            }

            ApplyDamageAndStagger(session, un.Actor, un.Skill, un.Target, out int dmg, out int stg, out bool weak);
            ev.DamageDealt = dmg;
            ev.StaggerDealt = stg;
            ev.HitWeakness = weak;
            ev.LogMessage = $"[일방 공격] {un.Actor.Id} 님이 {un.Target.Id} 님에게 {dmg} 데미지, {stg} 그로기를 입혔습니다.";

            session.CurrentTurn.Playbook.Add(ev);
            session.CurrentTurn.Events.Add(new ResolvedEvent(ev.LogMessage));
        }

        private static void ApplyDamageAndStagger(CombatSession session, ICombatant attacker, ISkill skill, ICombatant target, out int damage, out int stagger, out bool hitWeakness)
        {
            damage = skill.BaseDamage;
            hitWeakness = (target.Weakness & skill.Keywords) != 0;

            stagger = skill.BaseStagger;
            if (hitWeakness) stagger += skill.WeaknessStaggerBonus;

            if (damage > 0) target.ApplyDamage(damage);
            target.AddStagger(stagger);

            if (skill.Tag == SkillTag.Inspect)
            {
                session.Knowledge.RevealWeakness(target.Id);
            }
        }
    }
}