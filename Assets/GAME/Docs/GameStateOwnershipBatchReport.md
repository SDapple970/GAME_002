# GameState Ownership Batch Report

Date: 2026-07-11

## Summary

Batch 1 establishes `GameStateMachine` as validated global state storage and `GameFlowController` as the production-facing flow API. Combat-local phase changes now synchronize Planning and Resolving before presentation. Canonical Story dialogue uses Dialogue and Choice. Combat reward closure restores Exploration once.

## Modified files

- `Assets/GAME/Scripts/Core/GameStateMachine.cs`
- `Assets/GAME/Scripts/Core/GameFlowController.cs`
- `Assets/GAME/Scripts/Core/SceneFlowController.cs`
- `Assets/GAME/Scripts/Core/RuntimeBootstrapper.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatEntryPoint.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatStateMachine.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatStateSyncer.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatDemoFlowController.cs`
- `Assets/GAME/Scripts/Combat/Runtime/UI/CombatRewardUIBinder.cs`
- `Assets/GAME/Scripts/Quest/QuestCompletionFlow.cs`
- `Assets/GAME/Scripts/Story/Runtime/Core/DialogueRunner.cs`
- `Assets/GAME/Scripts/Story/Runtime/StoryEventRunner.cs`
- `Assets/GAME/Scripts/Dialogue/TimedChoiceDialoguePanel.cs`

`UIScreenRouter.cs` and `GameUIRootController.cs` required no code change: the existing router already recognizes Dialogue, Choice, Reward, Paused, and both combat states through `GameStateMachine.IsCombatState`.

## Added files

- `Assets/GAME/Tests/Editor/GameStateOwnershipTests.cs`
- `Assets/GAME/Docs/GameStateOwnershipBatchReport.md`

No scene, prefab, ScriptableObject, generated input wrapper, or `.meta` file is part of this batch.

## Public API changes

`GameStateMachine` adds `Previous`, `IsTransitioning`, `TrySetState(GameState, string)`, and `IsTransitionAllowed(GameState, GameState)`. Existing `SetState(GameState)` remains and delegates to validation. `OnStateChanged` retains `Action<GameState, GameState>`.

`GameFlowController` adds `BeginLoading`, `BeginDialogue`, `BeginChoice`, `BeginCombatTransition`, `EnterCombatPlanning`, `EnterCombatResolving`, `EnterUIOnly`, `EnterCutscene`, `Pause`, `ResumePreviousState`, and generic `RequestState`. Existing exploration, reward, combat-result, and reward-close methods remain.

`CombatStateMachine` adds `OnPhaseChanged(Action<Phase, Phase>)`; existing phases and APIs are unchanged.

## Transition policy

The policy is explicit per source state. Required paths are supported: Boot to Title/Loading; Title to Loading/Exploration; Loading to Title/Exploration; Exploration to Dialogue/Combat/Cutscene/UIOnly; Dialogue and Choice round trips; combat transition/planning/resolving/reward; Reward to Exploration/Loading; Cutscene and UIOnly completion paths; and pause/resume to the exact captured state.

Invalid and reentrant transitions are rejected with previous state, requested state, reason, and rejection cause. Duplicate requests return false without logging or events. `Previous` changes only on an accepted transition.

## Compatibility transitions retained

- `Boot -> Exploration`: bootstrap compatibility for field scenes.
- `Exploration -> Title`: RuntimeBootstrapper starts with the historical default Exploration value before inferring a title scene.
- `Exploration -> CombatPlanning`: current combat has no mandatory transition presentation.
- `Exploration -> Reward`: non-combat reward/quest compatibility.
- `CombatPlanning/CombatResolving -> UIOnly`: deferred Demo/cutscene compatibility.
- `Cutscene -> Reward` and `UIOnly <-> Cutscene`: current mission-ending flows.
- `Title -> UIOnly`, `Reward -> UIOnly`, and dialogue/choice to Cutscene/UIOnly/Loading: existing UI and travel flows.

## Direct writers migrated

Scene loading, runtime initial state, CombatEntryPoint phase synchronization, combat reward start/close, QuestCompletionFlow reward entry, DialogueRunner, StoryEventRunner, and TimedChoiceDialoguePanel now request through GameFlowController with a validated fallback.

`CombatStateSyncer` is a passive compatibility observer. `CombatDemoFlowController` retains presentation, camera, and field-lock behavior but no longer writes global state.

## Direct writers intentionally deferred

DemoMission completion/end controllers, MissionCompleteCutsceneController, SceneTravelService, legacy BattleTransitionController/SeamlessBattleManager, NonCombat DialogueController, MissionCompletePanel, and older dialogue/demo flows still call compatibility `SetState`. Their requests are now validated centrally. Migrating them belongs with dialogue/quest integration or Debug/Legacy separation, not this batch.

## Inspector changes

None. All serialized fields, including `DialogueRunner.useCutsceneState`, remain for serialization compatibility. Its value no longer changes dialogue state; actual cutscenes must request `EnterCutscene` through flow code.

## Automated results

Unity EditMode test filter `Game.Tests.Core.GameStateOwnershipTests`: 6 passed, 0 failed. Covered duplicate suppression, invalid rejection/logging, exact pause restoration, Loading completion, Dialogue/Choice round trip, and reentrant transition rejection.

Unity 6000.2.6f2 batch compilation: exit code 0, no C# errors. Remaining warnings are pre-existing obsolete TMP wrapping, unused legacy FieldEnemy event, and unused RescueNpcActor key.

## Manual Play Mode results

Not executed. Batch mode cannot perform the requested interactive Dungeon 1 combat sequence or verify movement/buttons visually. Required manual sequence remains Exploration -> CombatPlanning -> CombatResolving -> Planning -> Resolving -> Reward -> Exploration, plus title/story/quest regressions.

## Known risks

- A missing `CombatRewardUIBinder` leaves global state in CombatResolving after combat exit because the canonical reward result subscriber is absent.
- `CombatDemoFlowController` can still show the same reward panel as the production binder; its state writes are removed, but presentation ownership remains for Batch 5/6.
- Deferred compatibility writers may request rejected edges; warnings should be collected during full scene Play Mode testing.
- RuntimeBootstrapper can still create unwired UI service components; this batch changes state ownership, not UI hierarchy ownership.
- Multi-combatant request truncation, input action-map ownership, quest model duplication, and save/load are intentionally unchanged.

## Recommended next batch

Proceed with Batch 2, Input ownership. Make `GameInputInstaller` the sole action-map owner, route production commands through the existing input service/router, and retain serialized InputActionReferences until each scene binding is validated. Do not combine that work with combat entry consolidation.
