using System;
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>Optional field damage source. Each physical contact has one outcome;
    /// authored combat/story integrations can call the actor's scoped damage API too.</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class GeneralMissionEscortHazard : MonoBehaviour
    {
        [SerializeField] private string hazardId;
        [Min(1)] [SerializeField] private int damage = 10;
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || other.isTrigger || string.IsNullOrWhiteSpace(hazardId)) return;
            if (GameStateMachine.Instance != null && !GameStateMachine.Instance.Is(GameState.Exploration)) return;
            GeneralMissionEscortActor actor = other.GetComponentInParent<GeneralMissionEscortActor>();
            if (actor != null) actor.ApplyDamage(damage, $"hazard:{hazardId}:{Guid.NewGuid():N}");
        }
    }
}
