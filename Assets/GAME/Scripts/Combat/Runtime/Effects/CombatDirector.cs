using System;
using System.Collections;
using UnityEngine;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Integration;
using Game.Combat.Model;

namespace Game.Combat.Effects
{
    public sealed class CombatDirector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatEntryPoint entryPoint;
        [SerializeField] private CombatCameraController cameraController;

        [Header("Fallback Animation Settings")]
        [SerializeField] private float fallbackApproachDuration = 0.2f;
        [SerializeField] private float fallbackReturnDuration = 0.25f;
        [SerializeField] private float fallbackActionDelay = 0.15f;

        public void PlayResolution(CombatSession session, Action onComplete)
        {
            StartCoroutine(Co_PlayTurnAnimation(session, onComplete));
        }

        private void Awake()
        {
            if (entryPoint == null)
                entryPoint = FindFirstObjectByType<CombatEntryPoint>();

            if (cameraController == null)
                cameraController = FindFirstObjectByType<CombatCameraController>();
        }

        private IEnumerator Co_PlayTurnAnimation(CombatSession session, Action onComplete)
        {
            if (session == null || session.CurrentTurn == null || session.CurrentTurn.Playbook.Count == 0)
            {
                onComplete?.Invoke();
                yield break;
            }

            for (int i = 0; i < session.CurrentTurn.Playbook.Count; i++)
            {
                PlaybookEvent playbookEvent = session.CurrentTurn.Playbook[i];

                if (playbookEvent is Event_Unopposed unopposed)
                    yield return PlayUnopposed(unopposed);
                else if (playbookEvent is Event_Clash clash)
                    yield return PlayClash(clash);
                else if (playbookEvent is Event_Utility utility)
                    yield return PlayUtility(utility);

                yield return new WaitForSeconds(0.2f);
            }

            onComplete?.Invoke();
        }

        private IEnumerator PlayUnopposed(Event_Unopposed ev)
        {
            if (ev == null)
                yield break;

            if (ev.IsCancelled || ev.LackOfInspiration)
            {
                yield return new WaitForSeconds(0.3f);
                yield break;
            }

            GameObject actorObj = GetFieldObject(ev.Actor);
            GameObject targetObj = GetFieldObject(ev.Target);

            if (actorObj == null)
                yield break;

            Vector3 originalPosition = actorObj.transform.position;

            if (targetObj != null)
            {
                cameraController?.FocusAction(ev.Actor, ev.Target);
                yield return MoveActorForSkill(actorObj.transform, targetObj.transform, ev.Skill, originalPosition);
            }

            PlayAttackTrigger(actorObj);
            yield return WaitAfterMove(ev.Skill);

            if (targetObj != null)
                StartCoroutine(FlashColor(targetObj, Color.red, 0.2f));

            yield return new WaitForSeconds(0.3f);

            if (ShouldReturnAfterAction(ev.Skill) && actorObj != null)
                yield return MoveTransform(actorObj.transform, actorObj.transform.position, originalPosition, GetMoveDuration(actorObj.transform.position, originalPosition, ev.Skill));
        }

        private IEnumerator PlayClash(Event_Clash ev)
        {
            if (ev == null)
                yield break;

            GameObject objA = GetFieldObject(ev.ActorA);
            GameObject objB = GetFieldObject(ev.ActorB);

            if (objA == null || objB == null)
                yield break;

            Vector3 originalA = objA.transform.position;
            Vector3 originalB = objB.transform.position;

            cameraController?.FocusAction(ev.ActorA, ev.ActorB);

            yield return MoveActorForSkill(objA.transform, objB.transform, ev.SkillA, originalA);
            yield return MoveActorForSkill(objB.transform, objA.transform, ev.SkillB, originalB);

            PlayAttackTrigger(objA);
            PlayAttackTrigger(objB);

            float delay = Mathf.Max(GetActionDelay(ev.SkillA), GetActionDelay(ev.SkillB));
            yield return new WaitForSeconds(delay);

            if (ev.Loser != null)
            {
                GameObject loserObj = GetFieldObject(ev.Loser);
                if (loserObj != null)
                    StartCoroutine(FlashColor(loserObj, Color.red, 0.2f));
            }

            yield return new WaitForSeconds(0.4f);

            if (ShouldReturnAfterAction(ev.SkillA) && objA != null)
                yield return MoveTransform(objA.transform, objA.transform.position, originalA, GetMoveDuration(objA.transform.position, originalA, ev.SkillA));

            if (ShouldReturnAfterAction(ev.SkillB) && objB != null)
                yield return MoveTransform(objB.transform, objB.transform.position, originalB, GetMoveDuration(objB.transform.position, originalB, ev.SkillB));
        }

        private IEnumerator PlayUtility(Event_Utility ev)
        {
            if (ev == null || ev.IsCancelled)
                yield break;

            GameObject actorObj = GetFieldObject(ev.Actor);
            if (actorObj == null)
                yield break;

            cameraController?.FocusAction(ev.Actor, ev.Actor);
            PlayAttackTrigger(actorObj);
            StartCoroutine(FlashColor(actorObj, Color.yellow, 0.3f));
            yield return WaitAfterMove(ev.Skill);
        }

        private IEnumerator MoveActorForSkill(Transform actor, Transform target, ISkill skill, Vector3 originalPosition)
        {
            if (actor == null || target == null || !ShouldApproach(skill))
                yield break;

            Vector3 attackPoint = CalculateAttackPoint(actor.position, target.position, skill);
            attackPoint.z = originalPosition.z;

            cameraController?.FocusAction(GetCombatant(actor.gameObject), GetCombatant(target.gameObject));
            yield return MoveTransform(actor, actor.position, attackPoint, GetMoveDuration(actor.position, attackPoint, skill));
            cameraController?.FocusAction(GetCombatant(actor.gameObject), GetCombatant(target.gameObject));
        }

        private static bool ShouldApproach(ISkill skill)
        {
            if (skill == null)
                return false;

            return skill.MovementMode == SkillMovementMode.ApproachAndStay ||
                   skill.MovementMode == SkillMovementMode.ApproachAndReturn;
        }

        private static bool ShouldReturnAfterAction(ISkill skill)
        {
            return skill != null && skill.MovementMode == SkillMovementMode.ApproachAndReturn;
        }

        private static Vector3 CalculateAttackPoint(Vector3 actorPosition, Vector3 targetPosition, ISkill skill)
        {
            Vector3 direction = actorPosition - targetPosition;
            direction.z = 0f;

            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.left;
            else
                direction.Normalize();

            float distance = skill != null ? Mathf.Max(0f, skill.DesiredTargetDistance) : 1f;
            return targetPosition + direction * distance;
        }

        private float GetMoveDuration(Vector3 start, Vector3 end, ISkill skill)
        {
            float distance = Vector3.Distance(start, end);
            if (distance <= 0.001f)
                return 0f;

            float speed = skill != null ? skill.MoveSpeed : 0f;
            if (speed > 0.001f)
                return Mathf.Max(0.01f, distance / speed);

            return Mathf.Max(0.01f, fallbackApproachDuration);
        }

        private float GetActionDelay(ISkill skill)
        {
            return skill != null ? Mathf.Max(0f, skill.ActionDelayAfterMove) : fallbackActionDelay;
        }

        private IEnumerator WaitAfterMove(ISkill skill)
        {
            yield return new WaitForSeconds(GetActionDelay(skill));
        }

        private static void PlayAttackTrigger(GameObject actorObj)
        {
            Animator anim = actorObj != null ? actorObj.GetComponentInChildren<Animator>() : null;
            if (anim != null)
                anim.SetTrigger("Attack");
        }

        private GameObject GetFieldObject(ICombatant combatant)
        {
            if (combatant is FieldCombatantAdapter fieldCombatant)
                return fieldCombatant.FieldObject;

            return null;
        }

        private ICombatant GetCombatant(GameObject fieldObject)
        {
            if (fieldObject == null || entryPoint == null || entryPoint.ActiveSession == null)
                return null;

            CombatSession session = entryPoint.ActiveSession;

            for (int i = 0; i < session.Allies.Count; i++)
            {
                if (session.Allies[i] is FieldCombatantAdapter adapter && adapter.FieldObject == fieldObject)
                    return adapter;
            }

            for (int i = 0; i < session.Enemies.Count; i++)
            {
                if (session.Enemies[i] is FieldCombatantAdapter adapter && adapter.FieldObject == fieldObject)
                    return adapter;
            }

            return null;
        }

        private IEnumerator MoveTransform(Transform tf, Vector3 start, Vector3 end, float duration)
        {
            if (tf == null)
                yield break;

            if (duration <= 0.001f)
            {
                tf.position = end;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(t / duration);
                float eased = Mathf.Sin(normalizedTime * Mathf.PI * 0.5f);
                tf.position = Vector3.Lerp(start, end, eased);
                yield return null;
            }

            tf.position = end;
        }

        private IEnumerator FlashColor(GameObject obj, Color flashColor, float duration)
        {
            if (obj == null)
                yield break;

            SpriteRenderer sprite = obj.GetComponentInChildren<SpriteRenderer>();
            if (sprite == null)
                yield break;

            Color original = sprite.color;
            sprite.color = flashColor;
            yield return new WaitForSeconds(duration);
            sprite.color = original;
        }
    }
}
