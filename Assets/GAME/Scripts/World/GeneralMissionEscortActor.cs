using System;
using System.Collections.Generic;
using Game.Interaction;
using Game.NonCombat.Interaction;
using Game.Quest;
using UnityEngine;

namespace Game.World
{
    /// <summary>Field companion, outside the three combat party slots. Damage/hazards
    /// submit scoped outcomes; this actor never grants a reward or writes global state.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class GeneralMissionEscortActor : GeneralMissionBinding, IInteractable, INonCombatInteractable
    {
        [SerializeField] private string targetId = "rescue_target";
        [SerializeField] private Transform followTarget;
        [SerializeField] private string playerTag = "Player";
        [Min(0.1f)] [SerializeField] private float moveSpeed = 3f;
        [Min(0.1f)] [SerializeField] private float stopDistance = 1f;
        private Rigidbody2D _body;
        private Vector3 _initialPosition;
        private string _appliedRun;
        private int _appliedAttempt;
        private readonly List<Vector3> _path = new();
        public bool IsFollowing => HasActiveRun && Snapshot.Following;
        public int CurrentHp => Snapshot?.RescueHp ?? 0;
        public bool CanInteract => AllowsFieldAction && HasActiveRun && Snapshot.Kind == GeneralMissionKind.Rescue &&
            !Snapshot.Following && Snapshot.RescueHp > 0;
        public string PromptText => "구출하고 동행";

        private void Awake() { _body = GetComponent<Rigidbody2D>(); _initialPosition = transform.position; }
        public void Interact() => Interact(null);
        public void Interact(GameObject interactor)
        {
            if (!CanInteract) return;
            if (followTarget == null && interactor != null) followTarget = interactor.transform;
            if (followTarget == null) return;
            _path.Clear(); _path.Add(followTarget.position);
            questRuntime.TryRescueMissionTarget(questId, Snapshot.RunId, Snapshot.Attempt, targetId);
            Vector3 position = _body != null ? new Vector3(_body.position.x, _body.position.y, transform.position.z) : transform.position;
            questRuntime.UpdateMissionEscortPosition(questId, Snapshot.RunId, Snapshot.Attempt, position, _path);
        }

        public bool ApplyDamage(int amount, string outcomeId) => amount > 0 && ChangeHp(-amount, outcomeId);
        public bool ApplyHealing(int amount, string outcomeId) => amount > 0 && ChangeHp(amount, outcomeId);
        private bool ChangeHp(int delta, string outcomeId) => HasActiveRun &&
            questRuntime.TryChangeMissionRescueHp(questId, Snapshot.RunId, Snapshot.Attempt, delta, outcomeId);

        public bool TryEscape(string exitId, Vector3 exitPosition, float maximumDistance)
        {
            if (!AllowsFieldAction || !IsFollowing || maximumDistance <= 0 ||
                Vector2.Distance(transform.position, exitPosition) > maximumDistance) return false;
            return questRuntime.TryExitRescueMission(questId, Snapshot.RunId, Snapshot.Attempt, exitId);
        }

        protected override void ApplyState()
        {
            if (Snapshot == null) { _appliedRun = null; _appliedAttempt = 0; return; }
            if (Snapshot.Following && followTarget == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag(playerTag);
                if (player != null) followTarget = player.transform;
            }
            if (_appliedRun != Snapshot.RunId || _appliedAttempt != Snapshot.Attempt)
            {
                _appliedRun = Snapshot.RunId; _appliedAttempt = Snapshot.Attempt;
                transform.position = questRuntime.TryGetMissionEscortPosition(questId, out Vector3 saved) ? saved : _initialPosition;
                if (_body != null) _body.position = transform.position;
                _path.Clear(); _path.AddRange(questRuntime.GetMissionEscortPath(questId));
            }
        }

        protected override void BeforeRestore() { _appliedRun = null; _appliedAttempt = 0; }

        private void FixedUpdate()
        {
            if (_body == null) return;
            if (!AllowsFieldAction || !IsFollowing || followTarget == null) { _body.linearVelocity = Vector2.zero; return; }
            // Follow the player's walked trail instead of cutting across wall corners.
            if (_path.Count == 0 || Vector2.Distance(_path[_path.Count - 1], followTarget.position) >= 0.25f)
            {
                if (_path.Count >= 4096) { _body.linearVelocity = Vector2.zero; return; }
                _path.Add(followTarget.position);
            }
            while (_path.Count > 1 && Vector2.Distance(_body.position, _path[0]) < 0.1f) _path.RemoveAt(0);
            Vector2 delta = (Vector2)_path[0] - _body.position;
            float allowedDistance = _path.Count > 1 ? delta.magnitude : Mathf.Max(0, delta.magnitude - stopDistance);
            if (allowedDistance > 0)
                _body.MovePosition(_body.position + delta.normalized * Mathf.Min(moveSpeed * Time.fixedDeltaTime, allowedDistance));
            else _body.linearVelocity = Vector2.zero;
            questRuntime.UpdateMissionEscortPosition(questId, Snapshot.RunId, Snapshot.Attempt,
                new Vector3(_body.position.x, _body.position.y, transform.position.z), _path);
        }
    }
}
