using System.Collections;
using UnityEngine;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Model;

namespace Game.Combat.Integration
{
    public sealed class CombatFormationManager : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private CombatEntryPoint entryPoint;
        [SerializeField] private bool enableFormationPlacement = false;

        [Header("Dynamic Formation Settings")]
        [SerializeField] private float allyStartX = -3f;
        [SerializeField] private float enemyStartX = 3f;
        [SerializeField] private float spacing = 1.5f;

        [Header("Animation Settings")]
        [SerializeField] private bool autoFlipCharacters = true;
        [SerializeField] private float moveDuration = 0.4f;

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
            if (!enableFormationPlacement || session == null)
                return;

            if (session.Allies.Count == 0 || session.Enemies.Count == 0)
                return;

            Vector3 centerPosition = GetCenterPosition(session);

            for (int i = 0; i < session.Allies.Count; i++)
            {
                FieldCombatantAdapter adapter = session.Allies[i] as FieldCombatantAdapter;
                if (adapter == null || adapter.FieldObject == null)
                    continue;

                Vector3 destination = centerPosition + new Vector3(allyStartX - (i * spacing), 0f, 0f);
                StartCoroutine(Co_MoveToFormation(adapter.FieldObject.transform, destination, true));
            }

            for (int i = 0; i < session.Enemies.Count; i++)
            {
                FieldCombatantAdapter adapter = session.Enemies[i] as FieldCombatantAdapter;
                if (adapter == null || adapter.FieldObject == null)
                    continue;

                Vector3 destination = centerPosition + new Vector3(enemyStartX + (i * spacing), 0f, 0f);
                StartCoroutine(Co_MoveToFormation(adapter.FieldObject.transform, destination, false));
            }
        }

        private static Vector3 GetCenterPosition(CombatSession session)
        {
            FieldCombatantAdapter ally = session.Allies[0] as FieldCombatantAdapter;
            FieldCombatantAdapter enemy = session.Enemies[0] as FieldCombatantAdapter;

            if (ally != null && ally.FieldObject != null && enemy != null && enemy.FieldObject != null)
                return (ally.FieldObject.transform.position + enemy.FieldObject.transform.position) * 0.5f;

            return Vector3.zero;
        }

        private IEnumerator Co_MoveToFormation(Transform targetTransform, Vector3 destination, bool isAlly)
        {
            if (targetTransform == null)
                yield break;

            Vector3 startPosition = targetTransform.position;
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, moveDuration);

            if (autoFlipCharacters)
            {
                Vector3 scale = targetTransform.localScale;
                scale.x = isAlly ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
                targetTransform.localScale = scale;
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.Sin(normalizedTime * Mathf.PI * 0.5f);
                targetTransform.position = Vector3.Lerp(startPosition, destination, eased);
                yield return null;
            }

            targetTransform.position = destination;
        }
    }
}
