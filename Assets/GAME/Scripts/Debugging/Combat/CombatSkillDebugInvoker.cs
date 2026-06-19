using System.Text;
using UnityEngine;
using Game.Combat.Core;
using Game.Combat.Model;
using Game.Combat.Actions;

namespace Game.Combat.Debugging
{
    public sealed class CombatSkillDebugInvoker : MonoBehaviour
    {
        [SerializeField] private CombatEntryPoint entryPoint;
        [SerializeField] private int actorAllyIndex = 0;
        [SerializeField] private int targetEnemyIndex = 0;

        private CombatSession _session;
        private ICombatant _actor;
        private ICombatant _target;

        private void OnEnable()
        {
            if (entryPoint != null)
                entryPoint.OnCombatStarted += HandleCombatStarted;
        }

        private void OnDisable()
        {
            if (entryPoint != null)
                entryPoint.OnCombatStarted -= HandleCombatStarted;
        }

        private void HandleCombatStarted(CombatSession session)
        {
            _session = session;

            _actor = (session.Allies.Count > actorAllyIndex) ? session.Allies[actorAllyIndex] : null;
            _target = (session.Enemies.Count > targetEnemyIndex) ? session.Enemies[targetEnemyIndex] : null;

            Debug.Log("[CombatDebug] Combat started.");
            PrintSkills();
        }

        private void Update()
        {
            if (_session == null || _actor == null) return;

            // 0: 스킬 목록 다시 출력
            if (Input.GetKeyDown(KeyCode.Alpha0)) PrintSkills();

            // 1: Inspect (타겟 필요)
            if (Input.GetKeyDown(KeyCode.Alpha1)) UseFirstSkillByTag(SkillTag.Inspect, requireTarget: true);

            // 2: Fire(Attack 중 keywords가 Fire인 스킬)
            if (Input.GetKeyDown(KeyCode.Alpha2)) UseFirstSkillMatchingKeywords(KeywordMask.Fire);

            // 3: 기본 Attack(그냥 tag Attack 첫 번째)
            if (Input.GetKeyDown(KeyCode.Alpha3)) UseFirstSkillByTag(SkillTag.Attack, requireTarget: true);
        }

        private void PrintSkills()
        {
            if (_actor == null) return;

            var sb = new StringBuilder();
            sb.AppendLine($"[CombatDebug] Actor {_actor.Id} Skills:");
            for (int i = 0; i < _actor.Skills.Count; i++)
            {
                var s = _actor.Skills[i];
                sb.AppendLine($"  - [{i}] {s.Name}  Tag={s.Tag}  KW={s.Keywords}  Cost={s.InspirationCost}  ConsumesTurn={s.ConsumesTurn}");
            }
            Debug.Log(sb.ToString());
        }

        private void UseFirstSkillByTag(SkillTag tag, bool requireTarget)
        {
            ISkill found = null;
            foreach (var s in _actor.Skills)
            {
                if (s != null && s.Tag == tag) { found = s; break; }
            }

            if (found == null)
            {
                Debug.LogWarning($"[CombatDebug] No skill with tag {tag} on actor.");
                return;
            }

            if (requireTarget && _target == null)
            {
                Debug.LogWarning("[CombatDebug] No target enemy selected/found.");
                return;
            }

            SkillRunner.Resolve(_session, _actor, found, requireTarget ? _target : null);
            DumpCurrentTurnEvents();
        }

        private void UseFirstSkillMatchingKeywords(KeywordMask keyword)
        {
            ISkill found = null;
            foreach (var s in _actor.Skills)
            {
                if (s != null && (s.Keywords & keyword) != 0) { found = s; break; }
            }

            if (found == null)
            {
                Debug.LogWarning($"[CombatDebug] No skill matching keyword {keyword} on actor.");
                return;
            }

            if (_target == null)
            {
                Debug.LogWarning("[CombatDebug] No target enemy selected/found.");
                return;
            }

            SkillRunner.Resolve(_session, _actor, found, _target);
            DumpCurrentTurnEvents();
        }

        private void DumpCurrentTurnEvents()
        {
            if (_session?.CurrentTurn?.Events == null) return;

            for (int i = 0; i < _session.CurrentTurn.Events.Count; i++)
            {
                Debug.Log(_session.CurrentTurn.Events[i].ToString());
            }
        }
    }
}