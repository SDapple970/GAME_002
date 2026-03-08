// 위치: GAME/Scripts/Combat/Integration/CombatFormationManager.cs
using System.Collections;
using UnityEngine;
using Game.Combat.Core;
using Game.Combat.Model;
using Game.Combat.Adapters;

namespace Game.Combat.Integration
{
    public sealed class CombatFormationManager : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private CombatEntryPoint entryPoint;

        [Header("Formation Anchors")]
        [SerializeField] private Transform[] allyPositions;
        [SerializeField] private Transform[] enemyPositions;

        [Header("Animation Settings")]
        [SerializeField] private bool autoFlipCharacters = true;
        [Tooltip("진형을 잡기 위해 이동하는 데 걸리는 시간")]
        [SerializeField] private float moveDuration = 0.4f;

        private void OnEnable()
        {
            if (entryPoint != null) entryPoint.OnCombatStarted += HandleCombatStarted;
        }

        private void OnDisable()
        {
            if (entryPoint != null) entryPoint.OnCombatStarted -= HandleCombatStarted;
        }

        private void HandleCombatStarted(CombatSession session)
        {
            // 아군 배치 (코루틴으로 부드럽게 이동)
            for (int i = 0; i < session.Allies.Count; i++)
            {
                if (i >= allyPositions.Length) break;
                if (session.Allies[i] is FieldCombatantAdapter adapter && adapter.FieldObject != null)
                {
                    StartCoroutine(Co_MoveToFormation(adapter.FieldObject.transform, allyPositions[i].position, true));
                }
            }

            // 적군 배치
            for (int i = 0; i < session.Enemies.Count; i++)
            {
                if (i >= enemyPositions.Length) break;
                if (session.Enemies[i] is FieldCombatantAdapter adapter && adapter.FieldObject != null)
                {
                    StartCoroutine(Co_MoveToFormation(adapter.FieldObject.transform, enemyPositions[i].position, false));
                }
            }
        }

        // 🌟 순간이동 대신 부드럽게 위치로 뛰어가는 연출
        private IEnumerator Co_MoveToFormation(Transform targetTransform, Vector3 destination, bool isAlly)
        {
            Vector3 startPos = targetTransform.position;
            float t = 0f;

            // 1. 방향 전환 (이동 시작 시 바라볼 방향)
            if (autoFlipCharacters)
            {
                var scale = targetTransform.localScale;
                scale.x = isAlly ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
                targetTransform.localScale = scale;
            }

            // (선택 사항) Animator에 "Run" 같은 걷기/뛰기 파라미터가 있다면 여기서 켤 수 있어!
            // var anim = targetTransform.GetComponentInChildren<Animator>();
            // if (anim != null) anim.SetBool("IsRun", true);

            // 2. 부드러운 이동 (Lerp & Ease-out)
            while (t < moveDuration)
            {
                t += Time.deltaTime;
                float normalizedTime = Mathf.Sin((t / moveDuration) * Mathf.PI * 0.5f); // 갈수록 살짝 느려지는 효과
                targetTransform.position = Vector3.Lerp(startPos, destination, normalizedTime);
                yield return null;
            }

            targetTransform.position = destination; // 최종 위치 오차 보정

            // (선택 사항) 도착했으니 애니메이션 끄기
            // if (anim != null) anim.SetBool("IsRun", false);
        }
    }
}