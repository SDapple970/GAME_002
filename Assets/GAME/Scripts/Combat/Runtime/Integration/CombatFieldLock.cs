using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Combat.Integration
{
    public sealed class CombatFieldLock : MonoBehaviour
    {
        [Header("Disable while combat UI/action owns control")]
        [FormerlySerializedAs("disableBehaviours")]
        [SerializeField] private List<Behaviour> behavioursToDisable = new();

        [SerializeField] private List<GameObject> gameObjectsToDisable = new();
        [SerializeField] private List<Rigidbody2D> freezeBodies2D = new();
        [SerializeField] private List<Collider2D> disableColliders2D = new();

        private readonly Dictionary<Behaviour, bool> _previousBehaviourEnabled = new();
        private readonly Dictionary<GameObject, bool> _previousActiveSelf = new();
        private readonly Dictionary<Collider2D, bool> _previousColliderEnabled = new();
        private readonly Dictionary<Rigidbody2D, (Vector2 velocity, float angularVelocity, bool simulated)> _previousBodies = new();

        private bool _locked;
        private bool _missingReferenceWarned;

        internal bool IsLocked => _locked;

        public void Lock()
        {
            LockRuntimeTargets(null, null, null);
        }

        internal void LockRuntimeTargets(
            IEnumerable<Behaviour> runtimeBehaviours,
            IEnumerable<Rigidbody2D> runtimeBodies,
            IEnumerable<Collider2D> runtimeColliders)
        {
            if (_locked)
                return;

            _locked = true;

            _previousBehaviourEnabled.Clear();
            CaptureBehaviours(behavioursToDisable);
            CaptureBehaviours(runtimeBehaviours);

            _previousActiveSelf.Clear();
            for (int i = 0; i < gameObjectsToDisable.Count; i++)
            {
                GameObject go = gameObjectsToDisable[i];
                if (go == null)
                {
                    WarnMissingReferenceOnce();
                    continue;
                }

                if (!_previousActiveSelf.ContainsKey(go))
                    _previousActiveSelf[go] = go.activeSelf;
                go.SetActive(false);
            }

            _previousColliderEnabled.Clear();
            CaptureColliders(disableColliders2D);
            CaptureColliders(runtimeColliders);

            _previousBodies.Clear();
            CaptureBodies(freezeBodies2D);
            CaptureBodies(runtimeBodies);
        }

        public void Unlock()
        {
            UnlockExcept(null);
        }

        internal void UnlockExcept(ISet<GameObject> clearedRoots)
        {
            if (!_locked)
                return;

            _locked = false;

            foreach (KeyValuePair<Behaviour, bool> pair in _previousBehaviourEnabled)
            {
                if (pair.Key != null && !IsUnderClearedRoot(pair.Key.gameObject, clearedRoots))
                    pair.Key.enabled = pair.Value;
            }
            _previousBehaviourEnabled.Clear();

            foreach (KeyValuePair<GameObject, bool> pair in _previousActiveSelf)
            {
                if (pair.Key != null && !IsUnderClearedRoot(pair.Key, clearedRoots))
                    pair.Key.SetActive(pair.Value);
            }
            _previousActiveSelf.Clear();

            foreach (KeyValuePair<Collider2D, bool> pair in _previousColliderEnabled)
            {
                if (pair.Key != null && !IsUnderClearedRoot(pair.Key.gameObject, clearedRoots))
                    pair.Key.enabled = pair.Value;
            }
            _previousColliderEnabled.Clear();

            foreach (KeyValuePair<Rigidbody2D, (Vector2 velocity, float angularVelocity, bool simulated)> pair in _previousBodies)
            {
                Rigidbody2D body = pair.Key;
                if (body == null || IsUnderClearedRoot(body.gameObject, clearedRoots))
                    continue;

                body.simulated = pair.Value.simulated;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            _previousBodies.Clear();
        }

        private void CaptureBehaviours(IEnumerable<Behaviour> behaviours)
        {
            if (behaviours == null)
                return;

            foreach (Behaviour behaviour in behaviours)
            {
                if (behaviour == null)
                {
                    WarnMissingReferenceOnce();
                    continue;
                }

                if (_previousBehaviourEnabled.ContainsKey(behaviour))
                    continue;

                _previousBehaviourEnabled[behaviour] = behaviour.enabled;
                behaviour.enabled = false;
            }
        }

        private void CaptureColliders(IEnumerable<Collider2D> colliders)
        {
            if (colliders == null)
                return;

            foreach (Collider2D collider2d in colliders)
            {
                if (collider2d == null)
                {
                    WarnMissingReferenceOnce();
                    continue;
                }

                if (_previousColliderEnabled.ContainsKey(collider2d))
                    continue;

                _previousColliderEnabled[collider2d] = collider2d.enabled;
                collider2d.enabled = false;
            }
        }

        private void CaptureBodies(IEnumerable<Rigidbody2D> bodies)
        {
            if (bodies == null)
                return;

            foreach (Rigidbody2D body in bodies)
            {
                if (body == null)
                {
                    WarnMissingReferenceOnce();
                    continue;
                }

                if (_previousBodies.ContainsKey(body))
                    continue;

                _previousBodies[body] = (body.linearVelocity, body.angularVelocity, body.simulated);
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.simulated = false;
            }
        }

        private static bool IsUnderClearedRoot(GameObject target, ISet<GameObject> clearedRoots)
        {
            if (target == null || clearedRoots == null || clearedRoots.Count == 0)
                return false;

            foreach (GameObject root in clearedRoots)
            {
                if (root == null)
                    continue;

                if (target == root || target.transform.IsChildOf(root.transform))
                    return true;
            }

            return false;
        }

        private void WarnMissingReferenceOnce()
        {
            if (_missingReferenceWarned)
                return;

            _missingReferenceWarned = true;
            Debug.LogWarning("[CombatFieldLock] A configured or runtime lock target is missing. Other valid targets will still be locked.", this);
        }
    }
}
