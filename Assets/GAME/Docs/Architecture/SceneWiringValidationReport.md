# Scene Wiring Validation Report

Date: 2026-06-20

## 1. Scenes Inspected

- `Assets/GAME/Scenes/TitleScene.unity`
- `Assets/GAME/Scenes/Dungeon 1.unity`
- `Assets/GAME/Scenes/Demo.unity`
- `Assets/GAME/Scenes/Test.unity`
- `Assets/GAME/Scenes/InGame.unity`
- `Assets/GAME/Scenes/TutorialScene.unity`
- `Assets/GAME/Scenes/Dungeon 2.unity`
- `Assets/GAME/Scenes/Dungeon 3.unity`
- `Assets/GAME/Scenes/Dungeon 4.unity`
- `Assets/GAME/Scenes/Dungeon 5.unity`

Main playable validation focused on `TitleScene`, `Dungeon 1`, `Demo`, `Test`, and `InGame`. `Dungeon 2` through `Dungeon 5` and `TutorialScene` have no detected production-owner references in YAML and should be manually classified before forcing production wiring into them.

## 2. Objects / Components Added

No scene or prefab YAML was edited in this pass.

Runtime-only fallback creation was added through `RuntimeBootstrapper`:

- Creates a scene-local `RuntimeBootstrapper` after scene load if none exists.
- Finds existing core services before creating missing ones.
- Creates missing core services only when `createMissingCoreServices` is enabled.
- Core services covered by fallback creation:
  - `GameStateMachine`
  - `GameInputInstaller`
  - `GameFlowController`
  - `SceneFlowController`
  - `SaveLoadService`
  - `RewardService`
  - `GameUIRootController`
  - `UIScreenRouter`

This is intentionally conservative scene wiring. It avoids duplicate scene-authored objects and lets existing singleton guards destroy duplicate persistent instances.

## 3. Objects / Components Linked

No serialized scene links were written.

Runtime auto-linking was added:

- `GameUIRootController` now auto-binds missing roots when possible:
  - field HUD through `OverworldHUDRoot`
  - dialogue UI through `DialoguePanel`, `DialogueUIPanel`, or `StoryDialogueHUD`
  - choice UI through `ChoiceUIPanel` or `TimedChoicePanel`
  - combat UI through `CombatUIRootController` or `CombatPlanningHUD`
  - reward UI through `RewardUIPanel`
  - pause root by object name `PauseMenu`
  - loading root by object name `LoadingPanel`
- `UIScreenRouter` can now function in scenes where the controller/router are runtime-created and known UI roots exist.
- `GameInputInstaller` now safely ignores `OnEnable`/`OnDisable` when a duplicate installer is destroyed before it initializes input actions.
- `RescueNpcActor` now ignores routed interact input unless `GameStateMachine` allows Exploration input.

## 4. Compile / Static Validation Result

Unity Editor CLI was not available on PATH:

- `Unity`: not found
- `Unity.exe`: not found

`dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` exits with failure but reports:

- warnings: 0
- errors: 0

This appears to be Unity-generated project/tooling behavior rather than a C# compiler diagnostic. Unity Editor compilation in Unity 6000.2.6f2 is still required.

Static validation completed:

- Edited files were reviewed for syntax and referenced types.
- No `m_Script: {fileID: 0}` missing-script markers were found in `Assets/GAME/Scenes` or `Assets/GAME/Prefabs`.
- Runtime code search found no dependency on `Game.Debugging` or `Game.Legacy`.
- The only `GameState.Combat` usage found is inside `Assets/GAME/Scripts/Legacy/Battle/SeamlessBattleManager.cs`.

## 5. Missing Script Results

Search command:

`rg "m_Script: \{fileID: 0\}" Assets/GAME/Scenes Assets/GAME/Prefabs -g "*.unity" -g "*.prefab"`

Result: no matches.

## 6. Direct SceneManager.LoadScene Calls Still Remaining

Official owner is `SceneFlowController`, but compatibility/direct calls remain:

- `Assets/GAME/Scripts/Core/SceneFlowController.cs`
- `Assets/GAME/Scripts/Story/SceneTravelService.cs`
- `Assets/GAME/Scripts/UI/BattleTransitionController.cs`
- `Assets/GAME/Scripts/Title/Runtime/TitleSceneController.cs`
- `Assets/GAME/Scripts/UI/DemoEndController.cs`
- `Assets/GAME/Scripts/DemoMission/UI/MissionCompletePanel.cs`
- `Assets/GAME/Scripts/DemoMission/UI/CaseFileAcceptController.cs`
- `Assets/GAME/Scripts/DemoMission/Runtime/Ending/DemoRescueNpcEndFlow.cs`
- `Assets/GAME/Scripts/DemoMission/Runtime/Ending/DemoEndPanelController.cs`

These were not migrated in this pass to avoid broad flow changes. Next pass should convert button and mission/end-panel navigation into `SceneFlowController` calls or explicitly documented wrappers.

## 7. Direct InputActionReference Usage Still Remaining

Allowed / official input layer:

- `Assets/GAME/Scripts/Input/OverworldInputAdapter.cs`

Runtime compatibility outside input layer:

- `Assets/GAME/Scripts/Cutscene/Runtime/MissionCompleteCutsceneController.cs`

Debug-only:

- `Assets/GAME/Scripts/Debugging/Combat/CombatFieldCallDebug.cs`
- `Assets/GAME/Scripts/Debugging/Combat/CombatStartSmokeTest.cs`

Current production route remains:

`Unity Input System -> GameInputInstaller -> InputService/InputRouter -> target subscriber`

## 8. Direct UI SetActive Ownership Conflicts Still Remaining

`UIScreenRouter` is now able to control the major roots, but direct `SetActive` calls remain across legacy/compatibility UI:

- reward: `RewardUIPanel`
- combat UI: `CombatUIRootController`, `CombatPlanningHUD`, `CombatDemoFlowController`
- mission/quest HUDs and panels
- search HUDs
- title and demo end panels
- dialogue/choice panels
- cutscene reward/video panels
- interaction prompts
- old Legacy Battle UI root toggling

This pass did not remove those calls because the router replacement is not yet fully serialized into scenes and several panels still own internal sub-root visibility. The next pass should separate root ownership from internal widget visibility.

## 9. Reward Flow Result

Current validated compatibility flow:

`CombatEntryPoint.OnCombatEnded -> CombatRewardUIBinder -> RewardService.GrantCombatResult -> RewardUIPanel.Show -> RewardUIPanel.OnClosed -> GameFlowController.HandleRewardClosed`

Status:

- `RewardService` is ensured at runtime by `RuntimeBootstrapper`.
- `RewardUIPanel` remains display-only for combat result rewards.
- `RewardUIPanel.ApplyReward` is a no-op compatibility hook.
- `GameFlowController` handles reward-close fallback to Exploration.
- No `RewardFlow` class exists yet.

Risk:

- `CombatRewardUIBinder` still calls `DemoMissionRuntime.RegisterEnemyDefeated()` on combat victory for compatibility. Field enemy kill accounting should be manually checked to ensure no double objective progress.

## 10. Input Flow Result

Runtime route is present and safer:

- `GameInputInstaller` owns the generated `GameInput` instance.
- `InputRouter` gates movement, jump, attack, and parry to `Exploration`.
- Interact is routed through `GameInputInstaller` and allows only exploration, dialogue advance, or UI input according to router state.
- `RescueNpcActor` now rejects interact events outside Exploration.
- Duplicate destroyed `GameInputInstaller` instances no longer dereference null actions during enable/disable.

Expected state behavior:

- `Exploration`: movement and field interaction allowed.
- `Dialogue`, `Choice`, `CombatTransition`, `CombatPlanning`, `CombatResolving`, `Reward`, `Loading`, `Paused`: field movement blocked.
- `Title`, `Dialogue`, `Choice`, `Reward`, `UIOnly`, `Paused`: UI input allowed.

Remaining gap:

- `MissionCompleteCutsceneController` still reads a direct skip action.

## 11. UI Routing Result

Runtime route is present:

`GameStateMachine -> UIScreenRouter -> GameUIRootController -> known UI roots`

Expected visibility handled by `UIScreenRouter`:

- `Exploration`: field UI
- `Dialogue` / `Cutscene`: dialogue UI
- `Choice`: choice UI
- `CombatTransition` / `CombatPlanning` / `CombatResolving`: combat UI
- `Reward`: reward UI
- `Paused`: pause UI
- `Boot` / `Loading`: loading UI

The router now has a better chance to work in existing scenes because `GameUIRootController` auto-binds known panel roots when serialized fields are missing.

Manual risk:

- Auto-binding by component type can bind the first inactive instance found. Main scenes should still receive explicit Inspector links after validation.

## 12. Legacy References Still Present

Legacy scene/prefab/asset GUID scan found no references to moved Legacy scripts.

Code compatibility remains:

- `Assets/GAME/Scripts/UI/BattleTransitionController.cs` still listens to the old `BattleTrigger2D` event and loads the battle scene directly.
- `Assets/GAME/Scripts/Legacy/Battle/SeamlessBattleManager.cs` still uses the `GameState.Combat` compatibility alias.

The Legacy Battle path is not treated as the official combat entry path. Official combat remains `CombatEntryPoint`.

Debug scene references still present:

- `Test.unity` references combat debug scripts.
- `Dungeon 1.unity` and `Demo.unity` reference `VerticalSliceSceneValidator`.

Runtime scripts do not depend on `Game.Debugging`.

## 13. DemoMission Compatibility Result

DemoMission was not migrated in this pass.

Compatibility status:

- `DemoMissionRuntime` remains in `Dungeon 1`.
- `RescueNpcActor` still uses `DemoMissionRuntime`, but now honors `GameStateMachine` exploration gating.
- `QuestRuntime`, `QuestObjectiveTracker`, and `QuestCompletionFlow` are not forced into scenes by `RuntimeBootstrapper`.
- `CombatRewardUIBinder` still updates `DemoMissionRuntime` on combat victory for compatibility.

Manual check required:

- Enemy defeat progress should increment exactly once.
- Rescue interaction should not fire during dialogue, combat, reward, loading, UI-only, or paused states.
- Mission completion should not double-complete when Quest compatibility wrappers are added later.

## 14. Required Manual Unity Inspector Checks

Run these in Unity 6000.2.6f2:

1. Open `TitleScene`, enter Play Mode, confirm initial `GameState` is `Title`.
2. Confirm the Start button still transitions to the intended dungeon scene.
3. Confirm each loaded scene has only one active `EventSystem`.
4. Open `Dungeon 1`, `Demo`, `Test`, and `InGame`; confirm no missing script warnings in Console or Inspector.
5. Confirm scene-authored `GameStateMachine` and `GameInputInstaller` duplicates are destroyed safely when persistent instances already exist.
6. Confirm `RuntimeBootstrapper` does not create duplicate `GameStateMachine`, `GameInputInstaller`, `GameFlowController`, `SceneFlowController`, `RewardService`, `GameUIRootController`, or `UIScreenRouter`.
7. Assign explicit `GameUIRootController` root references in main scenes after confirming auto-bind targets.
8. Confirm `UIScreenRouter` receives `GameStateMachine.OnStateChanged` and updates panel roots.
9. Confirm player movement works only in `Exploration`.
10. Confirm dialogue, choice, combat transition, combat planning, combat resolving, reward, loading, UI-only, and paused states block field movement.
11. Confirm `RescueNpcActor` prompt appears in range and interact works only in Exploration.
12. Confirm NPC/dialogue flow does not require direct panel activation.
13. Confirm encounter start goes through `CombatEntryPoint` or a documented compatibility route.
14. Confirm `CombatPlanningHUD` appears in `CombatPlanning`.
15. Confirm `CombatDirector` presentation and turn resolution still run.
16. Confirm `RewardService` grants combat reward once.
17. Confirm `RewardUIPanel` displays rewards and does not grant rewards itself.
18. Confirm reward close calls `GameFlowController` and returns to the correct next state.
19. Confirm DemoMission kill/rescue progress updates exactly once.
20. Confirm no debug root is required for normal gameplay.

## 15. Recommended Next Codex Task

Perform a narrow Editor-backed wiring task:

- Open the main playable scenes in Unity.
- Add or keep one explicit `RuntimeBootstrapper` per main scene.
- Explicitly link `GameUIRootController` roots.
- Replace direct scene-load buttons/panels with `SceneFlowController` compatibility methods.
- Add a real `RewardFlow` owner or document `CombatRewardUIBinder` as the temporary reward flow bridge.
- Keep DemoMission migration out of scope until scene wiring and reward/input state gates are manually verified.
