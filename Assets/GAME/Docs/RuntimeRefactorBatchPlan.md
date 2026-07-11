# Runtime Refactor Batch Plan

Each batch must begin and end with a Unity compile, targeted Play Mode test, Missing Script scan, and `git diff` review. “Add” means optional tests/adapters in the existing subsystem, never a parallel manager.

## 1. GameState ownership

- Modify: `GameStateMachine.cs`, `GameFlowController.cs`, `UIScreenRouter.cs`, combat/state/dialogue/quest writers listed in `InputAndGameStateAudit.md`.
- Add: transition tests and, only if needed, transition-policy data adjacent to Core.
- Untouched: combat calculation/model, ScriptableObjects, scenes until API compiles.
- Inspector: later redirect serialized state coordinators; no enum reorder/removal.
- Risks: alias values, UIOnly/Cutscene compatibility, bootstrap-created services.
- Test: every documented transition, illegal/duplicate transition logging, scene load.
- Complete: one owner authorizes global transitions; CombatResolving, Dialogue, Choice are represented accurately.

## 2. Input ownership

- Modify: `GameInputInstaller.cs`, `InputService.cs`, `InputRouter.cs`, `PlayerInputController.cs`, Story interaction/runner, combat/reward UI command handlers.
- Add: state-aware input tests/adapters only.
- Untouched: generated `inputactions.cs`, action asset, UI prefabs in first pass.
- Inspector: inventory and later remove legacy InputActionReference/PlayerInput bindings only after replacements are assigned.
- Risks: inactive subscriber ordering, duplicate Interact consumption, legacy Demo/Test controls.
- Test: movement/interact/confirm/cancel/pause per state, device switch, enable/disable cycles.
- Complete: one action-map owner; no production direct polling or component-owned map switching.

## 3. Combat entry consolidation

- Modify: `CombatEntryPoint.cs`, `CombatEncounterTrigger2D.cs`, `FieldEnemy.cs`, `PlayerFieldAttackController.cs`, tutorial adapters.
- Add: request validation and entry tests.
- Untouched: resolver/director/UI behavior.
- Inspector: ensure every production trigger references the same scene entry point; remove legacy trigger only after migration.
- Risks: duplicate collision starts, multi-enemy list truncation, missing HP/loadout components.
- Test: touch, player-first-hit, enemy-first-hit, tutorial, duplicate start, multi-enemy group.
- Complete: every production path calls entry; full requested rosters enter one session; debug bypasses remain isolated.

## 4. Combat session and turn flow

- Modify: `CombatStateMachine.cs`, `CombatFlowOrchestrator.cs`, `CombatTurnResolver.cs`, `CombatTimeline.cs`, `CombatEndEvaluator.cs`, `CombatResultBuilder.cs` as tests require.
- Add: edit-mode calculation tests for order, targeting, damage, death, victory/defeat.
- Untouched: scene YAML, field presentation, reward UI.
- Inspector: none expected.
- Risks: double resolution (entry currently resolves before confirm), callback timing, already-mutated HP during animation.
- Test: deterministic plans, ties, dead actor skip, no target, consecutive turns, both end reasons.
- Complete: one resolve call per turn and explicit planning/resolving/exit events with deterministic results.

## 5. Combat UI routing

- Modify: `UIScreenRouter.cs`, `GameUIRootController.cs`, `CombatUIRootController.cs`, `CombatPlanningHUD.cs`, `CombatWidgetManager.cs`, `CombatLogHUD.cs`.
- Add: UI routing tests if practical.
- Untouched: resolver/model and reward grant logic.
- Inspector: assign canonical field/combat roots; preserve button/widget prefabs; disable competing root owner only after verification.
- Risks: inactive panels cannot subscribe, duplicate SetActive calls, missing auto-bound roots.
- Test: start, plan, resolve, next turn, exit across Demo/Dungeon 1/Test.
- Complete: router alone owns top-level canvases; panels own only local content.

## 6. Combat completion and reward flow

- Modify: `CombatEntryPoint.cs`, `CombatRewardUIBinder.cs`, `RewardService.cs`, `RewardUIPanel.cs`, `GameFlowController.cs`; classify `CombatDemoFlowController`.
- Add: reward idempotency/completion tests.
- Untouched: quest definitions and encounter triggers.
- Inspector: choose one reward binder/panel, assign service/close button/row prefab; remove duplicate listeners after validation.
- Risks: double grant/show/restore, defeat semantics, clearing session too early, per-combat versus per-enemy counts.
- Test: victory, defeat, duplicate event, close once/twice, missing panel/service.
- Complete: exactly one grant and panel show; one deterministic post-close state/restoration path.

## 7. World encounter integration

- Modify: `CombatEncounterGroup.cs`, `CombatEncounterTrigger2D.cs`, field enemy/player attack adapters, `CombatFieldLock.cs`, formation/camera integration.
- Add: encounter fixture scenes/prefabs only after code compiles.
- Untouched: core damage formulas, quest/reward data.
- Inspector: group membership, colliders/layers, player reference, formation anchors, lock behaviour list.
- Risks: duplicate triggers, destroyed/deactivated field objects, AI continuing during combat, multi-enemy cleanup.
- Test: approach/touch/attack, group sizes, victory cleanup, defeat restoration, re-entry prevention.
- Complete: encounter objects adapt and restore through one lifecycle with correct roster/outcome.

## 8. Dialogue and quest integration

- Modify: canonical Story runner/panels, `QuestRuntime.cs`, objective/completion flow, DemoMission bridge/actors.
- Add: asset migration adapters and quest/story integration tests.
- Untouched: original DemoMission/Quest assets until converted and validated.
- Inspector: select canonical dialogue stack; map mission/quest definitions, IDs, rescue actor, objective HUD, completion panel.
- Risks: three progress models, duplicate rewards/completion events, lost SO fields, Choice state absent today.
- Test: exploration -> dialogue -> choice -> exploration, rescue, kill counts, completion during combat deferral, scene return.
- Complete: QuestRuntime owns runtime objectives; Demo adapters publish events; dialogue states and UI route correctly.

## 9. Debug and Legacy separation

- Modify/move with `.meta`: explicit debug files, dummy factory/combatants, obsolete FieldEnemy/transition/player/input stacks after all references are migrated.
- Add: assembly definitions or build guards for Debugging/Legacy if compatible.
- Untouched: production entry/model and test scenes until references are explicit.
- Inspector: remove debug components from production scenes; retain them in Test/CombatTest.
- Risks: moving without meta breaks GUIDs; hidden UnityEvents; legacy scenes may still rely on old controls.
- Test: Missing Script scan all scenes/prefabs, production build excludes debug hotkeys, test scenes still run.
- Complete: production scenes contain no debug bypass; unclear usage remains Legacy, not deleted.

## 10. Save/load preparation

- Modify: `SaveLoadService.cs`, NonCombat save contracts/serializer, `QuestRuntime.cs`, `RewardService.cs`, story flags/progress, inventory/currency.
- Add: versioned DTOs/migrations and round-trip tests within existing save subsystem.
- Untouched: presentation and scene YAML.
- Inspector: stable definition IDs; service references for inventory/currency/quest/story.
- Risks: duplicate quest models, transient combat state, reward idempotency keys based on runtime hash, renamed string IDs.
- Test: new save, round trip, older version migration, mid-exploration and post-reward saves, duplicate reward prevention.
- Complete: stable IDs and versioned data restore progression without serializing scene object references or active combat internals.

