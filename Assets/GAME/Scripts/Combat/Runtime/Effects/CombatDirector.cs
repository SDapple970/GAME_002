using System;
using System.Collections;
using Game.Combat.Actions;
using UnityEngine;
using Game.Combat.Adapters;
using Game.Combat.Animation;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Integration;
using Game.Combat.Model;

namespace Game.Combat.Effects
{
    public sealed class CombatDirector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatEntryPoint entryPoint;
        [SerializeField] private CombatCameraController cameraController;

        [Header("Fallback Animation Settings")]
        [SerializeField] private float fallbackApproachDuration = 0.2f;
        [SerializeField] private float fallbackActionDelay = 0.15f;

        private Coroutine _activeResolutionRoutine;
        private CombatSession _activeResolutionSession;
        private CombatTurn _activeResolutionTurn;
        private CombatTurn _lastCompletedTurn;
        private Action _activeCompletion;
        private bool _completionRaised;
        private Coroutine _activeApproachRoutine;
        private CombatAttackDeclaration _activeApproachDeclaration;
        private Action _activeApproachCompletion;
        private CombatFlowOrchestrator _finalOrchestrator;
        private CombatSession _finalSession;
        private CombatApproachPresentationRequest _activeFinalApproachRequest;
        private Coroutine _activeFinalOutcomeRoutine;
        private CombatOutcomePresentationRequest _activeFinalOutcomeRequest;
        private int _finalBindingId;

        public event Action<CombatFinalPresentationCue> FinalPresentationCueRaised;

        public bool BindFinalExchange(
            CombatFlowOrchestrator orchestrator,
            CombatSession session)
        {
            if (ReferenceEquals(_finalOrchestrator, orchestrator) &&
                ReferenceEquals(_finalSession, session))
            {
                return true;
            }

            UnbindFinalExchange();
            if (orchestrator == null || session == null ||
                session.FlowMode != CombatFlowMode.StandoffClashChain)
            {
                return false;
            }

            _finalOrchestrator = orchestrator;
            _finalSession = session;
            _finalBindingId++;
            _finalOrchestrator.ApproachPresentationRequested += HandleFinalApproachRequested;
            _finalOrchestrator.OutcomePresentationRequested += HandleFinalOutcomeRequested;
            _finalOrchestrator.AttackDecisionRequested += HandleFinalAttackDecisionRequested;

            if (_finalOrchestrator.PendingApproachRequest != null)
                HandleFinalApproachRequested(_finalOrchestrator.PendingApproachRequest);
            else if (_finalOrchestrator.PendingOutcomePresentationRequest != null)
                HandleFinalOutcomeRequested(_finalOrchestrator.PendingOutcomePresentationRequest);
            else if (_finalOrchestrator.PendingDecisionRequest != null)
                HandleFinalAttackDecisionRequested(_finalOrchestrator.PendingDecisionRequest);

            return true;
        }

        public void UnbindFinalExchange()
        {
            if (_finalOrchestrator != null)
            {
                _finalOrchestrator.ApproachPresentationRequested -= HandleFinalApproachRequested;
                _finalOrchestrator.OutcomePresentationRequested -= HandleFinalOutcomeRequested;
                _finalOrchestrator.AttackDecisionRequested -= HandleFinalAttackDecisionRequested;
            }

            _finalBindingId++;
            if (_activeFinalOutcomeRoutine != null)
                StopCoroutine(_activeFinalOutcomeRoutine);

            _activeFinalOutcomeRoutine = null;
            _activeFinalOutcomeRequest = null;
            if (_activeFinalApproachRequest != null)
                CancelActiveApproachWithoutCompletion();
            _activeFinalApproachRequest = null;
            _finalOrchestrator = null;
            _finalSession = null;
        }

        public void PlayApproach(CombatAttackDeclaration declaration, Action onComplete)
        {
            if (declaration == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (ReferenceEquals(_activeApproachDeclaration, declaration))
                return;

            CancelActiveApproachWithoutCompletion();
            if (!isActiveAndEnabled)
            {
                onComplete?.Invoke();
                return;
            }

            _activeApproachDeclaration = declaration;
            _activeApproachCompletion = onComplete;
            Coroutine routine = StartCoroutine(Co_PlayApproach(declaration));
            if (_activeApproachDeclaration != null)
                _activeApproachRoutine = routine;
        }

        public void PlayResolution(CombatSession session, Action onComplete)
        {
            CombatTurn turn = session?.CurrentTurn;
            if (turn == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (ReferenceEquals(_lastCompletedTurn, turn))
                return;

            if (ReferenceEquals(_activeResolutionSession, session) && ReferenceEquals(_activeResolutionTurn, turn))
                return;

            CancelActiveResolutionWithoutCompletion();

            if (!isActiveAndEnabled)
            {
                onComplete?.Invoke();
                return;
            }

            _activeResolutionSession = session;
            _activeResolutionTurn = turn;
            _activeCompletion = onComplete;
            _completionRaised = false;

            Coroutine routine = StartCoroutine(Co_PlayTurnAnimation(turn));
            if (_activeResolutionTurn != null)
                _activeResolutionRoutine = routine;
        }

        private void Awake()
        {
            if (entryPoint == null)
                entryPoint = FindFirstObjectByType<CombatEntryPoint>();

            if (cameraController == null)
                cameraController = FindFirstObjectByType<CombatCameraController>();
        }

        private void OnDisable()
        {
            UnbindFinalExchange();

            if (_activeResolutionRoutine != null)
                StopCoroutine(_activeResolutionRoutine);

            CompleteActiveResolution();

            if (_activeApproachRoutine != null)
                StopCoroutine(_activeApproachRoutine);

            CompleteActiveApproach();
        }

        private void HandleFinalApproachRequested(CombatApproachPresentationRequest request)
        {
            CombatAttackDeclaration declaration = request?.Declaration;
            if (!IsCurrentFinalRequest(request?.ExchangeVersion ?? -1) ||
                declaration?.Attacker == null || declaration.Target == null || declaration.Skill == null ||
                !IsFinalSessionMember(declaration.Attacker) || !IsFinalSessionMember(declaration.Target))
            {
                return;
            }

            _activeFinalApproachRequest = request;
            int bindingId = _finalBindingId;
            RaiseFinalCue(
                CombatFinalPresentationCueKind.Approach,
                declaration.Attacker,
                declaration.Target,
                declaration.Skill);
            PlayApproach(
                declaration,
                () => CompleteFinalApproach(bindingId, request));
        }

        private void CompleteFinalApproach(
            int bindingId,
            CombatApproachPresentationRequest request)
        {
            if (bindingId != _finalBindingId ||
                !ReferenceEquals(_activeFinalApproachRequest, request) ||
                !IsCurrentFinalRequest(request?.ExchangeVersion ?? -1))
            {
                return;
            }

            _activeFinalApproachRequest = null;
            request.TryComplete();
        }

        private void HandleFinalOutcomeRequested(CombatOutcomePresentationRequest request)
        {
            if (!IsCurrentFinalRequest(request?.ExchangeVersion ?? -1) ||
                ReferenceEquals(_activeFinalOutcomeRequest, request))
            {
                return;
            }

            if (_activeFinalOutcomeRoutine != null)
                StopCoroutine(_activeFinalOutcomeRoutine);

            _activeFinalOutcomeRequest = request;
            if (!isActiveAndEnabled)
            {
                CompleteFinalOutcome(_finalBindingId, request);
                return;
            }

            int bindingId = _finalBindingId;
            Coroutine routine = StartCoroutine(Co_PlayFinalOutcome(bindingId, request));
            if (_activeFinalOutcomeRequest != null)
                _activeFinalOutcomeRoutine = routine;
        }

        private IEnumerator Co_PlayFinalOutcome(
            int bindingId,
            CombatOutcomePresentationRequest request)
        {
            if (!IsActiveFinalOutcome(bindingId, request))
                yield break;

            CombatOutcomeAction action = request.WinningAction;
            ICombatant actor = action?.Actor;
            ISkill skill = action?.Skill;
            ICombatant primaryTarget = GetPrimaryFinalTarget(request);
            GameObject actorObject = GetFieldObject(actor);

            if (request.HasClash)
            {
                CombatAttackDeclaration attack = request.AttackDeclaration;
                CombatResponseDeclaration response = request.ResponseDeclaration;
                PlayFinalClashAction(
                    CombatFinalPresentationCueKind.AttackAction,
                    attack?.Attacker,
                    response?.Responder ?? attack?.Target,
                    attack?.Skill);
                PlayFinalClashAction(
                    CombatFinalPresentationCueKind.ResponseAction,
                    response?.Responder,
                    attack?.Attacker,
                    response?.Skill);
                RaiseFinalCue(
                    CombatFinalPresentationCueKind.Clash,
                    attack?.Attacker,
                    response?.Responder,
                    skill);
                cameraController?.FocusAction(
                    attack?.Attacker,
                    response?.Responder);
            }

            if (actor != null && skill != null)
            {
                cameraController?.FocusAction(actor, primaryTarget);
                RaiseFinalCue(
                    CombatFinalPresentationCueKind.WinningSkill,
                    actor,
                    primaryTarget,
                    skill);
                if (!request.HasClash && actorObject != null)
                {
                    PlayCastPresentation(actorObject, skill);
                    PlayAttackTrigger(actorObject, skill);
                }

                yield return WaitAfterMove(skill);
            }

            if (!IsActiveFinalOutcome(bindingId, request))
                yield break;

            PlayFinalTargetResults(request, skill);
            cameraController?.HoldResultFrame();
            RaiseFinalCue(
                CombatFinalPresentationCueKind.OutcomeHold,
                actor,
                primaryTarget,
                skill);

            // Presentation placeholder timing — Content Tuning Pending.
            yield return new WaitForSeconds(Mathf.Max(0f, fallbackActionDelay));
            CompleteFinalOutcome(bindingId, request);
        }

        private void PlayFinalClashAction(
            CombatFinalPresentationCueKind cueKind,
            ICombatant actor,
            ICombatant target,
            ISkill skill)
        {
            if (actor == null || skill == null)
                return;

            cameraController?.FocusAction(actor, target);
            RaiseFinalCue(cueKind, actor, target, skill);
            GameObject actorObject = GetFieldObject(actor);
            if (actorObject == null)
                return;

            PlayCastPresentation(actorObject, skill);
            PlayAttackTrigger(actorObject, skill);
        }

        private void PlayFinalTargetResults(
            CombatOutcomePresentationRequest request,
            ISkill skill)
        {
            if (request?.ExecutionResult?.TargetResults != null)
            {
                for (int i = 0; i < request.ExecutionResult.TargetResults.Count; i++)
                {
                    CombatSkillTargetResult result = request.ExecutionResult.TargetResults[i];
                    if (result?.Target == null)
                        continue;

                    GameObject targetObject = GetFieldObject(result.Target);
                    if (targetObject != null)
                    {
                        PlayImpactPresentation(targetObject, skill);
                        PlayFinalTargetReaction(request, result, targetObject);
                    }

                    RaiseFinalResultCues(request, result, skill);
                }
            }
        }

        private static void PlayFinalTargetReaction(
            CombatOutcomePresentationRequest request,
            CombatSkillTargetResult result,
            GameObject targetObject)
        {
            CombatantAnimationDriver driver =
                targetObject != null ? targetObject.GetComponentInChildren<CombatantAnimationDriver>() : null;
            if (driver == null || result?.Target == null)
                return;

            if (result.HpAfter <= 0)
                driver.PlayDie();
            else if (request?.StunResult?.Target == result.Target &&
                     request.StunResult.StunApplied)
                driver.PlayStagger();
            else if (request?.PostureResult?.Target == result.Target &&
                     request.PostureResult.PostureApplied > 0)
                driver.PlayStagger();
            else if (result.DamageApplied > 0)
                driver.PlayHit();
        }

        private void RaiseFinalResultCues(
            CombatOutcomePresentationRequest request,
            CombatSkillTargetResult result,
            ISkill skill)
        {
            if (result.HpAfter <= 0)
            {
                RaiseFinalCue(CombatFinalPresentationCueKind.Defeat, request.Winner, result.Target, skill);
                return;
            }

            if (result.DamageApplied > 0)
                RaiseFinalCue(CombatFinalPresentationCueKind.HitReaction, request.Winner, result.Target, skill);

            if (request.PostureResult?.Target == result.Target &&
                request.PostureResult.PostureApplied > 0)
            {
                RaiseFinalCue(CombatFinalPresentationCueKind.PostureReaction, request.Winner, result.Target, skill);
            }

            if (request.StunResult?.Target == result.Target && request.StunResult.StunApplied)
                RaiseFinalCue(CombatFinalPresentationCueKind.StunReaction, request.Winner, result.Target, skill);
        }

        private void CompleteFinalOutcome(
            int bindingId,
            CombatOutcomePresentationRequest request)
        {
            if (!IsActiveFinalOutcome(bindingId, request))
                return;

            _activeFinalOutcomeRoutine = null;
            _activeFinalOutcomeRequest = null;
            request.TryComplete();
        }

        private void HandleFinalAttackDecisionRequested(CombatExchangeDecisionRequest request)
        {
            if (!IsCurrentFinalRequest(request?.ExchangeVersion ?? -1) ||
                request.Phase != Phase.Standoff)
            {
                return;
            }

            cameraController?.FocusPlanning();
            RaiseFinalCue(
                CombatFinalPresentationCueKind.Standoff,
                request.ActingActor,
                null,
                null);
        }

        private bool IsActiveFinalOutcome(
            int bindingId,
            CombatOutcomePresentationRequest request)
        {
            return bindingId == _finalBindingId &&
                   ReferenceEquals(_activeFinalOutcomeRequest, request) &&
                   IsCurrentFinalRequest(request?.ExchangeVersion ?? -1);
        }

        private bool IsCurrentFinalRequest(int exchangeVersion)
        {
            return _finalOrchestrator != null && _finalSession != null &&
                   _finalSession.FlowMode == CombatFlowMode.StandoffClashChain &&
                   _finalSession.ExchangeState != null && exchangeVersion >= 0 &&
                   _finalSession.ExchangeState.Version == exchangeVersion;
        }

        private bool IsFinalSessionMember(ICombatant combatant)
        {
            if (_finalSession == null || combatant == null)
                return false;

            System.Collections.Generic.IReadOnlyList<ICombatant> roster =
                _finalSession.GetSide(combatant.Side);
            for (int i = 0; i < roster.Count; i++)
            {
                if (ReferenceEquals(roster[i], combatant))
                    return true;
            }

            return false;
        }

        private static ICombatant GetPrimaryFinalTarget(
            CombatOutcomePresentationRequest request)
        {
            if (request?.ExecutionResult?.TargetResults != null &&
                request.ExecutionResult.TargetResults.Count > 0)
            {
                return request.ExecutionResult.TargetResults[0]?.Target;
            }

            return request?.Loser ?? request?.AttackDeclaration?.Target;
        }

        private void RaiseFinalCue(
            CombatFinalPresentationCueKind kind,
            ICombatant actor,
            ICombatant target,
            ISkill skill)
        {
            Action<CombatFinalPresentationCue> handlers = FinalPresentationCueRaised;
            if (handlers == null)
                return;

            CombatFinalPresentationCue cue =
                new CombatFinalPresentationCue(kind, actor, target, skill);
            Delegate[] invocationList = handlers.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                try
                {
                    ((Action<CombatFinalPresentationCue>)invocationList[i]).Invoke(cue);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        private IEnumerator Co_PlayApproach(CombatAttackDeclaration declaration)
        {
            GameObject attackerObject = GetFieldObject(declaration.Attacker);
            GameObject targetObject = GetFieldObject(declaration.Target);
            if (attackerObject != null && targetObject != null)
            {
                cameraController?.FocusAction(declaration.Attacker, declaration.Target);
                yield return MoveActorForSkill(
                    attackerObject.transform,
                    targetObject.transform,
                    declaration.Skill);
            }

            CompleteActiveApproach();
        }

        private void CompleteActiveApproach()
        {
            if (_activeApproachDeclaration == null)
                return;

            Action completion = _activeApproachCompletion;
            _activeApproachRoutine = null;
            _activeApproachDeclaration = null;
            _activeApproachCompletion = null;
            completion?.Invoke();
        }

        private void CancelActiveApproachWithoutCompletion()
        {
            if (_activeApproachRoutine != null)
                StopCoroutine(_activeApproachRoutine);

            _activeApproachRoutine = null;
            _activeApproachDeclaration = null;
            _activeApproachCompletion = null;
        }

        private IEnumerator Co_PlayTurnAnimation(CombatTurn turn)
        {
            if (turn == null || turn.Playbook.Count == 0)
            {
                CompleteActiveResolution();
                yield break;
            }

            for (int i = 0; i < turn.Playbook.Count; i++)
            {
                if (!ReferenceEquals(_activeResolutionTurn, turn))
                    yield break;

                PlaybookEvent playbookEvent = turn.Playbook[i];

                if (playbookEvent is Event_Unopposed unopposed)
                    yield return PlayUnopposed(unopposed);
                else if (playbookEvent is Event_Clash clash)
                    yield return PlayClash(clash);
                else if (playbookEvent is Event_Utility utility)
                    yield return PlayUtility(utility);
                else if (playbookEvent is Event_Area area)
                    yield return PlayArea(area);

                yield return new WaitForSeconds(0.2f);
            }

            CompleteActiveResolution();
        }

        private IEnumerator PlayUnopposed(Event_Unopposed ev)
        {
            if (ev == null)
                yield break;

            if (ev.IsCancelled || ev.LackOfInspiration)
            {
                yield return new WaitForSeconds(0.3f);
                yield break;
            }

            GameObject actorObj = GetFieldObject(ev.Actor);
            GameObject targetObj = GetFieldObject(ev.Target);

            if (actorObj == null)
                yield break;

            Debug.Log($"[CombatDirector] Actor={ev.Actor?.Id.Value} Skill={ev.Skill?.Name} Target={ev.Target?.Id.Value}", this);

            if (targetObj != null)
            {
                cameraController?.FocusAction(ev.Actor, ev.Target);
                yield return MoveActorForSkill(actorObj.transform, targetObj.transform, ev.Skill);
            }

            PlayCastPresentation(actorObj, ev.Skill);
            PlayAttackTrigger(actorObj, ev.Skill);
            yield return WaitAfterMove(ev.Skill);

            if (targetObj != null)
            {
                PlayImpactPresentation(targetObj, ev.Skill);
                PlayTargetReaction(ev.Target, targetObj, ev.DamageDealt, ev.StaggerDealt);
                StartCoroutine(FlashColor(targetObj, Color.red, 0.2f));
            }

            yield return new WaitForSeconds(0.3f);

        }

        private IEnumerator PlayClash(Event_Clash ev)
        {
            if (ev == null)
                yield break;

            if (ev.IsCancelled || ev.LackOfInspiration)
            {
                yield return new WaitForSeconds(0.3f);
                yield break;
            }

            GameObject objA = GetFieldObject(ev.ActorA);
            GameObject objB = GetFieldObject(ev.ActorB);

            if (objA == null || objB == null)
                yield break;

            Debug.Log(
                $"[CombatDirector] Clash A={ev.ActorA?.Id.Value}:{ev.SkillA?.Name} B={ev.ActorB?.Id.Value}:{ev.SkillB?.Name}",
                this
            );

            cameraController?.FocusAction(ev.ActorA, ev.ActorB);

            yield return MoveActorForSkill(objA.transform, objB.transform, ev.SkillA);
            yield return MoveActorForSkill(objB.transform, objA.transform, ev.SkillB);

            PlayCastPresentation(objA, ev.SkillA);
            PlayAttackTrigger(objA, ev.SkillA);
            PlayCastPresentation(objB, ev.SkillB);
            PlayAttackTrigger(objB, ev.SkillB);

            float delay = Mathf.Max(GetActionDelay(ev.SkillA), GetActionDelay(ev.SkillB));
            yield return new WaitForSeconds(delay);

            if (ev.Loser != null)
            {
                GameObject loserObj = GetFieldObject(ev.Loser);
                if (loserObj != null)
                {
                    PlayImpactPresentation(loserObj, GetWinnerSkill(ev));
                    PlayTargetReaction(ev.Loser, loserObj, ev.DamageDealtToLoser, ev.StaggerDealtToLoser);
                    StartCoroutine(FlashColor(loserObj, Color.red, 0.2f));
                }
            }

            yield return new WaitForSeconds(0.4f);

        }

        private IEnumerator PlayUtility(Event_Utility ev)
        {
            if (ev == null || ev.IsCancelled)
                yield break;

            GameObject actorObj = GetFieldObject(ev.Actor);
            if (actorObj == null)
                yield break;

            Debug.Log($"[CombatDirector] Utility Actor={ev.Actor?.Id.Value} Skill={ev.Skill?.Name}", this);

            cameraController?.FocusAction(ev.Actor, ev.Actor);
            PlayCastPresentation(actorObj, ev.Skill);
            PlayAttackTrigger(actorObj, ev.Skill);
            PlayImpactPresentation(actorObj, ev.Skill);
            StartCoroutine(FlashColor(actorObj, Color.yellow, 0.3f));
            yield return WaitAfterMove(ev.Skill);
        }

        private IEnumerator PlayArea(Event_Area ev)
        {
            if (ev == null || ev.IsCancelled || ev.LackOfInspiration)
                yield break;

            GameObject actorObj = GetFieldObject(ev.Actor);
            if (actorObj == null)
                yield break;

            ICombatant firstTarget = ev.Targets.Count > 0 ? ev.Targets[0] : null;
            GameObject firstTargetObj = GetFieldObject(firstTarget);
            if (firstTargetObj != null)
            {
                cameraController?.FocusAction(ev.Actor, firstTarget);
                yield return MoveActorForSkill(actorObj.transform, firstTargetObj.transform, ev.Skill);
            }

            PlayCastPresentation(actorObj, ev.Skill);
            PlayAttackTrigger(actorObj, ev.Skill);
            yield return WaitAfterMove(ev.Skill);

            for (int i = 0; i < ev.Targets.Count; i++)
            {
                ICombatant target = ev.Targets[i];
                GameObject targetObj = GetFieldObject(target);
                if (targetObj == null)
                    continue;

                int damage = i < ev.DamageDealt.Count ? ev.DamageDealt[i] : 0;
                int stagger = i < ev.StaggerDealt.Count ? ev.StaggerDealt[i] : 0;
                PlayImpactPresentation(targetObj, ev.Skill);
                PlayTargetReaction(target, targetObj, damage, stagger);
                StartCoroutine(FlashColor(targetObj, Color.red, 0.2f));
            }

            yield return new WaitForSeconds(0.3f);
        }

        private void CompleteActiveResolution()
        {
            if (_activeResolutionTurn == null || _completionRaised)
                return;

            _completionRaised = true;
            Action completion = _activeCompletion;
            _lastCompletedTurn = _activeResolutionTurn;
            _activeResolutionRoutine = null;
            _activeResolutionSession = null;
            _activeResolutionTurn = null;
            _activeCompletion = null;
            completion?.Invoke();
        }

        private void CancelActiveResolutionWithoutCompletion()
        {
            if (_activeResolutionRoutine != null)
                StopCoroutine(_activeResolutionRoutine);

            _activeResolutionRoutine = null;
            _activeResolutionSession = null;
            _activeResolutionTurn = null;
            _activeCompletion = null;
            _completionRaised = false;
        }

        private IEnumerator MoveActorForSkill(Transform actor, Transform target, ISkill skill)
        {
            if (actor == null || target == null || !ShouldApproach(skill))
                yield break;

            Vector3 attackPoint = CalculateAttackPoint(actor.position, target.position, skill);
            attackPoint.z = actor.position.z;

            cameraController?.FocusAction(GetCombatant(actor.gameObject), GetCombatant(target.gameObject));
            yield return MoveTransform(actor, actor.position, attackPoint, GetMoveDuration(actor.position, attackPoint, skill));
            cameraController?.FocusAction(GetCombatant(actor.gameObject), GetCombatant(target.gameObject));
        }

        private static bool ShouldApproach(ISkill skill)
        {
            if (skill == null)
                return false;

            return skill.MovementMode == SkillMovementMode.ApproachAndStay;
        }

        private static Vector3 CalculateAttackPoint(Vector3 actorPosition, Vector3 targetPosition, ISkill skill)
        {
            Vector3 direction = actorPosition - targetPosition;
            direction.z = 0f;

            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.left;
            else
                direction.Normalize();

            float distance = skill != null ? Mathf.Max(0f, skill.DesiredTargetDistance) : 1f;
            return targetPosition + direction * distance;
        }

        private float GetMoveDuration(Vector3 start, Vector3 end, ISkill skill)
        {
            float distance = Vector3.Distance(start, end);
            if (distance <= 0.001f)
                return 0f;

            float speed = skill != null ? skill.MoveSpeed : 0f;
            if (speed > 0.001f)
                return Mathf.Max(0.01f, distance / speed);

            return Mathf.Max(0.01f, fallbackApproachDuration);
        }

        private float GetActionDelay(ISkill skill)
        {
            return skill != null ? Mathf.Max(0f, skill.ActionDelayAfterMove) : fallbackActionDelay;
        }

        private IEnumerator WaitAfterMove(ISkill skill)
        {
            yield return new WaitForSeconds(GetActionDelay(skill));
        }

        private static void PlayAttackTrigger(GameObject actorObj, ISkill skill)
        {
            if (actorObj == null)
                return;

            CombatantAnimationDriver driver = actorObj.GetComponentInChildren<CombatantAnimationDriver>();
            SkillDefinitionSO skillDefinition = ResolveSkillDefinition(skill);
            if (driver != null)
            {
                driver.PlaySkill(skillDefinition != null ? skillDefinition.CombatAnimationTrigger : null);
                return;
            }

            Animator anim = actorObj.GetComponentInChildren<Animator>();
            if (anim != null)
                anim.SetTrigger(Animator.StringToHash("Attack"));
        }

        private void PlayCastPresentation(GameObject actorObj, ISkill skill)
        {
            if (actorObj == null)
                return;

            SkillDefinitionSO skillDefinition = ResolveSkillDefinition(skill);
            if (skillDefinition == null)
                return;

            Vector3 position = actorObj.transform.position;
            SpawnPresentationVfx(skillDefinition.CastVfxPrefab, position);
            PlayPresentationSfx(skillDefinition.CastSfx, position);
        }

        private void PlayImpactPresentation(GameObject targetObj, ISkill skill)
        {
            if (targetObj == null)
                return;

            SkillDefinitionSO skillDefinition = ResolveSkillDefinition(skill);
            if (skillDefinition == null)
                return;

            Vector3 position = targetObj.transform.position;
            SpawnPresentationVfx(skillDefinition.ImpactVfxPrefab, position);
            PlayPresentationSfx(skillDefinition.ImpactSfx, position);
        }

        private static void SpawnPresentationVfx(GameObject prefab, Vector3 position)
        {
            if (prefab == null)
                return;

            GameObject instance = Instantiate(prefab, position, Quaternion.identity);
            Destroy(instance, GetPresentationVfxLifetime(instance));
        }

        private static void PlayPresentationSfx(AudioClip clip, Vector3 position)
        {
            if (clip == null)
                return;

            AudioSource.PlayClipAtPoint(clip, position);
        }

        private static float GetPresentationVfxLifetime(GameObject instance)
        {
            const float fallbackLifetime = 2f;

            if (instance == null)
                return fallbackLifetime;

            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            if (particleSystems == null || particleSystems.Length == 0)
                return fallbackLifetime;

            float lifetime = 0f;
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                ParticleSystem.MainModule main = particleSystem.main;
                float systemLifetime = main.duration + main.startLifetime.constantMax;
                lifetime = Mathf.Max(lifetime, systemLifetime);
            }

            return lifetime > 0f ? lifetime : fallbackLifetime;
        }

        private static void PlayTargetReaction(ICombatant target, GameObject targetObj, int damageDealt, int staggerDealt)
        {
            if (targetObj == null || target == null)
                return;

            if (damageDealt <= 0 && staggerDealt <= 0)
                return;

            CombatantAnimationDriver driver = targetObj.GetComponentInChildren<CombatantAnimationDriver>();
            if (driver == null)
                return;

            if (target.HP <= 0)
                driver.PlayDie();
            else if (staggerDealt > 0 && target.Stagger >= target.StaggerMax)
                driver.PlayStagger();
            else
                driver.PlayHit();
        }

        private static SkillDefinitionSO ResolveSkillDefinition(ISkill skill)
        {
            return skill is SoSkill soSkill ? soSkill.Definition : null;
        }

        private static ISkill GetWinnerSkill(Event_Clash ev)
        {
            if (ev == null || ev.Winner == null)
                return null;

            if (ev.Winner == ev.ActorA)
                return ev.SkillA;

            if (ev.Winner == ev.ActorB)
                return ev.SkillB;

            return null;
        }

        private GameObject GetFieldObject(ICombatant combatant)
        {
            if (combatant is FieldCombatantAdapter fieldCombatant)
                return fieldCombatant.FieldObject;

            return null;
        }

        private ICombatant GetCombatant(GameObject fieldObject)
        {
            if (fieldObject == null || entryPoint == null || entryPoint.ActiveSession == null)
                return null;

            CombatSession session = entryPoint.ActiveSession;

            for (int i = 0; i < session.Allies.Count; i++)
            {
                if (session.Allies[i] is FieldCombatantAdapter adapter && adapter.FieldObject == fieldObject)
                    return adapter;
            }

            for (int i = 0; i < session.Enemies.Count; i++)
            {
                if (session.Enemies[i] is FieldCombatantAdapter adapter && adapter.FieldObject == fieldObject)
                    return adapter;
            }

            return null;
        }

        private IEnumerator MoveTransform(Transform tf, Vector3 start, Vector3 end, float duration)
        {
            if (tf == null)
                yield break;

            if (duration <= 0.001f)
            {
                tf.position = end;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(t / duration);
                float eased = Mathf.Sin(normalizedTime * Mathf.PI * 0.5f);
                tf.position = Vector3.Lerp(start, end, eased);
                yield return null;
            }

            tf.position = end;
        }

        private IEnumerator FlashColor(GameObject obj, Color flashColor, float duration)
        {
            if (obj == null)
                yield break;

            SpriteRenderer sprite = obj.GetComponentInChildren<SpriteRenderer>();
            if (sprite == null)
                yield break;

            Color original = sprite.color;
            sprite.color = flashColor;
            yield return new WaitForSeconds(duration);
            sprite.color = original;
        }
    }
}
