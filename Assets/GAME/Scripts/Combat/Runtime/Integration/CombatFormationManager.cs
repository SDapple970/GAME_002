// 위치: GAME/Scripts/Combat/Integration/CombatFormationManager.cs
using System.Collections;
using UnityEngine;
using Game.Combat.Core;
using Game.Combat.Model;
using Game.Combat.Adapters;

namespace Game.Combat.Integration
{
    /// <summary>
    /// 전투 발생 위치(중심점)를 기준으로 아군과 적군의 위치를 동적으로 계산하여 배치합니다.
    /// </summary>
    public sealed class CombatFormationManager : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private CombatEntryPoint entryPoint;

        [Header("Dynamic Formation Settings")]
        [Tooltip("전투 중심점에서 아군(1번)이 왼쪽으로 떨어질 거리")]
        [SerializeField] private float allyStartX = -3f;
        [Tooltip("전투 중심점에서 적군(1번)이 오른쪽으로 떨어질 거리")]
        [SerializeField] private float enemyStartX = 3f;
        [Tooltip("같은 진영 캐릭터 간의 간격 (2번, 3번 캐릭터가 얼마나 뒤로 갈지)")]
        [SerializeField] private float spacing = 1.5f;

        [Header("Animation Settings")]
        [SerializeField] private bool autoFlipCharacters = true;
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
            if (session.Allies.Count == 0 || session.Enemies.Count == 0) return;

            // 1. 전투의 중심점 계산 (카메라가 비추는 곳과 동일한 위치)
            Vector3 centerPos = GetCenterPosition(session);

            // 2. 아군 배치 (중심에서 왼쪽으로, 인덱스가 커질수록 더 왼쪽으로)
            for (int i = 0; i < session.Allies.Count; i++)
            {
                if (session.Allies[i] is FieldCombatantAdapter adapter && adapter.FieldObject != null)
                {
                    Vector3 dest = centerPos + new Vector3(allyStartX - (i * spacing), 0, 0);
                    StartCoroutine(Co_MoveToFormation(adapter.FieldObject.transform, dest, true));
                }
            }

            // 3. 적군 배치 (중심에서 오른쪽으로, 인덱스가 커질수록 더 오른쪽으로)
            for (int i = 0; i < session.Enemies.Count; i++)
            {
                if (session.Enemies[i] is FieldCombatantAdapter adapter && adapter.FieldObject != null)
                {
                    Vector3 dest = centerPos + new Vector3(enemyStartX + (i * spacing), 0, 0);
                    StartCoroutine(Co_MoveToFormation(adapter.FieldObject.transform, dest, false));
                }
            }
        }

        private Vector3 GetCenterPosition(CombatSession session)
        {
            // 첫 번째 아군과 첫 번째 적군의 현재 필드 위치를 기준으로 중간 지점을 구함
            var allyObj = ((FieldCombatantAdapter)session.Allies[0]).FieldObject;
            var enemyObj = ((FieldCombatantAdapter)session.Enemies[0]).FieldObject;

            if (allyObj != null && enemyObj != null)
            {
                return (allyObj.transform.position + enemyObj.transform.position) / 2f;
            }
            return Vector3.zero;
        }

        private IEnumerator Co_MoveToFormation(Transform targetTransform, Vector3 destination, bool isAlly)
        {
            Vector3 startPos = targetTransform.position;
            float t = 0f;

            // 방향 전환 (아군은 오른쪽, 적군은 왼쪽 보기)
            if (autoFlipCharacters)
            {
                var scale = targetTransform.localScale;
                scale.x = isAlly ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
                targetTransform.localScale = scale;
            }

            // 부드러운 이동 연출
            while (t < moveDuration)
            {
                t += Time.deltaTime;
                float normalizedTime = Mathf.Sin((t / moveDuration) * Mathf.PI * 0.5f);
                targetTransform.position = Vector3.Lerp(startPos, destination, normalizedTime);
                yield return null;
            }

            targetTransform.position = destination; // 오차 보정
        }
    }
}