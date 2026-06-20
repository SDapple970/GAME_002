# Post-Compile Scene Wiring Report

Date: 2026-06-20

## 1. Git Status Before And After

Before:

```text
clean
```

After:

```text
M  Assets/GAME/Scenes/Demo.unity
M  Assets/GAME/Scenes/Dungeon 1.unity
M  Assets/GAME/Scenes/InGame.unity
M  Assets/GAME/Scenes/Test.unity
M  Assets/GAME/Scenes/TitleScene.unity
M  Assets/GAME/Scripts/Title/Runtime/TitleSceneController.cs
M  Assets/GAME/Scripts/UI/GameUIRootController.cs
M  Assets/GAME/Scripts/UI/UIScreenRouter.cs
A  Assets/GAME/Docs/Architecture/PostCompileSceneWiringReport.md
A  Assets/GAME/Docs/Architecture/PostCompileSceneWiringReport.md.meta
```

An empty `Assets/.tmp.driveupload/` directory appeared transiently during file IO, contained no `.meta` files, and was not present in final status.

## 2. Unity Compile / Static Validation Result

Unity batchmode was attempted with Unity 6000.2.6f2, but it could not open the project because another Unity instance already has the project open.

Static compile was run through Unity's Bee `Assembly-CSharp.rsp` using Unity's bundled Mono/Roslyn compiler. Result: exit code `0`, no C# errors.

Remaining static warnings:

- Standalone Unity source-generator analyzer host warnings from running Roslyn outside the full Unity editor host.
- `TimedChoiceDialoguePanel.cs`: obsolete `TMP_Text.enableWordWrapping`.
- `RescueNpcActor.cs`: `interactKey` assigned but not used.

## 3. Scenes Inspected

Main scenes inspected:

- `TitleScene`
- `Dungeon 1`
- `InGame`
- `Demo`
- `Test`

Secondary scenes inspected but not modified:

- `TutorialScene`
- `Dungeon 2`
- `Dungeon 3`
- `Dungeon 4`
- `Dungeon 5`

No missing script markers were found in scenes, prefabs, or assets by static YAML scan.

## 4. Scenes Modified

Modified:

- `Assets/GAME/Scenes/TitleScene.unity`
- `Assets/GAME/Scenes/Dungeon 1.unity`
- `Assets/GAME/Scenes/InGame.unity`
- `Assets/GAME/Scenes/Demo.unity`
- `Assets/GAME/Scenes/Test.unity`

Not modified:

- `TutorialScene`
- `Dungeon 2`
- `Dungeon 3`
- `Dungeon 4`
- `Dungeon 5`

## 5. Components Added

`TitleScene`:

- Added a new top-level `Systems` GameObject.
- Added `RuntimeBootstrapper`, `GameStateMachine`, `GameInputInstaller`, `GameFlowController`, `SceneFlowController`, `SaveLoadService`, `RewardService`, `GameUIRootController`, and `UIScreenRouter`.

`Dungeon 1`, `InGame`, `Demo`, and `Test`:

- Added missing production owners to each scene's existing `Systems` GameObject:
  - `RuntimeBootstrapper`
  - `GameFlowController`
  - `SceneFlowController`
  - `SaveLoadService`
  - `RewardService`
  - `GameUIRootController`
  - `UIScreenRouter`

No duplicate production owner was added where one already existed.

## 6. References Wired

All modified scenes:

- `UIScreenRouter.uiRoot` was wired to the scene `GameUIRootController`.
- `UIScreenRouter.stateMachine` was wired to the scene `GameStateMachine`.

`TitleScene`:

- `GameUIRootController.titleRoot` -> `Canvas`
- `GameUIRootController.loadingRoot` -> `FadePanel`

`Dungeon 1`:

- `GameUIRootController.dialogueRoot` -> `DemodialoguePanel`
- `GameUIRootController.choiceRoot` -> `DemoChoicePanel`
- `GameUIRootController.combatRoot` -> `CombatHUD`
- `GameUIRootController.rewardRoot` -> `RewardUI`
- `CombatRewardUIBinder.rewardService` -> scene `RewardService`

`InGame`:

- `GameUIRootController.rewardRoot` -> `RewardUIPanelController`
- Other UI roots left null because no unambiguous field/dialogue/combat UI root was present.

`Demo`:

- `GameUIRootController.combatRoot` -> `CombatHUD`
- `GameUIRootController.rewardRoot` -> `RewardPanel`

`Test`:

- `GameUIRootController.fieldRoot` -> `Canvas_Overworld`
- `GameUIRootController.dialogueRoot` -> `DialoguePanel`
- `GameUIRootController.choiceRoot` -> `TimedChoicePanel`
- `GameUIRootController.combatRoot` -> `Canvas_Combat`
- `GameUIRootController.rewardRoot` -> `Canvas_Reward`

## 7. Runtime Auto-Discovery Still Required

Still intentionally relies on runtime discovery:

- `InputService` and `InputRouter` are not MonoBehaviours. They are constructed by `GameInputInstaller`.
- `CombatStateMachine` is a plain runtime class, not a scene component.
- `CombatTurnResolver` is static, not a scene component.
- `SaveLoadService.saveManager` remains null because Save/Load is not being implemented in this task.
- `RewardService.currencyWallet` remains null and resolves `CurrencyWallet.Instance` at grant time.
- `InGame` UI roots other than reward remain null because no safe single UI target was available.

## 8. UI Routing Status

`GameUIRootController` now supports a `titleRoot`.

`UIScreenRouter` routes:

- `Title` -> title UI
- `Exploration` -> field UI
- `Dialogue` / `Cutscene` -> dialogue UI
- `Choice` -> choice UI
- `CombatTransition`, `CombatPlanning`, `CombatResolving` -> combat UI
- `Reward` -> reward UI
- `Paused` -> pause UI if present
- `Loading` / `Boot` -> loading UI if present

Known routing limitations:

- `InGame` has only reward UI explicitly wired.
- `Dungeon 1` has no explicit field HUD root.
- `Demo` has combat and reward roots only.
- Existing direct `SetActive` calls remain and are documented below.

## 9. Input Routing Status

`GameInputInstaller` is explicitly present in every main scene after this pass.

`InputRouter` is constructed by `GameInputInstaller` and checks `GameStateMachine`:

- `Exploration` allows field movement and interaction.
- `Dialogue`, `Choice`, `CombatTransition`, `CombatPlanning`, `CombatResolving`, `Reward`, `Paused`, and `UIOnly` block exploration movement.
- UI input remains allowed in title/dialogue/choice/reward/UI-only/paused states.

No broad input rewrite was performed.

## 10. Reward Flow Status

Current production reward path:

```text
CombatEntryPoint
-> CombatRewardUIBinder
-> RewardService
-> RewardUIPanel
-> GameFlowController
```

`Dungeon 1` has `CombatRewardUIBinder.rewardService` explicitly linked to the scene `RewardService`.

`RewardService` grants combat result rewards. `RewardUIPanel` displays and closes only; it does not grant rewards again. `RewardUIPanel.OnClosed` leads through `CombatRewardUIBinder` to `GameFlowController.HandleRewardClosed()` when available.

No `RewardApplier` scene usage was found in the inspected main scene inventory.

## 11. Direct SceneManager.LoadScene Calls Remaining

Remaining direct scene loads are documented for later conversion:

- `Assets/GAME/Scripts/UI/DemoEndController.cs`
- `Assets/GAME/Scripts/UI/BattleTransitionController.cs`
- `Assets/GAME/Scripts/DemoMission/UI/MissionCompletePanel.cs`
- `Assets/GAME/Scripts/DemoMission/UI/CaseFileAcceptController.cs`
- `Assets/GAME/Scripts/Title/Runtime/TitleSceneController.cs` fallback only
- `Assets/GAME/Scripts/DemoMission/Runtime/Ending/DemoRescueNpcEndFlow.cs`
- `Assets/GAME/Scripts/DemoMission/Runtime/Ending/DemoEndPanelController.cs`
- `Assets/GAME/Scripts/Core/SceneFlowController.cs` owner path
- `Assets/GAME/Scripts/Story/SceneTravelService.cs` existing story travel path

Low-risk conversion done:

- `TitleSceneController` now uses `SceneFlowController.Instance.LoadScene(sceneName)` when available, with `SceneManager.LoadScene` kept as compatibility fallback.

## 12. Direct InputActionReference Usage Remaining

Remaining direct `InputActionReference` usage:

- `MissionCompleteCutsceneController`
- `OverworldInputAdapter`
- `CombatStartSmokeTest`
- `CombatFieldCallDebug`

Debug-only uses were left unchanged. `OverworldInputAdapter` remains a compatibility bridge and was not rewritten in this task.

## 13. Direct SetActive Ownership Conflicts Remaining

Direct `SetActive` calls remain in many UI and compatibility scripts. Potential ownership overlaps to review later:

- `CombatUIRootController` directly toggles combat/overworld/reward canvases while `UIScreenRouter` now owns major UI visibility.
- `RewardUIPanel` directly toggles its root while `UIScreenRouter` can toggle the reward root.
- `MissionCompleteCutsceneController` directly toggles video and reward panel roots.
- `DemoRescueNpcEndFlow`, `DemoEndPanelController`, and `DemoEndController` directly toggle demo ending UI.
- Story/search/dialogue panels still directly toggle their own local roots.

No direct `SetActive` calls were removed in this task.

## 14. DemoMission Compatibility Status

`DemoMissionRuntime` remains intact and unmigrated.

`Dungeon 1` still contains `DemoMissionRuntime` and the combat reward binder still registers enemy defeat through `DemoMissionRuntime.GetOrCreate()`. This is compatibility behavior and was not changed.

## 15. Legacy Battle Compatibility Status

Legacy Battle code remains untouched. Direct legacy UI toggles such as `Legacy/Battle/SeamlessBattleManager` are documented but not modified.

## 16. Manual Unity Play Mode Checklist

Run after closing duplicate Unity instances:

1. Open `TitleScene`.
2. Confirm Console has no compile errors or missing script warnings.
3. Press Start and confirm transition uses the configured dungeon scene.
4. Open `Dungeon 1` and enter Play Mode.
5. Confirm field movement works only in `Exploration`.
6. Trigger dialogue/choice and confirm movement is blocked.
7. Start combat through the existing combat entry path.
8. Confirm combat UI appears in combat states.
9. Win combat and confirm `RewardService` grants once and `RewardUIPanel` displays.
10. Close reward UI and confirm state returns through `GameFlowController`.

## 17. Recommended Next Task

Run a Unity Editor validation pass with the project closed in other Unity instances, then address UI visibility ownership conflicts separately. Do not start DemoMission migration, FieldEnemy splitting, Legacy Battle removal, or Save/Load implementation until this scene wiring baseline is manually verified.
