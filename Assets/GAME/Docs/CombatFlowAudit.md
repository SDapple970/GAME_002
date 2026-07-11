# Combat Flow Audit

## Entry paths

Production paths converge on `Game.Combat.Core.CombatEntryPoint.StartCombat(CombatStartRequest)`:

1. `CombatEncounterTrigger2D` collision/trigger collects player plus `CombatEncounterGroup`, builds a request, calls entry.
2. `Game.Battle.FieldEnemy` collision or `NotifyHitByPlayer` builds a request, calls entry. It is obsolete and wired only in `Test.unity`.
3. `PlayerFieldAttackController` field hit builds a request from the struck enemy, calls entry.
4. `TutorialBattleStartInteractionEventSO` resolves the entry point and calls it; `TutorialQuestCombatBridge` supplies quest context.
5. `CombatFieldCallDebug` F1/F2/F3 calls `StartCombatFromField`, which creates a request and calls entry.

Alternate paths:

- `CombatTestRunner` directly constructs `CombatSession` in `CombatTest.unity` (Debug only).
- `CombatStartSmokeTest` directly calls `CombatBootstrapper.StartCombat` in `Test.unity` (Debug only).
- `SeamlessBattleManager` writes `GameState.Combat` and activates combat UI without a session (Legacy).
- `BattleTransitionController` writes transition/planning states but never creates combat (Legacy presentation path).
- `CombatEntryPoint` editor F9/F10 hotkeys force completion, not entry.

No runtime scene startup or UI button was found directly constructing a production session. `CombatEntryPoint` remains the intended and actual production entry.

## Start-to-finish trace

1. Entry validates there is no active session and global state is Exploration (or state service missing).
2. Entry normalizes inspiration and creates a new request, but `AddFirstFieldObject` copies only one ally and one enemy. This is a correctness risk for group combat.
3. `FieldCombatantFactory` adapts each retained GameObject. `HpAccessor` finds `CombatHpComponent` or compatible damage/HP components; `FieldCombatantAdapter` exposes `ICombatant`; loadout and keyword components supply skills/keywords.
4. `CombatBootstrapper.StartCombat` creates `InspirationPool`, `CombatEnvironment`, `CombatSession`, factory combatants, applies opening effects, constructs `CombatStateMachine`, and ticks once.
5. `CombatStateMachine` moves `EnterCombat -> Planning`, creates the turn, and raises planning. Entry binds `CombatFlowOrchestrator`, subscribes `CombatDirector.PlayResolution`, writes `GameState.CombatPlanning`, and raises `OnCombatStarted`.
6. `CombatPlanningHUD` binds the session, chooses an ally, creates skill and target buttons, creates a `CombatPlanDraft`, and calls `CombatFlowOrchestrator.SubmitPlayerDraftAndAdvance`.
7. Orchestrator validates the draft (`CombatPlanValidator`), applies player plans, asks `CombatTurnResolver` to plan enemy actions, and calls `CombatEntryPoint.SubmitCurrentTurn`.
8. Entry calls `CombatTurnResolver.ResolveTurn` before `CombatStateMachine.ConfirmPlanning`. `CombatTimeline` orders actions by planned speed/tie rules. `SkillRunner` calculates outcomes, mutates HP/inspiration/knowledge/stagger, and appends playbook/result events.
9. State machine changes Planning -> Resolving and invokes `OnRequireResolutionPlay`. `CombatDirector` presents the already-calculated playbook via movement/animation/effects and calls the completion callback. Formation, camera, animation driver, log, inspiration HUD, and widgets are presentation subscribers.
10. State machine evaluates end with `CombatEndEvaluator`. If neither side is defeated it increments turn and returns to Planning; otherwise it sets ExitCombat/end reason.
11. Entry detects ExitCombat in Update, unsubscribes director, applies field outcome, builds `CombatResult`, raises `OnCombatEnded`, and clears active session/state machine.
12. `CombatRewardUIBinder` grants through `RewardService`, registers one DemoMission enemy defeat on victory, changes global state to Reward via `GameFlowController`, and shows `RewardUIPanel`.
13. Closing the panel invokes `OnClosed`; binder returns state to Exploration. Competing `CombatDemoFlowController` can instead use UIOnly, show the same panel, unlock field/camera, and restore Exploration.

## Calculation versus presentation

Calculation/model: `CombatBootstrapper`, `CombatStateMachine`, `CombatFlowOrchestrator`, `CombatPlanValidator`, `CombatTurnResolver`, `CombatTimeline`, `SkillRunner`, `OpeningEffectApplier`, `StaggerSystem`, `CombatEndEvaluator`, `CombatResultBuilder`, and all `Runtime/Model` types.

Presentation/field: `CombatDirector`, `CombatantAnimationDriver`, `CombatFormationManager`, `EncounterAdvantageApplier`, `CombatCameraController`, `CombatPlanningHUD`, `CombatLogHUD`, `CombatInspirationHUD`, `CombatWidgetManager`, `CombatantWidget`, `CombatUIRootController`, `CombatRewardUIBinder`, `RewardUIPanel`, and `CombatFieldLock`.

`CombatEntryPoint` and `CombatStateMachine` are orchestration boundaries, not pure calculation. Entry also applies world cleanup (`SetActive(false)` or destroy) and therefore owns field restoration side effects today.

## Defects and ownership conflicts

- Global `GameState.CombatResolving` is never written by the active combat code. Global state remains CombatPlanning while the local phase is Resolving.
- Entry retains only the first member of each request list. `CombatEncounterGroup` therefore over-promises multi-enemy support.
- `CombatStateSyncer` duplicates entry's CombatPlanning write.
- `CombatDemoFlowController`, `CombatUIRootController`, `CombatRewardUIBinder`, and `UIScreenRouter` compete over canvas/reward/state restoration.
- Entry clears its active session immediately after `OnCombatEnded`; listeners must use the event result/session data and cannot query it afterward.
- Defeat follows the same reward route unless UI configuration distinguishes it. World/player restoration behavior after defeat is not centralized.
- DemoMission kill tracking increments once per victorious combat, not once per defeated combatant.
- Direct HP mutation is complete before presentation. This separation is good, but consumers must not infer animation-time HP as calculation time.

## Recommended direction

Keep `CombatEntryPoint` as the single production boundary. Make it accept the full validated roster, expose phase changes to one game-flow owner, and publish one completion result. Keep resolver/model code free of scene/UI concerns. Choose one reward/field restoration coordinator; retire demo and state-sync duplicates only after Inspector migration.

