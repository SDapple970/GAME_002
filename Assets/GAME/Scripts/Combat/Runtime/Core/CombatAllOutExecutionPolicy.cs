using System;
using System.Collections.Generic;
using Game.Combat.Actions;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    /// <summary>
    /// Each eligible ally contributes their first authored, damaging AllEnemies attack.
    /// All-Out uses the authored effects without a separate multiplier or resource cost.
    /// </summary>
    public static class CombatAllOutExecutionPolicy
    {
        /// <summary>Returns the lead skill for compatibility callers when the entire party can execute.</summary>
        public static bool TryCreateExecutionRequest(
            CombatSession session,
            out CombatSkillExecutionRequest request)
        {
            Selection selection = CombatAllOutPolicy.IsCandidate(session) ? new Selection(session) : null;
            request = selection != null && selection.UnavailableReason == null && selection.Requests.Count > 0
                ? selection.Requests[0] : null;
            return request != null;
        }

        internal static bool CanParticipate(CombatSession session, ICombatant actor)
        {
            return actor?.Side == Side.Allies && actor.HP > 0 &&
                CombatDeclarationPolicy.IsActiveMember(session, actor) &&
                !CombatDeclarationPolicy.IsPanicked(session, actor);
        }

        // This selection belongs to one ChainDecision. Roster identity and order are part of it.
        internal sealed class Selection
        {
            private readonly CombatSession _session;
            private readonly ICombatant[] _allies;
            private readonly ICombatant[] _enemies;
            private readonly List<ICombatant> _participants = new();
            private readonly List<ICombatant> _targets = new();
            private readonly List<CombatSkillExecutionRequest> _requests = new();

            internal IReadOnlyList<ICombatant> Participants => _participants;
            internal IReadOnlyList<ICombatant> Targets => _targets;
            internal IReadOnlyList<CombatSkillExecutionRequest> Requests => _requests;
            internal string UnavailableReason { get; }

            internal Selection(CombatSession session)
            {
                _session = session;
                _allies = session.Allies.ToArray();
                _enemies = session.Enemies.ToArray();
                foreach (ICombatant target in _enemies)
                    if (CombatDeclarationPolicy.IsLivingMember(session, target))
                        _targets.Add(target);

                foreach (ICombatant actor in _allies)
                {
                    if (!CanParticipate(session, actor))
                        continue;
                    if (_participants.Exists(member => ReferenceEquals(member, actor)))
                    {
                        UnavailableReason = "총공격 참가자 명단에 중복 아군이 있습니다";
                        continue;
                    }
                    _participants.Add(actor);

                    ISkill skill = FindAuthoredAttack(actor);
                    if (skill == null)
                    {
                        UnavailableReason ??= $"아군 {actor.Id}의 작성된 AllEnemies 공격 스킬 없음";
                        continue;
                    }
                    var request = new CombatSkillExecutionRequest(
                        actor, skill, _targets, null, CombatClashOutcome.AttackerWin);
                    if (SkillRunner.CanExecuteRequest(session, request))
                        _requests.Add(request);
                    else
                        UnavailableReason ??= $"아군 {actor.Id}의 AllEnemies 공격 스킬을 실행할 수 없습니다";
                }
            }

            internal bool HasCurrentRosters() =>
                !_session.IsCombatEnded && MatchesRoster(_allies, _session.Allies) &&
                MatchesRoster(_enemies, _session.Enemies);

            internal bool IsCurrent()
            {
                if (!HasCurrentRosters() || _participants.Count == 0 || _targets.Count == 0)
                    return false;

                int participantCount = 0;
                foreach (ICombatant actor in _allies)
                {
                    if (!CanParticipate(_session, actor)) continue;
                    if (participantCount >= _participants.Count ||
                        !ReferenceEquals(_participants[participantCount++], actor))
                        return false;
                }
                if (participantCount != _participants.Count)
                    return false;

                int targetCount = 0;
                foreach (ICombatant target in _enemies)
                {
                    if (!CombatDeclarationPolicy.IsLivingMember(_session, target)) continue;
                    if (target.HP <= 0 || !target.IsStunned || targetCount >= _targets.Count ||
                        !ReferenceEquals(_targets[targetCount++], target))
                        return false;
                }
                if (targetCount != _targets.Count)
                    return false;

                foreach (CombatSkillExecutionRequest request in _requests)
                    if (!SkillRunner.CanExecuteRequest(_session, request))
                        return false;
                return true;
            }

            private static bool MatchesRoster(ICombatant[] snapshot, List<ICombatant> roster)
            {
                if (snapshot.Length != roster.Count) return false;
                for (int i = 0; i < snapshot.Length; i++)
                    if (!ReferenceEquals(snapshot[i], roster[i])) return false;
                return true;
            }

            private static ISkill FindAuthoredAttack(ICombatant actor)
            {
                if (actor.Skills == null) return null;
                foreach (ISkill skill in actor.Skills)
                    if (skill?.Tag == SkillTag.Attack &&
                        skill.Targeting == TargetingRule.AllEnemies && skill.BaseDamage > 0)
                        return skill;
                return null;
            }
        }
    }
}
