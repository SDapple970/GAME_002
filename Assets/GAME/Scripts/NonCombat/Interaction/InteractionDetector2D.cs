using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.NonCombat.Interaction
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class InteractionDetector2D : MonoBehaviour
    {
        private readonly List<IInteractable> _candidates = new();
        private bool _inputSubscribed;

        private void Awake()
        {
            Collider2D trigger = GetComponent<Collider2D>();
            if (trigger != null && !trigger.isTrigger)
                Debug.LogWarning("[InteractionDetector2D] Collider2D should be configured as Trigger.", this);
        }

        private void OnEnable()
        {
            SubscribeInput();
        }

        private void OnDisable()
        {
            if (_inputSubscribed && GameInputInstaller.Instance != null)
                GameInputInstaller.Instance.Interact -= HandleInteract;

            _inputSubscribed = false;
        }

        private void Start()
        {
            SubscribeInput();
        }

        private void SubscribeInput()
        {
            if (_inputSubscribed || GameInputInstaller.Instance == null)
                return;

            GameInputInstaller.Instance.Interact += HandleInteract;
            _inputSubscribed = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (TryGetInteractable(other, out IInteractable interactable) && !_candidates.Contains(interactable))
                _candidates.Add(interactable);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (TryGetInteractable(other, out IInteractable interactable))
                _candidates.Remove(interactable);
        }

        private void HandleInteract()
        {
            if (GameStateMachine.Instance != null && !GameStateMachine.Instance.Is(GameState.Exploration))
                return;

            IInteractable nearest = FindNearest();
            if (nearest != null && nearest.CanInteract)
                nearest.Interact();
        }

        private IInteractable FindNearest()
        {
            IInteractable nearest = null;
            float nearestDistance = float.MaxValue;

            for (int i = _candidates.Count - 1; i >= 0; i--)
            {
                IInteractable candidate = _candidates[i];
                if (candidate == null)
                {
                    _candidates.RemoveAt(i);
                    continue;
                }

                if (!candidate.CanInteract)
                    continue;

                Component component = candidate as Component;
                if (component == null)
                    continue;

                float distance = ((Vector2)component.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        private static bool TryGetInteractable(Collider2D collider, out IInteractable interactable)
        {
            interactable = null;
            MonoBehaviour[] behaviours = collider.GetComponentsInParent<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IInteractable candidate)
                {
                    interactable = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
