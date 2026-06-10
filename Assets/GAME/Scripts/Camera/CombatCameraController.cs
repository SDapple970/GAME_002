using System.Collections;
using UnityEngine;
using Game.CameraSys;
using Game.Combat.Adapters;
using Game.Combat.Model;

namespace Game.Combat.Integration
{
    public sealed class CombatCameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private MonoBehaviour explorationCameraFollow;
        [SerializeField, HideInInspector] private CameraFollow2D mainCamera;

        [Header("Framing")]
        [SerializeField] private float transitionDuration = 0.35f;
        [SerializeField] private float combatPadding = 2f;
        [SerializeField] private float minOrthoSize = 4f;
        [SerializeField] private float maxOrthoSize = 8f;
        [SerializeField] private Vector3 combatOffset = new Vector3(0f, 0.8f, 0f);

        private CombatSession _session;
        private Coroutine _moveRoutine;

        public void EnterCombat(CombatSession session)
        {
            EnsureReferences();

            _session = session;

            if (explorationCameraFollow != null)
                explorationCameraFollow.enabled = false;

            FocusPlanning();
        }

        public void FocusPlanning()
        {
            EnsureReferences();

            if (targetCamera == null || _session == null)
                return;

            if (!TryGetPrimaryPositions(_session, out Vector3 playerPosition, out Vector3 enemyPosition))
                return;

            FramePositions(playerPosition, enemyPosition);
        }

        public void FocusAction(ICombatant actor, ICombatant target)
        {
            EnsureReferences();

            if (targetCamera == null)
                return;

            if (!TryGetPosition(actor, out Vector3 actorPosition))
                return;

            if (!TryGetPosition(target, out Vector3 targetPosition))
                targetPosition = actorPosition;

            FramePositions(actorPosition, targetPosition);
        }

        public void HoldResultFrame()
        {
            if (explorationCameraFollow != null)
                explorationCameraFollow.enabled = false;
        }

        public void ExitToExplorationFollow()
        {
            StopActiveRoutine();

            if (explorationCameraFollow != null)
                explorationCameraFollow.enabled = true;

            _session = null;
        }

        private void EnsureReferences()
        {
            if (targetCamera == null)
            {
                if (mainCamera != null)
                    targetCamera = mainCamera.GetComponent<Camera>();

                if (targetCamera == null)
                    targetCamera = GetComponent<Camera>();

                if (targetCamera == null)
                    targetCamera = Camera.main;
            }

            if (explorationCameraFollow == null && mainCamera != null)
                explorationCameraFollow = mainCamera;
        }

        private void FramePositions(Vector3 a, Vector3 b)
        {
            Vector3 center = (a + b) * 0.5f;
            Vector3 cameraPosition = center + combatOffset;
            cameraPosition.z = targetCamera.transform.position.z;

            float size = CalculateOrthoSize(a, b);
            StartCameraMove(cameraPosition, size);
        }

        private float CalculateOrthoSize(Vector3 a, Vector3 b)
        {
            float aspect = targetCamera != null && targetCamera.aspect > 0f ? targetCamera.aspect : 1.777f;

            Bounds bounds = new Bounds(a, Vector3.zero);
            bounds.Encapsulate(b);

            float verticalSize = bounds.extents.y + combatPadding;
            float horizontalSize = (bounds.extents.x + combatPadding) / aspect;
            float targetSize = Mathf.Max(verticalSize, horizontalSize, minOrthoSize);

            return Mathf.Clamp(targetSize, minOrthoSize, maxOrthoSize);
        }

        private void StartCameraMove(Vector3 position, float orthoSize)
        {
            StopActiveRoutine();

            if (transitionDuration <= 0.001f)
            {
                targetCamera.transform.position = position;
                targetCamera.orthographicSize = orthoSize;
                return;
            }

            _moveRoutine = StartCoroutine(Co_MoveCamera(position, orthoSize));
        }

        private IEnumerator Co_MoveCamera(Vector3 endPosition, float endSize)
        {
            Vector3 startPosition = targetCamera.transform.position;
            float startSize = targetCamera.orthographicSize;
            float elapsed = 0f;

            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / transitionDuration);
                float eased = Mathf.SmoothStep(0f, 1f, t);

                targetCamera.transform.position = Vector3.Lerp(startPosition, endPosition, eased);
                targetCamera.orthographicSize = Mathf.Lerp(startSize, endSize, eased);
                yield return null;
            }

            targetCamera.transform.position = endPosition;
            targetCamera.orthographicSize = endSize;
            _moveRoutine = null;
        }

        private void StopActiveRoutine()
        {
            if (_moveRoutine == null)
                return;

            StopCoroutine(_moveRoutine);
            _moveRoutine = null;
        }

        private static bool TryGetPrimaryPositions(CombatSession session, out Vector3 playerPosition, out Vector3 enemyPosition)
        {
            playerPosition = Vector3.zero;
            enemyPosition = Vector3.zero;

            if (session == null || session.Allies.Count == 0)
                return false;

            ICombatant player = session.Allies[0];
            if (!TryGetPosition(player, out playerPosition))
                return false;

            for (int i = 0; i < session.Enemies.Count; i++)
            {
                ICombatant enemy = session.Enemies[i];
                if (enemy == null || enemy.HP <= 0)
                    continue;

                if (TryGetPosition(enemy, out enemyPosition))
                    return true;
            }

            enemyPosition = playerPosition;
            return true;
        }

        private static bool TryGetPosition(ICombatant combatant, out Vector3 position)
        {
            position = Vector3.zero;

            FieldCombatantAdapter adapter = combatant as FieldCombatantAdapter;
            if (adapter == null || adapter.FieldObject == null)
                return false;

            position = adapter.FieldObject.transform.position;
            return true;
        }
    }
}
