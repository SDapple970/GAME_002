using System;
using System.Collections.Generic;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Model;
using Game.Core;
using Game.Enemies;
using Game.Enemy.Overworld;
using Game.Player;
using UnityEngine;

namespace Game.Combat.Integration
{
    public sealed class CombatWorldLifecycleAdapter : MonoBehaviour
    {
        private static readonly Dictionary<int, CombatWorldLifecycleAdapter> OwnersByEntryId = new();

        [SerializeField] private CombatEntryPoint entryPoint;
        [SerializeField] private CombatFieldLock fieldLock;
        [SerializeField] private CombatCameraController cameraController;
        [SerializeField] private CombatFormationManager formationManager;
        [SerializeField] private bool debugLog;

        private CombatEntryPoint _subscribedEntryPoint;
        private GameStateMachine _subscribedStateMachine;
        private FieldContext _context;
        private bool _ownsEntryPoint;
        private bool _missingFieldLockWarned;
        private bool _duplicateOwnerWarned;
        private bool _mismatchedResultWarned;
        private bool _forcedVictoryWarned;
        private bool _disableRecoveryWarned;
        private int _cameraEnterCount;
        private int _cameraExitCount;
        private int _restorationCount;
        private int _outcomeApplicationCount;
        private UnityEngine.Object _preparedEncounterOwner;
        private string _preparedCompletionId;

        internal bool OwnsEntryPoint => _ownsEntryPoint;
        internal string ActiveCompletionId => _context != null ? _context.CompletionId : null;
        internal bool HasActiveContext => _context != null;
        internal int CameraEnterCount => _cameraEnterCount;
        internal int CameraExitCount => _cameraExitCount;
        internal int RestorationCount => _restorationCount;
        internal int OutcomeApplicationCount => _outcomeApplicationCount;
        internal int CapturedActorCount => _context != null ? _context.Actors.Count : 0;
        internal int CapturedEncounterCount => _context != null ? _context.EncounterOwners.Count : 0;
        internal bool IsFieldLocked => fieldLock != null && fieldLock.IsLocked;

        internal bool HasCapturedCombatant(int combatantId)
        {
            return _context != null && _context.ByCombatantId.ContainsKey(combatantId);
        }

        internal bool TryGetCapturedPosition(int combatantId, out Vector3 position)
        {
            position = Vector3.zero;
            if (_context == null || !_context.ByCombatantId.TryGetValue(combatantId, out ActorSnapshot snapshot))
                return false;

            position = snapshot.Position;
            return true;
        }

        private void Awake()
        {
            AutoBindReferences();
        }

        private void OnEnable()
        {
            AutoBindReferences();
            if (!TryClaimOwnership())
                return;

            SubscribeEntryPoint();
            SubscribeStateMachine();

            if (_context != null && GameStateMachine.Instance != null &&
                !GameStateMachine.Instance.Is(GameState.Exploration))
            {
                ApplyWorldLock(_context);
                return;
            }

            if (_context == null && entryPoint != null && entryPoint.ActiveSession != null)
            {
                Debug.LogWarning("[CombatWorldLifecycleAdapter] Enabled after combat had already started; capturing the best available late field snapshot.", this);
                HandleCombatStarted(entryPoint.ActiveSession);
            }
        }

        private void OnDisable()
        {
            UnsubscribeEntryPoint();
            UnsubscribeStateMachine();
            ReleaseOwnership();

            if (_context != null)
            {
                if (!_disableRecoveryWarned)
                {
                    _disableRecoveryWarned = true;
                    Debug.LogWarning("[CombatWorldLifecycleAdapter] Disabled with an active field context. Local locks were released for safety; re-enable will recover the retained context.", this);
                }

                RestoreSafeLockOnly();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeEntryPoint();
            UnsubscribeStateMachine();
            ReleaseOwnership();
            RestoreSafeLockOnly();
            _context = null;
        }

        internal static CombatWorldLifecycleAdapter EnsureFor(CombatEntryPoint targetEntryPoint)
        {
            if (targetEntryPoint == null)
                return null;

            CombatWorldLifecycleAdapter adapter = targetEntryPoint.GetComponent<CombatWorldLifecycleAdapter>();
            if (adapter == null)
                adapter = targetEntryPoint.gameObject.AddComponent<CombatWorldLifecycleAdapter>();

            adapter.entryPoint = targetEntryPoint;
            adapter.AutoBindReferences();
            if (adapter.isActiveAndEnabled && !adapter._ownsEntryPoint)
            {
                adapter.TryClaimOwnership();
                adapter.SubscribeEntryPoint();
                adapter.SubscribeStateMachine();
            }

            return adapter;
        }

        internal static CombatWorldLifecycleAdapter FindFor(CombatEntryPoint targetEntryPoint)
        {
            if (targetEntryPoint == null)
                return null;

            int key = targetEntryPoint.GetInstanceID();
            if (OwnersByEntryId.TryGetValue(key, out CombatWorldLifecycleAdapter owner))
            {
                if (owner != null)
                    return owner;
                OwnersByEntryId.Remove(key);
            }

            return targetEntryPoint.GetComponent<CombatWorldLifecycleAdapter>();
        }

        internal static bool OwnsSession(CombatEntryPoint targetEntryPoint, CombatSession session)
        {
            CombatWorldLifecycleAdapter owner = FindFor(targetEntryPoint);
            return owner != null && owner._ownsEntryPoint && owner._context != null && session != null &&
                   owner._context.CompletionId == session.CompletionId;
        }

        internal static void PrepareEncounterOwner(
            CombatEntryPoint targetEntryPoint,
            CombatSession session,
            UnityEngine.Object encounterOwner)
        {
            CombatWorldLifecycleAdapter owner = FindFor(targetEntryPoint);
            if (owner == null || session == null)
                return;

            owner._preparedCompletionId = session.CompletionId;
            owner._preparedEncounterOwner = encounterOwner;
        }

        internal static void ResetOwnershipForTests()
        {
            OwnersByEntryId.Clear();
        }

        private void AutoBindReferences()
        {
            if (entryPoint == null)
                entryPoint = GetComponent<CombatEntryPoint>();
            if (entryPoint == null)
                entryPoint = FindUnique<CombatEntryPoint>();

            if (fieldLock == null)
                fieldLock = GetComponent<CombatFieldLock>();
            if (fieldLock == null)
                fieldLock = FindUnique<CombatFieldLock>();
            if (fieldLock == null && entryPoint != null)
            {
                fieldLock = entryPoint.GetComponent<CombatFieldLock>();
                if (fieldLock == null)
                    fieldLock = entryPoint.gameObject.AddComponent<CombatFieldLock>();

                if (Application.isPlaying && !_missingFieldLockWarned)
                {
                    _missingFieldLockWarned = true;
                    Debug.LogWarning("[CombatWorldLifecycleAdapter] No configured CombatFieldLock was found. A runtime local lock was added to the CombatEntryPoint object.", this);
                }
            }

            if (cameraController == null)
                cameraController = FindUnique<CombatCameraController>();
            if (formationManager == null)
                formationManager = FindUnique<CombatFormationManager>();
        }

        private static T FindUnique<T>() where T : Component
        {
            T[] candidates = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return candidates.Length == 1 ? candidates[0] : null;
        }

        private bool TryClaimOwnership()
        {
            if (entryPoint == null)
                return false;

            int key = entryPoint.GetInstanceID();
            if (OwnersByEntryId.TryGetValue(key, out CombatWorldLifecycleAdapter owner))
            {
                if (owner == null)
                {
                    OwnersByEntryId.Remove(key);
                }
                else if (owner != this)
                {
                    if (!_duplicateOwnerWarned)
                    {
                        _duplicateOwnerWarned = true;
                        Debug.LogWarning(
                            $"[CombatWorldLifecycleAdapter] Duplicate owner blocked for entry '{entryPoint.name}'. Active='{owner.name}', Duplicate='{name}'.",
                            this);
                    }

                    _ownsEntryPoint = false;
                    return false;
                }
            }

            OwnersByEntryId[key] = this;
            _ownsEntryPoint = true;
            return true;
        }

        private void ReleaseOwnership()
        {
            if (!_ownsEntryPoint || entryPoint == null)
            {
                _ownsEntryPoint = false;
                return;
            }

            int key = entryPoint.GetInstanceID();
            if (OwnersByEntryId.TryGetValue(key, out CombatWorldLifecycleAdapter owner) && owner == this)
                OwnersByEntryId.Remove(key);
            _ownsEntryPoint = false;
        }

        private void SubscribeEntryPoint()
        {
            if (_subscribedEntryPoint == entryPoint)
                return;

            UnsubscribeEntryPoint();
            _subscribedEntryPoint = entryPoint;
            if (_subscribedEntryPoint == null)
                return;

            _subscribedEntryPoint.OnCombatStarted += HandleCombatStarted;
            _subscribedEntryPoint.OnCombatEnded += HandleCombatEnded;
        }

        private void UnsubscribeEntryPoint()
        {
            if (_subscribedEntryPoint != null)
            {
                _subscribedEntryPoint.OnCombatStarted -= HandleCombatStarted;
                _subscribedEntryPoint.OnCombatEnded -= HandleCombatEnded;
            }
            _subscribedEntryPoint = null;
        }

        private void SubscribeStateMachine()
        {
            GameStateMachine current = GameStateMachine.Instance;
            if (_subscribedStateMachine == current)
                return;

            UnsubscribeStateMachine();
            _subscribedStateMachine = current;
            if (_subscribedStateMachine != null)
                _subscribedStateMachine.OnStateChanged += HandleGameStateChanged;
        }

        private void UnsubscribeStateMachine()
        {
            if (_subscribedStateMachine != null)
                _subscribedStateMachine.OnStateChanged -= HandleGameStateChanged;
            _subscribedStateMachine = null;
        }

        private void HandleCombatStarted(CombatSession session)
        {
            if (!_ownsEntryPoint || session == null)
                return;

            SubscribeStateMachine();

            if (_context != null)
            {
                if (_context.CompletionId == session.CompletionId)
                    return;

                Debug.LogWarning("[CombatWorldLifecycleAdapter] A second session was rejected while an older field context is still active.", this);
                return;
            }

            _context = FieldContext.Capture(session);
            if (_preparedCompletionId == session.CompletionId &&
                _preparedEncounterOwner is ICombatEncounterRuntimeOwner preparedOwner)
            {
                if (!_context.EncounterOwners.Contains(preparedOwner))
                    _context.EncounterOwners.Add(preparedOwner);

                for (int i = 0; i < _context.Actors.Count; i++)
                {
                    ActorSnapshot actor = _context.Actors[i];
                    if (actor.Side == Side.Enemies && actor.EncounterOwner == null)
                        actor.EncounterOwner = preparedOwner;
                }
            }
            _preparedCompletionId = null;
            _preparedEncounterOwner = null;
            for (int i = 0; i < _context.EncounterOwners.Count; i++)
                _context.EncounterOwners[i].AdoptAcceptedSession(session.CompletionId);

            ApplyWorldLock(_context);
            if (cameraController != null)
            {
                cameraController.EnterCombat(session);
                _cameraEnterCount++;
            }
            formationManager?.ApplyFormation(session);

            LogDebug($"Captured completion={session.CompletionId}, roster={_context.Actors.Count}, encounters={_context.EncounterOwners.Count}.");
        }

        private void ApplyWorldLock(FieldContext context)
        {
            if (context == null || fieldLock == null || fieldLock.IsLocked)
                return;

            fieldLock.LockRuntimeTargets(context.LockBehaviours, context.Bodies, null);
        }

        private void HandleCombatEnded(CombatResult result)
        {
            if (!_ownsEntryPoint || result == null || _context == null)
                return;

            if (!string.IsNullOrWhiteSpace(result.CompletionId) && result.CompletionId != _context.CompletionId)
            {
                if (!_mismatchedResultWarned)
                {
                    _mismatchedResultWarned = true;
                    Debug.LogWarning($"[CombatWorldLifecycleAdapter] Ignored mismatched completion. active={_context.CompletionId}, result={result.CompletionId}.", this);
                }
                return;
            }

            if (_context.OutcomeProcessed)
                return;

            bool ownerAccepted = false;
            for (int i = 0; i < _context.EncounterOwners.Count; i++)
                ownerAccepted |= _context.EncounterOwners[i].TryBeginOutcome(result);

            if (_context.EncounterOwners.Count > 0 && !ownerAccepted)
                return;

            _context.OutcomeProcessed = true;
            _outcomeApplicationCount++;

            int clearedCount = ApplyFieldOutcome(result, _context);
            CompleteEncounterOutcomes(result, _context);

            if (cameraController != null)
                cameraController.HoldResultFrame();

            LogDebug($"Outcome={result.EndReason}, cleared={clearedCount}; field remains locked through Reward.");
        }

        private int ApplyFieldOutcome(CombatResult result, FieldContext context)
        {
            if (!IsVictory(result))
                return 0;

            HashSet<int> defeatedIds = new HashSet<int>(result.DefeatedEnemyIds);
            int cleared = 0;
            foreach (int defeatedId in defeatedIds)
            {
                if (!context.ByCombatantId.TryGetValue(defeatedId, out ActorSnapshot snapshot) ||
                    snapshot.Side != Side.Enemies || snapshot.FieldObject == null ||
                    !context.ClearedObjects.Add(snapshot.FieldObject))
                {
                    continue;
                }

                cleared++;
                if (entryPoint != null && entryPoint.DestroyDefeatedFieldObjects)
                {
                    if (Application.isPlaying)
                        Destroy(snapshot.FieldObject);
                    else
                        DestroyImmediate(snapshot.FieldObject);
                }
                else if (entryPoint == null || entryPoint.DeactivateDefeatedFieldObjects)
                {
                    snapshot.FieldObject.SetActive(false);
                }
            }

            return cleared;
        }

        private void CompleteEncounterOutcomes(CombatResult result, FieldContext context)
        {
            for (int ownerIndex = 0; ownerIndex < context.EncounterOwners.Count; ownerIndex++)
            {
                ICombatEncounterRuntimeOwner owner = context.EncounterOwners[ownerIndex];
                bool hasActiveMembers = false;
                for (int actorIndex = 0; actorIndex < context.Actors.Count; actorIndex++)
                {
                    ActorSnapshot actor = context.Actors[actorIndex];
                    if (actor.Side != Side.Enemies || actor.EncounterOwner != owner || actor.FieldObject == null)
                        continue;

                    if (!context.ClearedObjects.Contains(actor.FieldObject) && actor.Combatant.HP > 0 && actor.FieldObject.activeSelf)
                    {
                        hasActiveMembers = true;
                        break;
                    }
                }

                if (IsVictory(result) && hasActiveMembers && !_forcedVictoryWarned)
                {
                    _forcedVictoryWarned = true;
                    Debug.LogWarning("[CombatWorldLifecycleAdapter] Victory left active encounter enemies. The encounter will rearm instead of being falsely cleared.", this);
                }

                owner.CompleteOutcome(result, hasActiveMembers);
            }
        }

        private void HandleGameStateChanged(GameState previous, GameState next)
        {
            if (next != GameState.Exploration || _context == null || !_context.OutcomeProcessed)
                return;

            RestoreAfterCombat();
        }

        private void RestoreAfterCombat()
        {
            FieldContext context = _context;
            if (context == null || context.Restored)
                return;

            context.Restored = true;
            int restored = 0;
            for (int i = 0; i < context.Actors.Count; i++)
            {
                ActorSnapshot snapshot = context.Actors[i];
                if (snapshot.FieldObject == null || context.ClearedObjects.Contains(snapshot.FieldObject))
                    continue;

                snapshot.RestoreTransformAndActiveState();
                restored++;
            }

            fieldLock?.UnlockExcept(context.ClearedObjects);
            if (cameraController != null)
            {
                cameraController.ExitToExplorationFollow();
                _cameraExitCount++;
            }

            for (int i = 0; i < context.EncounterOwners.Count; i++)
                context.EncounterOwners[i].ObserveExploration();

            _restorationCount++;
            LogDebug($"Restored={restored}, completion={context.CompletionId}.");
            _context = null;
        }

        private void RestoreSafeLockOnly()
        {
            if (_context == null)
                return;

            fieldLock?.UnlockExcept(_context.ClearedObjects);
            cameraController?.ExitToExplorationFollow();
        }

        private static bool IsVictory(CombatResult result)
        {
            return result != null &&
                   (result.EndReason != CombatEndReason.None
                       ? result.EndReason == CombatEndReason.Victory
                       : result.IsWin);
        }

        private void LogDebug(string message)
        {
            if (debugLog)
                Debug.Log($"[CombatWorldLifecycleAdapter] {message}", this);
        }

        private sealed class FieldContext
        {
            internal readonly string CompletionId;
            internal readonly List<ActorSnapshot> Actors = new();
            internal readonly Dictionary<int, ActorSnapshot> ByCombatantId = new();
            internal readonly List<ICombatEncounterRuntimeOwner> EncounterOwners = new();
            internal readonly List<Behaviour> LockBehaviours = new();
            internal readonly List<Rigidbody2D> Bodies = new();
            internal readonly HashSet<GameObject> ClearedObjects = new();
            internal bool OutcomeProcessed;
            internal bool Restored;

            private FieldContext(string completionId)
            {
                CompletionId = completionId;
            }

            internal static FieldContext Capture(CombatSession session)
            {
                FieldContext context = new FieldContext(session.CompletionId);
                CaptureSide(session.Allies, context);
                CaptureSide(session.Enemies, context);
                return context;
            }

            private static void CaptureSide(IReadOnlyList<ICombatant> combatants, FieldContext context)
            {
                for (int i = 0; i < combatants.Count; i++)
                {
                    if (!(combatants[i] is FieldCombatantAdapter adapter) || adapter.FieldObject == null)
                        continue;

                    ActorSnapshot snapshot = new ActorSnapshot(combatants[i], adapter.FieldObject);
                    context.Actors.Add(snapshot);
                    context.ByCombatantId[combatants[i].Id.Value] = snapshot;

                    if (snapshot.Body != null && !context.Bodies.Contains(snapshot.Body))
                        context.Bodies.Add(snapshot.Body);

                    for (int behaviourIndex = 0; behaviourIndex < snapshot.LockBehaviours.Count; behaviourIndex++)
                    {
                        Behaviour behaviour = snapshot.LockBehaviours[behaviourIndex];
                        if (behaviour != null && !context.LockBehaviours.Contains(behaviour))
                            context.LockBehaviours.Add(behaviour);
                    }

                    ICombatEncounterRuntimeOwner owner = snapshot.EncounterOwner;
                    if (owner != null && !context.EncounterOwners.Contains(owner))
                        context.EncounterOwners.Add(owner);
                }
            }
        }

        private sealed class ActorSnapshot
        {
            internal readonly ICombatant Combatant;
            internal readonly GameObject FieldObject;
            internal readonly Side Side;
            internal readonly bool ActiveSelf;
            internal readonly bool ActiveInHierarchy;
            internal readonly Vector3 Position;
            internal readonly Quaternion Rotation;
            internal readonly Vector3 LocalScale;
            internal readonly Transform Parent;
            internal readonly int SiblingIndex;
            internal readonly Rigidbody2D Body;
            internal readonly bool BodySimulated;
            internal readonly Vector2 LinearVelocity;
            internal readonly float AngularVelocity;
            internal readonly Collider2D[] Colliders;
            internal readonly bool[] ColliderEnabledStates;
            internal readonly List<Behaviour> LockBehaviours = new();
            internal ICombatEncounterRuntimeOwner EncounterOwner;

            internal ActorSnapshot(ICombatant combatant, GameObject fieldObject)
            {
                Combatant = combatant;
                FieldObject = fieldObject;
                Side = combatant.Side;
                Transform transform = fieldObject.transform;
                ActiveSelf = fieldObject.activeSelf;
                ActiveInHierarchy = fieldObject.activeInHierarchy;
                Position = transform.position;
                Rotation = transform.rotation;
                LocalScale = transform.localScale;
                Parent = transform.parent;
                SiblingIndex = transform.GetSiblingIndex();

                Body = fieldObject.GetComponent<Rigidbody2D>();
                if (Body != null)
                {
                    BodySimulated = Body.simulated;
                    LinearVelocity = Body.linearVelocity;
                    AngularVelocity = Body.angularVelocity;
                }

                Colliders = fieldObject.GetComponentsInChildren<Collider2D>(true);
                ColliderEnabledStates = new bool[Colliders.Length];
                for (int i = 0; i < Colliders.Length; i++)
                    ColliderEnabledStates[i] = Colliders[i] != null && Colliders[i].enabled;

                AddLockBehaviour(fieldObject.GetComponent<PlayerMotor2D_New>());
                AddLockBehaviour(fieldObject.GetComponent<PlayerInputController>());
                AddLockBehaviour(fieldObject.GetComponent<FieldEnemyPatrolAI2D>());
                AddLockBehaviour(fieldObject.GetComponent<FieldEnemyMotor2D>());
                AddLockBehaviour(fieldObject.GetComponent<OverworldEnemyAI>());
                AddLockBehaviour(fieldObject.GetComponent<OverworldPlayerController>());

                CombatEncounterGroup group = fieldObject.GetComponentInParent<CombatEncounterGroup>();
                if (group != null)
                {
                    EncounterOwner = group;
                }
                else
                {
                    CombatEncounterTrigger2D trigger = fieldObject.GetComponentInParent<CombatEncounterTrigger2D>();
                    EncounterOwner = trigger != null ? trigger.RuntimeOwner : null;
                }
            }

            internal void RestoreTransformAndActiveState()
            {
                if (FieldObject == null)
                    return;

                Transform transform = FieldObject.transform;
                if (transform.parent != Parent)
                    transform.SetParent(Parent, true);

                if (Parent != null)
                    transform.SetSiblingIndex(Mathf.Clamp(SiblingIndex, 0, Parent.childCount - 1));

                transform.position = Position;
                transform.rotation = Rotation;
                transform.localScale = LocalScale;
                if (FieldObject.activeSelf != ActiveSelf)
                    FieldObject.SetActive(ActiveSelf);

                if (Body != null)
                {
                    Body.simulated = BodySimulated;
                    Body.linearVelocity = Vector2.zero;
                    Body.angularVelocity = 0f;
                }

                for (int i = 0; i < Colliders.Length; i++)
                {
                    if (Colliders[i] != null)
                        Colliders[i].enabled = ColliderEnabledStates[i];
                }
            }

            private void AddLockBehaviour(Behaviour behaviour)
            {
                if (behaviour != null && !LockBehaviours.Contains(behaviour))
                    LockBehaviours.Add(behaviour);
            }
        }
    }
}
