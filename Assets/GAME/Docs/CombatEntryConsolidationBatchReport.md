# Runtime Refactor Batch 3 — Combat Entry Consolidation Report

## 1. Implementation result

`CombatEntryPoint.StartCombat(CombatStartRequest)` is the single validated, atomic production combat-start boundary. The verified production path is:

`Field encounter / field attack / tutorial request -> CombatStartRequest -> CombatEntryPoint.StartCombat -> normalization -> complete preflight -> FieldCombatantFactory -> CombatBootstrapper -> CombatSession -> CombatStateMachine`

The historical first-member truncation statement is stale. Before this batch, `CombatEntryPoint.AddFieldObjects` already copied every unique non-null member and `FieldCombatantFactory.CreateSide` already iterated every retained member. That full-roster behavior was preserved, verified after construction, and locked with 1v1, 1v2, 2v2, order, side, ID, compatibility-adapter, and field-outcome tests.

No second entry point, manager, encounter framework, session constructor, or combat-state writer was added. Runtime Refactor Batch 4 was not started.

## 2. Loaded instruction and reference files

Instructions loaded:

- repository-root `AGENTS.md`
- no nested `AGENTS.md` or `AGENTS.override.md` exists

Required supporting documents read completely:

- `Assets/GAME/Docs/CurrentRuntimeCodeAudit.md`
- `Assets/GAME/Docs/CombatFlowAudit.md`
- `Assets/GAME/Docs/InputAndGameStateAudit.md`
- `Assets/GAME/Docs/RuntimeRefactorBatchPlan.md`
- `Assets/GAME/Docs/GameStateOwnershipBatchReport.md`
- `Assets/GAME/Docs/InputOwnershipBatchReport.md`

Current source, serialized assets, and Unity results took precedence where older documents still reported roster truncation.

## 3. Initial branch and status

- Branch: `main`, tracking `origin/main`.
- Initial uncommitted state: untracked `Assets/.tmp.driveupload/` content and untracked `Assets/GAME/Combat/Editor.meta`.
- Neither item was edited, staged, deleted, moved, or incorporated into this batch. The temporary upload directory became empty through external workspace activity during the task and therefore no longer appears in Git status; the pre-existing `Editor.meta` remains untracked and unchanged.

## 4. Modified files

- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatEntryPoint.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Adapters/FieldCombatantFactory.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatEncounterGroup.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatEncounterTrigger2D.cs`
- `Assets/GAME/Scripts/Player/Runtime/PlayerFieldAttackController.cs`
- `Assets/GAME/Scripts/Core/GameStateMachine.cs`

`CombatEncounterGroup.cs` was mechanically normalized from its prior non-UTF-8 encoding so the repository patch tool could safely edit it. Its code content then received only the scoped encounter-roster changes; its `.meta` and GUID were not changed.

## 5. Added files

- `Assets/GAME/Tests/Editor/CombatEntryConsolidationTests.cs`
- `Assets/GAME/Tests/Editor/CombatEntryConsolidationTests.cs.meta`
- `Assets/GAME/Docs/CombatEntryConsolidationBatchReport.md`
- `Assets/GAME/Docs/CombatEntryConsolidationBatchReport.md.meta`

## 6. Deleted or moved files

None.

## 7. Production combat entry owner

`Game.Combat.Core.CombatEntryPoint` remains the production start/end lifecycle owner. It accepts the public request, prevents concurrent/active starts, requires the global flow dependencies, normalizes without mutating caller data, validates every retained combatant, constructs only through `CombatBootstrapper`, verifies the resulting complete roster, binds the existing collaborators, synchronizes global combat state through `GameFlowController`, and publishes `OnCombatStarted` once.

`CombatBootstrapper` remains the only production `CombatSession` and `CombatStateMachine` constructor. `FieldCombatantFactory` remains the field-object adapter factory. `CombatStateMachine`, resolver, director, UI, reward, and quest responsibilities were not redesigned.

## 8. Exact production caller map

| Caller | Classification | Serialized use | Path |
|---|---|---|---|
| `CombatEncounterTrigger2D` | Production | Dungeon 1: `Enemy_Ghost`, `Enemy_Ghost (1)`, `Enemy_Ghost (2)`; Test: `Enemy_Angel` | builds `CombatStartRequest` -> `CombatEntryPoint.StartCombat` |
| `PlayerFieldAttackController` | Production | Dungeon 1: active `Player_new`; inactive compatibility `Player` | one deduplicated encounter snapshot -> `CombatEntryPoint.StartCombat` |
| `TutorialBattleStartInteractionEventSO` | Tutorial production adapter | no serialized SO asset reference found | resolves request rosters -> `CombatEntryPoint.StartCombat`; quest advancement remains conditional on acceptance |
| `FieldEnemy` | Legacy compatibility | disabled component on Test `Enemy_Angel` only | builds request -> `CombatEntryPoint.StartCombat` |
| `CombatFieldCallDebug` | Debug | Test `CombatFieldCallDebug` | `StartCombatFromField` -> canonical `StartCombat` |

No production UI, quest, reward, scene-start, or state-sync component directly creates a session. `TutorialQuestCombatBridge` observes `OnCombatEnded` and does not start combat.

Serialized production ownership found:

- `CombatEntryPoint`: Demo, Dungeon 1, InGame, Test, and `Assets/GAME/Prefabs/DungeonCombatRuntime.prefab`.
- Dungeon 1 `CombatManager/CombatEntryPoint` is referenced by all three encounter triggers and both serialized player field-attack controllers.
- Every serialized scene containing a production `CombatEntryPoint` also contains `GameStateMachine` and `GameFlowController`.
- No `CombatEncounterGroup` component is currently serialized in GAME scenes or prefabs.

## 9. Tutorial entry paths

`TutorialBattleStartInteractionEventSO` was already a correct production adapter and required no modification. It builds a `CombatStartRequest`, preserves tutorial initiative/opening effect/quest context, calls `CombatEntryPoint.StartCombat`, and advances the tutorial quest only after acceptance. It does not construct a session or own entry validation.

`TutorialQuestCombatBridge` remains completion-only and preserves existing quest behavior and serialized fields.

## 10. Debug/Test direct-session bypasses retained

- `CombatStartSmokeTest` in `Assets/GAME/Scenes/Test.unity` directly calls `CombatBootstrapper.StartCombat`. It is Debug-only.
- `CombatTestRunner` in `Assets/GAME/Combat/Tests/Scenes/CombatTest.unity` directly constructs `CombatSession` and `CombatStateMachine`. It is an isolated Test harness.
- `CombatFieldCallDebug` remains in Test and uses the compatibility entry adapter rather than constructing a session.
- `CombatEntryPoint` editor F9/F10 shortcuts still force completion only; they do not start combat and were not routed through production input.

No Debug/Test bypass was connected to a production scene or made a production dependency.

## 11. Legacy entry paths retained

- Disabled Test-only `FieldEnemy` remains a compatibility request caller.
- `SeamlessBattleManager` remains an unreferenced Legacy state/UI-only path; it creates no session.
- `BattleTransitionController` remains serialized in InGame and Test as a Legacy transition/state presentation path; it creates no session.
- Legacy `BattleTrigger2D` event behavior remains untouched.

These files were not deleted, moved, promoted, or newly referenced.

## 12. Request normalization policy

`CombatEntryPoint` creates a private immutable startup snapshot containing arrays and the resolved scalar fields. The caller's `CombatStartRequest` and its lists are never cleared, reordered, deduplicated, or otherwise mutated.

Normalization:

1. preserves the original order of unique non-null objects on each side;
2. removes nulls and within-side duplicate references;
3. detects the same non-null object on both sides before inactive filtering and rejects the request;
4. removes inactive-in-hierarchy objects according to the policy below;
5. preserves `Reason`, `InitiativeSide`, and `OpeningEffectOrNull`;
6. uses request inspiration max when positive, otherwise the serialized max clamped to at least one;
7. uses request start when non-negative, otherwise the serialized default; the result is clamped once to `[0, resolved max]`.

Only the private snapshot is converted to a fresh `CombatStartRequest` for the existing bootstrapper API.

## 13. Invalid-combatant policy

The production policy is all-or-nothing. Every active explicitly requested object is inspected through `HpAccessor` before construction. If any object lacks a readable/writable HP source, has no source component, throws while reading HP, has non-positive MaxHP, negative HP, or HP greater than MaxHP, the entire request is rejected. A mixed valid/invalid roster never starts a partial encounter.

Missing or unresolved optional skills retain the existing `FieldCombatantFactory` loadout/fallback behavior and do not invalidate an otherwise valid combatant.

Each rejection returns false and produces one contextual diagnostic with rejection reason, request `StartReason`, requested counts, normalized valid counts, and the first offending object while still validating the complete roster.

## 14. Inactive-object policy

Inactive-in-hierarchy field objects are excluded during normalization. If this empties either side, the request is rejected. Mixed active/inactive requests retain only active members; this is distinct from the all-or-nothing invalid-HP policy and is covered by tests. Inactive filtering never changes the caller's lists.

## 15. Full-roster behavior

The existing full-roster copy and factory loops were preserved. After bootstrap, entry now verifies:

- constructed ally/enemy counts exactly match the normalized snapshot;
- each index retains request order;
- every combatant remains on the requested side;
- every constructed combatant is a `FieldCombatantAdapter` over the expected field object;
- every `CombatantId` is unique across both sides.

`FieldCombatantFactory` now starts enemy IDs at `max(100, next ally ID)`, preserving established small-roster IDs while preventing overlap for unusually large ally rosters. Field-outcome processing still iterates every enemy and is covered by a two-defeated-enemy test.

## 16. Duplicate-start behavior

`CombatEntryPoint` rejects starts while `_startingCombat`, `ActiveSession`, or `ActiveStateMachine` is set. The collision/attack callers no longer depend on their own active-state check as authority.

`CombatEncounterTrigger2D` now has local in-progress and same-frame noise guards. It disarms only after `StartCombat` returns true; rejection clears the in-progress flag and leaves the trigger armed for a later activation.

`PlayerFieldAttackController` resolves one unique encounter root, creates one active/deduplicated enemy snapshot (preserving the complete group roster), submits once, and returns after rejection or acceptance. Rejection does not set its combat-started latch; cooldown/animation/hit timing remain unchanged.

`OnCombatStarted` is published once per accepted start. Observer exceptions are logged individually so one presentation subscriber cannot emit a second start or invalidate the already-committed session.

## 17. Atomic rollback behavior

Preflight completes before `_startingCombat` is set or any runtime collaborator is bound. Transactional startup then:

1. constructs through `CombatBootstrapper`;
2. verifies non-null session/state machine and the exact complete roster;
3. assigns active references;
4. binds the existing orchestrator;
5. subscribes phase/director callbacks;
6. synchronizes the global state;
7. publishes one started event;
8. clears `_startingCombat` in `finally`.

An exception before commit unsubscribes any partial callbacks, binds the orchestrator to null, clears active references and flags, and restores Exploration if this startup changed global state. An injected state-change subscriber exception is covered by an automated rollback/reuse test.

The only Batch 1 compatibility adjustment is allowing `CombatPlanning -> Exploration` and `CombatResolving -> Exploration`, required for startup rollback after a state subscriber throws. Normal successful combat completion still follows the existing Reward flow.

## 18. Public API changes

Preserved without signature changes:

- `CombatEntryPoint.StartCombat`
- `CombatEntryPoint.StartCombatFromField`
- `ActiveSession`
- `ActiveStateMachine`
- `OnCombatStarted`
- `OnCombatEnded`
- `CombatStartRequest` constructor and fields
- `CombatEncounterGroup.GetActiveEnemies`

No new public manager or public runtime type was added. The normalized snapshot is a private nested non-MonoBehaviour. The public GameState API is unchanged; only two transition-policy edges were extended for atomic rollback.

## 19. Inspector impact

Required Inspector changes: none.

All existing serialized fields on entry, trigger, group, player attack, tutorial, Legacy, and Debug components are preserved with their names and types. No automatic reconnection is required.

Dungeon 1 currently has no serialized `CombatEncounterGroup`; a temporary unsaved group is required for manual multi-enemy validation. This batch does not require designers to save a scene change.

## 20. Scene/prefab/SO/.meta impact

- `.unity`: unchanged.
- `.prefab`: unchanged.
- `.asset` / ScriptableObjects: unchanged.
- Input Actions and generated input code: unchanged.
- Existing `.meta` files and GUIDs: unchanged.
- New test/report assets: each has one Unity-generated new `.meta`.
- No Missing Script signature was introduced by code changes; final scan results are included in the completion response.

## 21. Unity compilation result

Unity `6000.2.6f2` was run against isolated project copies because Unity processes were already present. Compilation and the test assembly build completed with zero C# errors.

Warnings were existing project/plugin warnings only:

- two MCP-for-Unity obsolete `FindObjectsOfType` warnings;
- obsolete TMP word-wrapping API;
- unused Legacy `FieldEnemy.OnBattleRequested`;
- retained compatibility fields on `StoryInteractionController` and `MissionCompleteCutsceneController`;
- unused DemoMission `RescueNpcActor.interactKey`.

No new warning points to a Batch 3 file.

## 22. GameStateOwnershipTests result

6 passed, 0 failed, 0 skipped.

## 23. InputOwnershipTests result

18 passed, 0 failed, 0 skipped.

## 24. CombatEntryConsolidationTests result

35 passed, 0 failed, 0 skipped.

The existing `CombatFoundationTests` regression suite also passed: 2 passed, 0 failed, 0 skipped.

Combined requested/regression EditMode result: 61 passed, 0 failed, 0 skipped.

## 25. Manual Play Mode result

Not executed. Batch-mode Unity does not validate physical collision timing, visual field lock, UI widgets, animation, or an unsaved Dungeon 1 group configuration. No manual result is claimed.

Required Dungeon 1 procedure:

1. Open `Assets/GAME/Scenes/Dungeon 1.unity`; enter Play Mode and confirm Exploration input plus exactly one intended `CombatManager/CombatEntryPoint`.
2. Contact each encounter with one and multiple player colliders; confirm one accepted start, one started event, CombatPlanning, and immediate field lock.
3. Field-attack while outside contact where possible; confirm PlayerFirstHit/Allies initiative and one request. Exercise a rejection and confirm the next attack remains available after cooldown.
4. Without saving, temporarily group at least two valid enemies under a `CombatEncounterGroup`; add a helper/marker child. Confirm session enemy count equals valid combatants, helper exclusion, unique targets where UI supports them, and every defeated enemy reaches field outcome handling.
5. Temporarily remove an HP adapter from one requested member. Confirm false return, Exploration unchanged, null active references, then restore HP and start successfully through the same entry.
6. Attack while entering a contact trigger. Confirm one source is accepted and the other returns false without duplicate UI, binding, or event.
7. Complete combat, close Reward, confirm one Exploration return, unchanged defeated-enemy behavior, and successful entry into a later encounter.
8. Check Console for exceptions, repeated rejection spam, or a stuck startup flag.

## 26. Unexecuted tests

- Interactive Dungeon 1 contact/field-attack collision tests.
- Visual combat widget/formation/camera behavior for a temporary multi-enemy group.
- Tutorial interaction through a live serialized `TutorialBattleStartInteractionEventSO`; no such asset reference was found.
- Physical duplicate contact and field-attack race timing.
- Player defeat/world restoration behavior, which remains outside this batch.

## 27. Known risks

- Dungeon 1 has three independent 1v1 triggers and no serialized group, so production multi-enemy presentation remains a manual unsaved configuration test.
- Auto-collection intentionally accepts only active direct children that satisfy the same HP adaptation contract. A designer who puts the HP component only on a grandchild must use a manual list or adjust hierarchy in a later world-integration batch.
- Manually serialized group lists preserve invalid active objects so the entry can reject the entire authored encounter rather than silently changing it.
- Strict production entry now requires both global state and flow owners. Current entry scenes contain them; dynamically created isolated entries must provide them.
- Observer exceptions from `OnCombatStarted` are logged and isolated after commit; subscriber-side cleanup remains the subscriber owner's responsibility.
- Legacy state-only paths remain capable of changing combat-related GameState without a session, but no new code calls them.

## 28. Preserved compatibility

- Existing public start APIs and serialized fields.
- Existing full-roster `AddFieldObjects` behavior.
- Existing loadout and fallback-skill behavior.
- Tutorial quest and UnityEvent compatibility.
- Debug `StartCombatFromField`, direct bootstrap smoke test, and direct-session test runner.
- Disabled Test `FieldEnemy`, Legacy battle transition and seamless manager.
- F9/F10 debug completion shortcuts.
- Combat resolver, timeline, skill runner, damage, director, animation, UI routing, reward, quest, save/load, and input ownership.

## 29. Unrelated changes

The pre-existing untracked `Assets/GAME/Combat/Editor.meta` remains untracked and byte-for-byte outside this batch. No destructive Git command, reset, checkout, commit, push, branch, or pull request was performed. No unrelated tracked file was reformatted or cleaned up.

## 30. Exact recommended next batch

Runtime Refactor Batch 4 — Combat Session and Turn Flow

This report does not begin or implement Batch 4.
