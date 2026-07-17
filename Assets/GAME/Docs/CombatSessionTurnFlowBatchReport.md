# Runtime Refactor Batch 4 — Combat Session and Turn Flow

## 1. Implementation result

Batch 4 establishes one guarded production path:

`CombatPlanningHUD -> CombatFlowOrchestrator -> CombatEntryPoint.SubmitCurrentTurn -> CombatTurnResolver -> CombatStateMachine Resolution -> CombatDirector -> CombatStateMachine EndTurn -> Planning or ExitCombat`

The orchestrator no longer calls `CombatTurnResolver`. `CombatEntryPoint` performs final complete-plan normalization, submits a turn once, invokes the resolver once, and enters Resolution only after successful calculation. No second resolver, state machine, session model, timeline, or coordinator was added. Batch 5 was not started.

## 2. Loaded instruction files

- Repository-root `AGENTS.md`.
- No nested `AGENTS.md` or `AGENTS.override.md` exists.

Required supporting documents read completely:

- `Assets/GAME/Docs/CurrentRuntimeCodeAudit.md`
- `Assets/GAME/Docs/CombatFlowAudit.md`
- `Assets/GAME/Docs/RuntimeRefactorBatchPlan.md`
- `Assets/GAME/Docs/GameStateOwnershipBatchReport.md`
- `Assets/GAME/Docs/InputOwnershipBatchReport.md`
- `Assets/GAME/Docs/CombatEntryConsolidationBatchReport.md`

Current code, current serialized assets, and compiler output took precedence over older audit statements.

## 3. Initial branch and Git status

- Branch: `main`, tracking `origin/main`.
- Initial worktree: clean.
- No unrelated initial change required preservation.

## 4. Modified files

- `Assets/GAME/Scripts/Combat/Runtime/Actions/SkillRunner.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Adapters/OpeningEffectApplier.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatEntryPoint.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatFlowOrchestrator.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatPlanValidator.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatStateMachine.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatTimeline.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatTurnResolver.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/InspirationPool.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Effects/CombatDirector.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Model/CombatPlaybook.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Model/CombatSession.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Model/CombatTurn.cs`
- `Assets/GAME/Scripts/Debugging/Combat/CombatStartSmokeTest.cs`
- `Assets/GAME/Scripts/Debugging/Combat/CombatTestRunner.cs`

`CombatTurn`, `CombatSession`, `CombatPlanValidator`, `CombatTurnResolver`, `CombatPlaybook`, `OpeningEffectApplier`, `CombatTimeline`, `CombatTestRunner`, and `CombatStartSmokeTest` used legacy CP949 encoding. Files in this list that required patching were mechanically normalized to UTF-8 before scoped code edits. No serialized asset or GUID was involved.

## 5. Added files

- `Assets/GAME/Tests/Editor/CombatSessionTurnFlowTests.cs`
- `Assets/GAME/Tests/Editor/CombatSessionTurnFlowTests.cs.meta`
- `Assets/GAME/Docs/CombatSessionTurnFlowBatchReport.md`
- `Assets/GAME/Docs/CombatSessionTurnFlowBatchReport.md.meta`

## 6. Deleted or moved files

None.

## 7. Production turn-flow owner map

| Responsibility | Production owner |
|---|---|
| UI draft construction and repeat-button guard | `CombatPlanningHUD` |
| Player draft validation/normalization, additional-ally policy, enemy planning | `CombatFlowOrchestrator` + `CombatPlanValidator` |
| Final plan-set validation, submission, one resolver invocation | `CombatEntryPoint.SubmitCurrentTurn` |
| Calculation, ordering, clashes, costs, HP/stagger/knowledge mutation, playbook | `CombatTurnResolver` |
| Local phase, presentation request/completion, EndTurn, end evaluation | `CombatStateMachine` |
| Resolved playbook presentation only | `CombatDirector` |
| Result construction/publication | `CombatEntryPoint` |

## 8. Previous competing submission paths

- `CombatFlowOrchestrator` committed plans, called `CombatTurnResolver.ResolveTurn`, then called `ConfirmPlanningFromUI`.
- `CombatEntryPoint.SubmitCurrentTurn` separately called `ResolveTurn` and `ConfirmPlanning`.
- Public `ConfirmPlanningFromUI` directly entered Resolution without proving calculation occurred.
- `Demo/UI/Canvas/CombatHUD/Panel_Planning/Button_Confirm` has a serialized null-target `ConfirmPlanningFromUI` callback.
- `Test/UI/Canvas_Combat/Panel_Planning/Button_Confirm` has a serialized entry-point `ConfirmPlanningFromUI` callback.

## 9. Final canonical submission path

The HUD creates one draft and calls the orchestrator. The orchestrator verifies its bound session/phase/turn, normalizes the canonical player plan, creates explicit plans for every other actor, and commits one complete snapshot. Entry revalidates every plan, submits the turn lifecycle, resolves once, and asks the state machine to enter Resolution. `ConfirmPlanningFromUI` remains `void` for UnityEvent compatibility and delegates to `SubmitCurrentTurn`.

## 10. CombatTurn lifecycle

The non-serialized runtime lifecycle is:

`Planning -> Submitted -> Resolved -> Presenting -> Presented -> Completed`

Calculation exceptions use `ResolutionFailed`. Plans accept mutation only in Planning. Resolver results accept writes only during the submitted calculation. A turn cannot be resubmitted, recalculated, replayed through the state machine, or completed twice. `ForceExit` completes the active turn and makes ExitCombat final.

## 11. Plan validation policy

- Every plan actor must be the exact active-session combatant for its ID.
- The player draft must contain exactly the canonical first ally and no other actor.
- Living actors may use only skills from their current `Skills` list.
- Single targets must exist in the session, be on the authored absolute side, and be alive.
- Dead and stunned actors normalize to explicit `None` plans.
- Every session actor must have an explicit committed plan before calculation.
- A living actor with usable skills and no committed plan causes direct entry submission to fail.
- Unknown actors, unknown skills, dead targets, wrong sides, and unsupported enum values reject the submission.

## 12. Plan normalization policy

Caller drafts are read but never mutated. Each accepted action is rebuilt from its authoritative `ISkill`: ID, tag, targeting, speed, and `ConsumesTurn`. `Self` replaces the target with the actor. Area/environment/no-target actions clear the caller target ID. Final normalization is repeated at the entry boundary, and the resolver verifies the committed snapshot still exactly matches authoritative metadata.

## 13. Supported targeting rules

All current enum rules are supported:

- `None` and `Environment`: targetless utility execution.
- `Self`: the actor.
- `SingleEnemy`: one living member of the absolute Enemies side.
- `SingleAlly`: one living member of the absolute Allies side. This preserves current Dungeon 1 enemy assets.
- `AnySingle`: any living session member.
- `AllEnemies` and `AllAllies`: one cost and one area calculation across every living member of the authored side, represented by one `Event_Area` and one director action presentation.

## 14. Deferred targeting rules

None among the current `TargetingRule` enum values. Unknown future enum values reject with a diagnostic rather than becoming utility actions.

## 15. Player-controlled ally policy

`session.Allies[0]` remains the single player-controlled ally for the current MVP. The submitted actor must be that exact object. No party-command UI was added.

## 16. Additional-ally policy

A valid plan already supplied for an additional ally is normalized and preserved. Otherwise an explicit two-slot `None` plan is committed. No new ally AI or command UI was added.

## 17. Enemy-planning policy

Each living enemy without a supplied plan is planned once. The planner starts at `TurnIndex % Skills.Count`, searches deterministically for the first skill with a legal target, and uses the first living target in serialized roster order. A supplied valid enemy plan is retained. Dead, stunned, skill-less, or target-less enemies receive explicit `None`.

## 18. Dead and stunned actor policy

Dead or already-stunned actors receive `None` during plan normalization. Resolver rechecks actor and target state immediately before every execution. An actor killed or stunned in Slot 1 has its planned Slot 2 action represented as a cancelled playbook event and cannot mutate combat state.

## 19. Production ordering owner

`CombatTurnResolver` is the sole production ordering and calculation owner.

## 20. Preserved compatibility ordering paths

`CombatTimeline` remains present and compilable, including its public surface, but production submission does not call it. Its static process-global order counter therefore cannot influence production ordering. It was not deleted because Debug/Legacy reference safety is intentionally deferred.

## 21. Speed and tie-break rules

For each turn:

1. complete Slot 1 before collecting Slot 2;
2. higher authoritative skill speed first;
3. equal speed: session initiative side first;
4. equal priority: stable session roster order (Allies list followed by Enemies list);
5. final defensive tie: `CombatantId`.

Clash pair selection walks this same stable order. No process-global counter participates.

## 22. Random-source design

Production uses a private Unity-compatible random source. Tests can access internal fixed and seeded sources through `InternalsVisibleTo("Assembly-CSharp-Editor")`. No MonoBehaviour, singleton, global manager, or serialized provider was added. Each clash power is `BaseDamage + inclusive roll [1, 3]`, preserving the previous range.

## 23. Clash rules

Only mutual single-target actions clash. Each action is assigned at most once, and clash actions do not also execute unopposed. Pair discovery and pair ordering are deterministic. Dead, stunned, or no-longer-mutual participants produce one cancelled `Event_Clash`. Draws spend the accepted costs once and apply no damage. A winner applies damage/stagger once.

## 24. Inspiration spending policy

The existing shared session `InspirationPool` is preserved. For clashes, both non-negative costs are summed and affordability is checked before any spend; the combined amount is then spent once. If either action cannot be funded, neither cost is spent. Unopposed and area actions validate actor/target availability before spending. `None` costs nothing. Shared ownership across both sides remains a design risk for a later combat-rules batch.

## 25. Presentation handshake

After calculation marks the turn Resolved, the state machine enters Resolution and starts presentation once. The director reads the existing playbook, never invokes calculation, and reports completion once. Empty playbooks and missing presenters use the same immediate state-machine fallback. Disabling a director stops its active coroutine and invokes its stored completion once so combat is not left stuck.

## 26. Duplicate callback protection

State-machine presentation start requires `Resolved -> Presenting`. Completion requires the exact active session, `CombatTurn` reference, turn index, Resolution phase, and `Presenting` lifecycle. Duplicate callbacks cannot pass the lifecycle transition. Director tracks its active and last-completed turn and refuses a second coroutine for either.

## 27. Stale callback protection

Callbacks capture the specific session, turn reference, and turn index. A callback from a prior turn, after session replacement, or after `ForceExit` is ignored. ExitCombat sets a final state and completes the active lifecycle so stale completion cannot reopen EndTurn or Planning.

## 28. EndTurn timing

EndTurn runs only after presentation completion and only from `Presented`. It marks the prior turn Completed, evaluates combat end, then—only for a living battle—clears stun once and calls guarded `TryBeginNewTurn`. That increments `TurnIndex`, grants one existing per-turn Inspiration point, creates one fresh turn, and enters Planning once.

`ConsumesTurn` is normalized from the skill but does not collapse the established two configured slots; each slot remains an independent authored reservation in the current MVP.

## 29. End-condition timing

Initial invalid sessions are checked at EnterCombat. Calculated HP remains mutated while the complete final playbook presents. Victory/Defeat/Abort is evaluated after presentation, before stun clear or new-turn creation. It is not evaluated between calculation and final animation.

Semantics remain:

- Victory: all enemies dead and at least one ally alive.
- Defeat: all allies dead and at least one enemy alive.
- Abort: both sides dead or the session is invalid.
- None: both sides have a living member.

## 30. Public API changes

Preserved methods/properties/events include entry active properties/events, `SubmitCurrentTurn`, `ConfirmPlanningFromUI`, orchestrator submission, state-machine events/ForceExit, session turn APIs, HUD/director serialized fields, and `CombatTurn.SetPlan`.

Compatible extensions/changes:

- `CombatTurnLifecycle`, lifecycle property, guarded plan/lifecycle methods, and read-only plan/event/playbook views.
- `CombatTurnResolver.ResolveTurn` now returns `bool`; existing callers may ignore the return.
- `CombatStateMachine.ConfirmPlanning` now returns `bool`; existing callers may ignore the return.
- `CombatSession.TryBeginNewTurn`; existing `BeginNewTurn` remains.
- `InspirationPool.CanSpend`.
- `Event_Area` and cancellation fields on `Event_Clash`.
- Internal fixed/seeded random sources exposed only to the Editor test assembly.

## 31. Inspector impact

Required Inspector changes: none.

The two serialized compatibility button callbacks remain untouched. The Demo callback remains a pre-existing null target; the Test callback now reaches guarded canonical submission through the retained public wrapper.

## 32. Scene/prefab/SO/Input Actions/.meta impact

- Existing `.unity`, `.prefab`, `.asset`, `.inputactions`, generated input code, and existing `.meta`: unchanged.
- Existing script GUIDs: unchanged.
- New test/report assets have new `.meta` files only.
- No Inspector reconnection or missing-script risk was introduced by a rename/move because no asset or type was renamed/moved.

## 33. Unity compilation result

Unity version used: `6000.2.6f2`.

- Final local Unity Test Runner validation completed on 2026-07-17.
- Unity runtime compilation completed with zero C# errors.
- Unity Editor/test assembly compilation completed with zero C# errors.

The original six warnings remained unrelated: obsolete TMP word wrapping; unused Legacy `FieldEnemy.OnBattleRequested`; retained unused input compatibility fields on `StoryInteractionController` and `MissionCompleteCutsceneController`; and unused DemoMission `RescueNpcActor.interactKey`. No warning points to a Batch 4 file.

## 34. CombatSessionTurnFlowTests result and validation correction

Initial manual EditMode run on 2026-07-17:

- 105 total
- 104 passed
- 1 failed
- Failing test: `Game.Tests.Combat.CombatSessionTurnFlowTests.MissingPresenter_UsesImmediateFallbackOnce`

Root cause: the test called `Tick()` twice and then expected `Phase.EndTurn`. The intentional missing-presenter fallback completes presentation during the first Resolution tick and enters EndTurn immediately. The second tick processes EndTurn, completes the previous turn, grants the per-turn Inspiration once, creates the next turn, and enters Planning. The old assertion therefore observed the state machine one tick later than the state it expected; runtime behavior was correct.

Exact test correction: the test now asserts that `ConfirmPlanning()` succeeds, records the initial turn index and Inspiration, expects the missing-presenter warning, and verifies `Presented`/EndTurn after the first tick. It keeps the previous `CombatTurn` reference, then verifies that the second tick marks it `Completed`, increments the turn index exactly once, grants Inspiration exactly once, and creates a new Planning turn. A third tick verifies that Planning, turn index, and Inspiration remain unchanged.

Final rerun result:

- Isolated `MissingPresenter_UsesImmediateFallbackOnce`: 1 passed, 0 failed, 0 skipped.
- `CombatSessionTurnFlowTests`: 44 passed, 0 failed, 0 skipped.
- Complete EditMode suite: 105 passed, 0 failed, 0 skipped.

## 35. CombatEntryConsolidationTests result

35 passed, 0 failed, 0 skipped in the final complete EditMode rerun.

## 36. CombatFoundationTests result

2 passed, 0 failed, 0 skipped in the final complete EditMode rerun.

## 37. GameStateOwnershipTests result

6 passed, 0 failed, 0 skipped in the final complete EditMode rerun.

## 38. InputOwnershipTests result

18 passed, 0 failed, 0 skipped in the final complete EditMode rerun.

## 39. Manual Play Mode result

Not executed. Interactive Dungeon 1 controls, UI, coroutine timing, animation, camera, VFX/SFX, and physical double-click behavior are not claimed as passed.

Required manual procedure:

1. Open `Assets/GAME/Scenes/Dungeon 1.unity`; confirm Exploration then start a normal encounter.
2. Select one skill/target and confirm once; verify one CombatResolving transition, one presentation, and one HP/Inspiration mutation.
3. Double-click Confirm; verify only one accepted submission and no duplicate damage/cost/log/coroutine.
4. Let presentation finish; verify one TurnIndex increment, one Inspiration gain, one fresh HUD build, cleared selection, and CombatPlanning.
5. In an unsaved setup, kill or stun an actor in Slot 1; verify its Slot 2 cancellation presents and does not execute.
6. In an unsaved multi-enemy setup, verify speed, initiative, stable roster ties, one action per slot, and dead-enemy cancellation.
7. Configure mutual actions; verify one clash, no unopposed duplicate, one cost/result, and no partial spend on cancellation.
8. Defeat the final enemy; verify the final action presents before ExitCombat, no new Planning turn appears, and existing Reward flow starts once.
9. Allow final ally defeat; verify presentation completes, no new turn begins, and one combat result is emitted.
10. Temporarily omit/disable the director without saving; verify immediate/disable fallback completes once and does not stick in Resolution.
11. Use Editor F9/F10 during presentation; verify stale completion cannot reopen Planning or emit another result.

## 40. Unexecuted tests

- Dungeon 1 interactive Play Mode.
- Director interruption with live coroutines and field objects.
- Visual area-action, formation, widget, camera, VFX, and SFX behavior.
- Physical rapid-click and stale-callback timing.

## 41. Known risks

- The shared Inspiration pool funds both sides; ownership was deliberately preserved.
- `CombatTimeline` retains its legacy static order counter, but production does not call it.
- The Test-scene direct `CombatTestRunner` remains an isolated bypass and does not represent canonical submission.
- Existing public collection mutation is source-restricted to read-only views; external code outside this repository that mutated those collections would require migration to guarded APIs.
- `Event_Area` presentation was compiler-validated but not visually tested.
- A calculation exception may leave mutations applied before the exception; it marks `ResolutionFailed`, never enters presentation, and cannot be recalculated, but no transactional HP rollback is claimed.

## 42. Preserved Debug/Test/Legacy compatibility

- `CombatAutoPlanner` remains Debug-only and uses canonical `SubmitCurrentTurn`.
- `CombatSkillDebugInvoker` remains a direct Debug `SkillRunner` bypass.
- `CombatTestRunner` and `CombatStartSmokeTest` remain direct isolated Test/Debug constructors and compile with the guarded event API.
- Editor F9/F10 force-exit shortcuts remain isolated and are not routed through production input.
- `CombatTimeline` remains intact as a non-production compatibility path.
- Demo flow code remains preserved and unreferenced.
- No Debug, Demo, Test, or Legacy file was deleted, moved, or promoted to production ownership.

## 43. Unrelated changes

The initial worktree was clean. No scene, prefab, ScriptableObject, Input Actions, generated wrapper, package, or unrelated runtime file was changed. No destructive Git command, commit, push, branch, pull request, or Batch 5 work was performed.

## 44. Exact recommended next batch

Runtime Refactor Batch 5 — Combat UI Routing

Do not begin Batch 5 until the blocked EditMode suites and Dungeon 1 manual procedure for this batch have been executed successfully.
