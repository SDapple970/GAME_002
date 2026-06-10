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

        public void Lock()
        {
            if (_locked)
                return;

            _locked = true;

            _previousBehaviourEnabled.Clear();
            for (int i = 0; i < behavioursToDisable.Count; i++)
            {
                Behaviour behaviour = behavioursToDisable[i];
                if (behaviour == null)
                    continue;

                _previousBehaviourEnabled[behaviour] = behaviour.enabled;
                behaviour.enabled = false;
            }

            _previousActiveSelf.Clear();
            for (int i = 0; i < gameObjectsToDisable.Count; i++)
            {
                GameObject go = gameObjectsToDisable[i];
                if (go == null)
                    continue;

                _previousActiveSelf[go] = go.activeSelf;
                go.SetActive(false);
            }

            _previousColliderEnabled.Clear();
            for (int i = 0; i < disableColliders2D.Count; i++)
            {
                Collider2D collider2d = disableColliders2D[i];
                if (collider2d == null)
                    continue;

                _previousColliderEnabled[collider2d] = collider2d.enabled;
                collider2d.enabled = false;
            }

            _previousBodies.Clear();
            for (int i = 0; i < freezeBodies2D.Count; i++)
            {
                Rigidbody2D body = freezeBodies2D[i];
                if (body == null)
                    continue;

                _previousBodies[body] = (body.linearVelocity, body.angularVelocity, body.simulated);
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        public void Unlock()
        {
            if (!_locked)
                return;

            _locked = false;

            foreach (KeyValuePair<Behaviour, bool> pair in _previousBehaviourEnabled)
            {
                if (pair.Key != null)
                    pair.Key.enabled = pair.Value;
            }
            _previousBehaviourEnabled.Clear();

            foreach (KeyValuePair<GameObject, bool> pair in _previousActiveSelf)
            {
                if (pair.Key != null)
                    pair.Key.SetActive(pair.Value);
            }
            _previousActiveSelf.Clear();

            foreach (KeyValuePair<Collider2D, bool> pair in _previousColliderEnabled)
            {
                if (pair.Key != null)
                    pair.Key.enabled = pair.Value;
            }
            _previousColliderEnabled.Clear();

            foreach (KeyValuePair<Rigidbody2D, (Vector2 velocity, float angularVelocity, bool simulated)> pair in _previousBodies)
            {
                Rigidbody2D body = pair.Key;
                if (body == null)
                    continue;

                body.linearVelocity = pair.Value.velocity;
                body.angularVelocity = pair.Value.angularVelocity;
                body.simulated = pair.Value.simulated;
            }
            _previousBodies.Clear();
        }
    }
}
