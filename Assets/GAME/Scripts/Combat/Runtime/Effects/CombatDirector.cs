// 위치: GAME/Scripts/Combat/Runtime/Effects/CombatDirector.cs
using System.Collections;
using UnityEngine;
using Game.Combat.Core;
using Game.Combat.Model;
using Game.Combat.Adapters;

namespace Game.Combat.Effects
{
    /// <summary>
    /// 전투 중 발생하는 이벤트(일방 공격, 합)의 애니메이션과 이동 연출을 총괄하는 디렉터입니다.
    /// </summary>
    public sealed class CombatDirector : MonoBehaviour
    {
        [Header("Animation Settings")]
        [Tooltip("목표에게 다가갈 때 멈춰 설 거리 (너무 딱 붙지 않도록)")]
        [SerializeField] private float stopDistance = 1.5f;
        [Tooltip("목표를 향해 돌진하는 데 걸리는 시간")]
        [SerializeField] private float approachDuration = 0.25f;
        [Tooltip("공격 후 원래 자리로 되돌아가는 데 걸리는 시간")]
        [SerializeField] private float returnDuration = 0.3f;

        // ==========================================================
        // 🎬 메인 연출 코루틴 (일방 공격)
        // ==========================================================
        public IEnumerator PlayUnopposed(Event_Unopposed ev)
        {
            if (ev.IsCancelled || ev.LackOfInspiration)
            {
                Debug.Log($"[Director] 🚫 {ev.Actor?.Id}의 행동 취소됨.");
                yield return new WaitForSeconds(0.3f);
                yield break;
            }

            GameObject actorObj = GetFieldObject(ev.Actor);
            GameObject targetObj = GetFieldObject(ev.Target);

            if (actorObj != null && targetObj != null)
            {
                Vector3 originalPos = actorObj.transform.position;
                Vector3 targetPos = targetObj.transform.position;

                // 타겟을 향한 방향 벡터 계산
                Vector3 dir = (targetPos - originalPos).normalized;
                // 타겟 바로 앞(stopDistance)까지만 이동
                Vector3 attackPos = targetPos - (dir * stopDistance);

                // 1. [돌진]
                yield return StartCoroutine(MoveTransform(actorObj.transform, originalPos, attackPos, approachDuration));

                // 2. [타격 애니메이션]
                var anim = actorObj.GetComponentInChildren<Animator>();
                if (anim != null) anim.SetTrigger("Attack");
                yield return new WaitForSeconds(0.15f);

                // 3. [피격 연출] (빨간색 깜빡임)
                StartCoroutine(FlashColor(targetObj, Color.red, 0.2f));
                Debug.Log($"💥 [Director] {actorObj.name}의 일방 공격! -> {targetObj.name} ({ev.DamageDealt} 데미지)");

                yield return new WaitForSeconds(0.3f);

                // 🌟 4. [막타 판정] 대상이 죽었다면 원래 자리로 돌아가지 않고 종료!
                if (ev.Target.HP <= 0)
                {
                    Debug.Log($"[Director] 💀 {targetObj.name} 처치! {actorObj.name}가 복귀하지 않고 해당 위치에 머뭅니다.");
                    yield break;
                }

                // 5. [복귀] 대상이 살아있을 때만 원래 자리로 돌아감
                yield return StartCoroutine(MoveTransform(actorObj.transform, attackPos, originalPos, returnDuration));
            }
        }

        // ==========================================================
        // 🎬 메인 연출 코루틴 (합 - Clash)
        // ==========================================================
        public IEnumerator PlayClash(Event_Clash ev)
        {
            GameObject objA = GetFieldObject(ev.ActorA);
            GameObject objB = GetFieldObject(ev.ActorB);

            if (objA != null && objB != null)
            {
                Vector3 posA = objA.transform.position;
                Vector3 posB = objB.transform.position;

                // 두 캐릭터의 중간 지점 계산
                Vector3 center = (posA + posB) / 2f;
                Vector3 dirA = (center - posA).normalized;
                Vector3 dirB = (center - posB).normalized;

                Vector3 clashPosA = center - (dirA * (stopDistance * 0.5f));
                Vector3 clashPosB = center - (dirB * (stopDistance * 0.5f));

                // 1. [동시 돌진]
                Coroutine moveA = StartCoroutine(MoveTransform(objA.transform, posA, clashPosA, approachDuration));
                Coroutine moveB = StartCoroutine(MoveTransform(objB.transform, posB, clashPosB, approachDuration));
                yield return moveA;
                yield return moveB;

                // 2. [합 격돌 애니메이션]
                var animA = objA.GetComponentInChildren<Animator>();
                var animB = objB.GetComponentInChildren<Animator>();
                if (animA != null) animA.SetTrigger("Attack");
                if (animB != null) animB.SetTrigger("Attack");

                yield return new WaitForSeconds(0.15f);
                Debug.Log($"⚔️ [Director] {objA.name}(위력:{ev.PowerA}) vs {objB.name}(위력:{ev.PowerB}) 합 격돌!");

                // 3. [패자 피격 판정]
                if (ev.Loser != null)
                {
                    GameObject loserObj = GetFieldObject(ev.Loser);
                    if (loserObj != null)
                    {
                        StartCoroutine(FlashColor(loserObj, Color.red, 0.2f));
                        Debug.Log($"🩸 [Director] 합 패배: {loserObj.name} ({ev.DamageDealtToLoser} 데미지)");
                    }
                }

                yield return new WaitForSeconds(0.4f);

                // 🌟 4. [막타 판정] 누군가 죽었다면 복귀 연출을 스킵!
                bool actorADead = ev.ActorA.HP <= 0;
                bool actorBDead = ev.ActorB.HP <= 0;

                if (!actorADead && !actorBDead)
                {
                    // 둘 다 살았을 때만 각자의 자리로 동시 복귀
                    StartCoroutine(MoveTransform(objA.transform, clashPosA, posA, returnDuration));
                    StartCoroutine(MoveTransform(objB.transform, clashPosB, posB, returnDuration));
                    yield return new WaitForSeconds(returnDuration);
                }
                else
                {
                    Debug.Log($"[Director] 💀 합 결과 누군가 처치됨! 생존자는 제자리로 돌아가지 않습니다.");
                }
            }
        }

        // ==========================================================
        // 🛠️ 헬퍼(Helper) 함수들
        // ==========================================================

        /// <summary>
        /// ICombatant 데이터로부터 실제 씬에 존재하는 필드 게임 오브젝트를 가져옵니다.
        /// </summary>
        private GameObject GetFieldObject(ICombatant combatant)
        {
            if (combatant is FieldCombatantAdapter adapter && adapter.FieldObject != null)
            {
                return adapter.FieldObject;
            }
            return null;
        }

        /// <summary>
        /// Transform을 start에서 end까지 부드럽게(Lerp) 이동시키는 코루틴입니다.
        /// </summary>
        private IEnumerator MoveTransform(Transform targetTransform, Vector3 start, Vector3 end, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                // Ease-out 효과 (갈수록 살짝 느려짐)
                float normalizedTime = Mathf.Sin((t / duration) * Mathf.PI * 0.5f);
                targetTransform.position = Vector3.Lerp(start, end, normalizedTime);
                yield return null;
            }
            targetTransform.position = end; // 오차 보정
        }

        /// <summary>
        /// 피격 시 스프라이트의 색상을 잠깐 변경했다가 되돌리는 코루틴입니다.
        /// </summary>
        private IEnumerator FlashColor(GameObject target, Color flashColor, float duration)
        {
            // 타겟 안에서 SpriteRenderer 찾기 (모델 구조에 따라 수정 가능)
            var sprite = target.GetComponentInChildren<SpriteRenderer>();
            if (sprite == null) yield break;

            Color originalColor = sprite.color;
            sprite.color = flashColor;

            yield return new WaitForSeconds(duration);

            sprite.color = originalColor;
        }
    }
}