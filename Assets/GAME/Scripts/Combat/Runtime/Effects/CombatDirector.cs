// Scripts/Combat/Effects/CombatDirector.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Combat.Core;
using Game.Combat.Model;
using Game.Combat.Adapters;
using Game.Combat.Data; // 💡 PlaybookEvent 등 대본 클래스 접근을 위해 추가!

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

        public void PlayResolution(CombatSession session, System.Action onComplete)
        {
            StartCoroutine(Co_PlayTurnAnimation(session, onComplete));
        }

        private IEnumerator Co_PlayTurnAnimation(CombatSession session, System.Action onComplete)
        {
            if (session == null || session.CurrentTurn == null || session.CurrentTurn.Playbook.Count == 0)
            {
                // 이번 턴에 할 행동(대본)이 없으면 즉시 종료
                onComplete?.Invoke();
                yield break;
            }

            // 💡 대본(Playbook)에 적힌 사건들을 순서대로 꺼내서 읽습니다!
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

                // 각 행동이 끝날 때마다 아주 살짝 텀을 줍니다 (자연스러운 턴 진행)
                yield return new WaitForSeconds(0.2f);
            }

            // 모든 연출이 끝나면 다음 턴으로 넘기라고 시스템에 알림!
            onComplete?.Invoke();
        }

        // 🎬 1. 일방 공격 연출
        private IEnumerator PlayUnopposed(Event_Unopposed ev)
        {
            if (ev.IsCancelled || ev.LackOfInspiration)
            {
                Debug.Log($"[Director] 🚫 {ev.Actor?.Id}의 행동 취소됨. (기절/사망 또는 코스트 부족)");
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

                // [돌진]
                yield return StartCoroutine(MoveTransform(actorObj.transform, originalPos, attackPos, approachDuration));

                // [타격]
                var anim = actorObj.GetComponentInChildren<Animator>();
                if (anim != null) anim.SetTrigger("Attack");
                yield return new WaitForSeconds(0.15f); // 타격감 싱크 맞추기

                // [피격]
                StartCoroutine(FlashColor(targetObj, Color.red, 0.2f));
                Debug.Log($"💥 [Director] {actorObj.name}의 일방 공격! -> {targetObj.name} ({ev.DamageDealt} 데미지)");

                yield return new WaitForSeconds(0.3f);

                // [복귀]
                yield return StartCoroutine(MoveTransform(actorObj.transform, attackPos, originalPos, returnDuration));
            }
        }

        // 🎬 2. 합(Clash) 격돌 연출
        private IEnumerator PlayClash(Event_Clash ev)
        {
            GameObject objA = GetFieldObject(ev.ActorA);
            GameObject objB = GetFieldObject(ev.ActorB);

            if (objA != null && objB != null)
            {
                Vector3 posA = objA.transform.position;
                Vector3 posB = objB.transform.position;

                // 두 캐릭터의 중간 지점(Center) 계산
                Vector3 center = (posA + posB) / 2f;
                Vector3 dirA = (center - posA).normalized;
                Vector3 dirB = (center - posB).normalized;

                // 서로를 향해 달리되, 살짝 거리를 둡니다.
                Vector3 clashPosA = center - (dirA * (stopDistance * 0.5f));
                Vector3 clashPosB = center - (dirB * (stopDistance * 0.5f));

                // [동시 돌진]
                Coroutine moveA = StartCoroutine(MoveTransform(objA.transform, posA, clashPosA, approachDuration));
                Coroutine moveB = StartCoroutine(MoveTransform(objB.transform, posB, clashPosB, approachDuration));
                yield return moveA;
                yield return moveB;

                // [합 격돌!]
                var animA = objA.GetComponentInChildren<Animator>();
                var animB = objB.GetComponentInChildren<Animator>();
                if (animA != null) animA.SetTrigger("Attack");
                if (animB != null) animB.SetTrigger("Attack");

                yield return new WaitForSeconds(0.15f);
                Debug.Log($"⚔️ [Director] {objA.name}(위력:{ev.PowerA}) vs {objB.name}(위력:{ev.PowerB}) 합 격돌!");

                // [패자 피격 판정]
                if (ev.Loser != null)
                {
                    GameObject loserObj = GetFieldObject(ev.Loser);
                    if (loserObj != null)
                    {
                        StartCoroutine(FlashColor(loserObj, Color.red, 0.2f));
                        Debug.Log($"🩸 [Director] 합 패배: {loserObj.name} ({ev.DamageDealtToLoser} 데미지)");
                    }
                }
                else
                {
                    Debug.Log("🛡️ [Director] 합 무승부!");
                }

                yield return new WaitForSeconds(0.4f);

                // [동시 복귀]
                StartCoroutine(MoveTransform(objA.transform, clashPosA, posA, returnDuration));
                StartCoroutine(MoveTransform(objB.transform, clashPosB, posB, returnDuration));
                yield return new WaitForSeconds(returnDuration);
            }
        }

        // 🎬 3. 유틸리티/버프 스킬 연출
        private IEnumerator PlayUtility(Event_Utility ev)
        {
            if (ev.IsCancelled) yield break;

            GameObject actorObj = GetFieldObject(ev.Actor);
            if (actorObj != null)
            {
                var anim = actorObj.GetComponentInChildren<Animator>();
                if (anim != null) anim.SetTrigger("Attack"); // 나중에 Buff 애니메이션이 생기면 교체

                // 노란색으로 빛나며 제자리에서 스킬 사용
                StartCoroutine(FlashColor(actorObj, Color.yellow, 0.3f));
                Debug.Log($"✨ [Director] {actorObj.name} 유틸리티 스킬 사용!");

                yield return new WaitForSeconds(0.5f);
            }
        }

        // --- 헬퍼 메서드 ---

        private GameObject GetFieldObject(ICombatant combatant)
        {
            if (combatant is FieldCombatantAdapter fa) return fa.FieldObject;
            return null;
        }

        private IEnumerator MoveTransform(Transform tf, Vector3 start, Vector3 end, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                // Ease-Out 효과 (갈수록 약간 느려짐)
                float normalizedTime = Mathf.Sin((t / duration) * Mathf.PI * 0.5f);
                tf.position = Vector3.Lerp(start, end, normalizedTime);
                yield return null;
            }
            tf.position = end;
        }

        private IEnumerator FlashColor(GameObject obj, Color flashColor, float duration)
        {
            var sprite = obj.GetComponentInChildren<SpriteRenderer>();
            if (sprite == null) yield break;

            Color original = sprite.color;
            sprite.color = flashColor;
            yield return new WaitForSeconds(duration);
            sprite.color = original;
        }
    }
}