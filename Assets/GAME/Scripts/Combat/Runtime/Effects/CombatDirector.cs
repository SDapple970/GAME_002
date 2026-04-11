// GAME/Scripts/Combat/Effects/CombatDirector.cs
using System;
using System.Collections;
using UnityEngine;
using Game.Combat.Core;
using Game.Combat.Model;
using Game.Combat.Adapters;
using Game.Combat.Data;

namespace Game.Combat.Effects
{
    public sealed class CombatDirector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatEntryPoint entryPoint;

        [Header("Animation Settings")]
        [SerializeField] private float approachDuration = 0.2f;
        [SerializeField] private float returnDuration = 0.25f;
        [SerializeField] private float stopDistance = 1.0f;

        public void PlayResolution(CombatSession session, Action onComplete)
        {
            StartCoroutine(Co_PlayTurnAnimation(session, onComplete));
        }

        private IEnumerator Co_PlayTurnAnimation(CombatSession session, Action onComplete)
        {
            if (session == null || session.CurrentTurn == null || session.CurrentTurn.Playbook.Count == 0)
            {
                onComplete?.Invoke();
                yield break;
            }

            foreach (var playbookEvent in session.CurrentTurn.Playbook)
            {
                if (playbookEvent is Event_Unopposed unopposed)
                {
                    yield return StartCoroutine(PlayUnopposed(unopposed));
                }
                else if (playbookEvent is Event_Clash clash)
                {
                    yield return StartCoroutine(PlayClash(clash));
                }
                else if (playbookEvent is Event_Utility utility)
                {
                    yield return StartCoroutine(PlayUtility(utility));
                }

                yield return new WaitForSeconds(0.2f);
            }

            onComplete?.Invoke();
        }

        private IEnumerator PlayUnopposed(Event_Unopposed ev)
        {
            if (ev.IsCancelled || ev.LackOfInspiration)
            {
                Debug.Log($"[Director] 행동 취소: {ev.Actor?.Id}");
                yield return new WaitForSeconds(0.3f);
                yield break;
            }

            GameObject actorObj = GetFieldObject(ev.Actor);
            GameObject targetObj = GetFieldObject(ev.Target);

            if (actorObj != null && targetObj != null)
            {
                Vector3 originalPos = actorObj.transform.position;
                Vector3 targetPos = targetObj.transform.position;
                Vector3 dir = (targetPos - originalPos).normalized;
                Vector3 attackPos = targetPos - (dir * stopDistance);

                yield return StartCoroutine(MoveTransform(actorObj.transform, originalPos, attackPos, approachDuration));

                Animator anim = actorObj.GetComponentInChildren<Animator>();
                if (anim != null)
                    anim.SetTrigger("Attack");

                yield return new WaitForSeconds(0.15f);

                StartCoroutine(FlashColor(targetObj, Color.red, 0.2f));

                yield return new WaitForSeconds(0.3f);

                if (ev.Target != null && ev.Target.HP <= 0)
                    yield break;

                yield return StartCoroutine(MoveTransform(actorObj.transform, attackPos, originalPos, returnDuration));
            }
        }

        private IEnumerator PlayClash(Event_Clash ev)
        {
            GameObject objA = GetFieldObject(ev.ActorA);
            GameObject objB = GetFieldObject(ev.ActorB);

            if (objA != null && objB != null)
            {
                Vector3 posA = objA.transform.position;
                Vector3 posB = objB.transform.position;

                Vector3 center = (posA + posB) / 2f;
                Vector3 dirA = (center - posA).normalized;
                Vector3 dirB = (center - posB).normalized;

                Vector3 clashPosA = center - (dirA * (stopDistance * 0.5f));
                Vector3 clashPosB = center - (dirB * (stopDistance * 0.5f));

                Coroutine moveA = StartCoroutine(MoveTransform(objA.transform, posA, clashPosA, approachDuration));
                Coroutine moveB = StartCoroutine(MoveTransform(objB.transform, posB, clashPosB, approachDuration));
                yield return moveA;
                yield return moveB;

                Animator animA = objA.GetComponentInChildren<Animator>();
                Animator animB = objB.GetComponentInChildren<Animator>();
                if (animA != null) animA.SetTrigger("Attack");
                if (animB != null) animB.SetTrigger("Attack");

                yield return new WaitForSeconds(0.15f);

                if (ev.Loser != null)
                {
                    GameObject loserObj = GetFieldObject(ev.Loser);
                    if (loserObj != null)
                        StartCoroutine(FlashColor(loserObj, Color.red, 0.2f));
                }

                yield return new WaitForSeconds(0.4f);

                bool actorADead = ev.ActorA != null && ev.ActorA.HP <= 0;
                bool actorBDead = ev.ActorB != null && ev.ActorB.HP <= 0;

                if (!actorADead && !actorBDead)
                {
                    StartCoroutine(MoveTransform(objA.transform, clashPosA, posA, returnDuration));
                    StartCoroutine(MoveTransform(objB.transform, clashPosB, posB, returnDuration));
                    yield return new WaitForSeconds(returnDuration);
                }
            }
        }

        private IEnumerator PlayUtility(Event_Utility ev)
        {
            if (ev.IsCancelled)
                yield break;

            GameObject actorObj = GetFieldObject(ev.Actor);
            if (actorObj != null)
            {
                Animator anim = actorObj.GetComponentInChildren<Animator>();
                if (anim != null)
                    anim.SetTrigger("Attack");

                StartCoroutine(FlashColor(actorObj, Color.yellow, 0.3f));
                yield return new WaitForSeconds(0.5f);
            }
        }

        private GameObject GetFieldObject(ICombatant combatant)
        {
            if (combatant is FieldCombatantAdapter fieldCombatant)
                return fieldCombatant.FieldObject;

            return null;
        }

        private IEnumerator MoveTransform(Transform tf, Vector3 start, Vector3 end, float duration)
        {
            if (tf == null)
                yield break;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float normalizedTime = Mathf.Sin((t / duration) * Mathf.PI * 0.5f);
                tf.position = Vector3.Lerp(start, end, normalizedTime);
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