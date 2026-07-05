# Office / MissionSelect Phase 1 Report - 2026-07-03

## 1. Summary

Phase 1 adds a minimal Office / MissionSelect backbone without changing current gameplay flow or requiring scene rewiring.

The new runtime owners are passive unless a scene explicitly wires them:

- `OfficeFlowController` owns Office hub flow.
- `MissionSelectFlow` owns mission selection logic.
- `MissionSelectPanel` displays mission choices only.
- `SceneFlowController` remains the scene loading owner.
- `DailyFlowController` remains the day phase transition owner.

No shop, supply, party management, full calendar UI, title scene replacement, combat behavior, reward behavior, quest behavior, or settlement behavior was implemented or changed.

## 2. Files Changed

- `Assets/GAME/Scripts/Daily/DailyFlowController.cs`
- `Assets/GAME/Scripts/NonCombat/Save/GameSaveData.cs`
- `Assets/GAME/Scripts/Office/MissionEntry.cs`
- `Assets/GAME/Scripts/Office/MissionEntry.cs.meta`
- `Assets/GAME/Scripts/Office/MissionSelectRequest.cs`
- `Assets/GAME/Scripts/Office/MissionSelectRequest.cs.meta`
- `Assets/GAME/Scripts/Office/MissionSelectResult.cs`
- `Assets/GAME/Scripts/Office/MissionSelectResult.cs.meta`
- `Assets/GAME/Scripts/Office/MissionBoardDefinitionSO.cs`
- `Assets/GAME/Scripts/Office/MissionBoardDefinitionSO.cs.meta`
- `Assets/GAME/Scripts/Office/MissionSelectFlow.cs`
- `Assets/GAME/Scripts/Office/MissionSelectFlow.cs.meta`
- `Assets/GAME/Scripts/Office/OfficeFlowController.cs`
- `Assets/GAME/Scripts/Office/OfficeFlowController.cs.meta`
- `Assets/GAME/Scripts/UI/MissionSelectPanel.cs`
- `Assets/GAME/Scripts/UI/MissionSelectPanel.cs.meta`
- `Assets/GAME/Docs/Architecture/OfficeMissionSelectPhase1Report_2026-07-03.md`
- `Assets/GAME/Docs/Architecture/OfficeMissionSelectPhase1Report_2026-07-03.md.meta`

## 3. Existing Office / MissionSelect Code Found

Existing Office code:

- `OfficeMenuController`
- `OfficeHotspot2D`
- `CaseFileDocumentPanel`

Existing related content/data:

- `CaseFileDataSO`
- `CaseBoard`
- `QuestDefinitionSO`
- `SceneTravelService`
- `SceneFlowController`

No existing `OfficeFlowController`, `MissionSelectFlow`, `MissionBoardDefinitionSO`, `MissionSelectRequest`, `MissionSelectResult`, or presentation-only `MissionSelectPanel` was found.

The `Assets/GAME/Scripts/World` folder referenced in the task does not exist in this checkout.

## 4. New OfficeFlowController Behavior

`OfficeFlowController` was added under `Game.Office`.

Responsibilities:

- enter Office phase through `DailyFlowController.EnterOffice()`
- request mission selection through `MissionSelectFlow`
- receive `MissionSelectResult`
- record the selected mission id
- enter field exploration phase through `DailyFlowController.EnterMission()`
- optionally ask `SceneFlowController` to load the selected mission field scene

Scene loading is guarded by:

- selected mission validity
- target field scene presence
- optional `SceneFlowController` availability

The controller logs once for missing `MissionSelectFlow`, invalid mission id, missing target field scene, and missing optional `SceneFlowController`.

## 5. New MissionSelectFlow Behavior

`MissionSelectFlow` was added under `Game.Office`.

Responsibilities:

- expose available mission entries from `MissionBoardDefinitionSO`
- expose additional locally serialized mission entries
- filter locked/empty-id entries by default
- request mission selection and publish `OnMissionListReady`
- select a mission by id
- produce `MissionSelectResult`
- publish `OnMissionSelected`

It does not load scenes, start combat, grant rewards, mutate quest progress, or show UI directly.

## 6. Mission Data Source

Mission data is represented by `MissionEntry`.

`MissionEntry` can reference:

- `QuestDefinitionSO` for quest id/title/description
- `CaseFileDataSO` for title/description/unlocked state/target scene/spawn
- manually serialized id/title/description/target field scene/spawn values

`MissionBoardDefinitionSO` is a lightweight ScriptableObject list of `MissionEntry` values.

DemoMission data was not converted in this phase.

## 7. MissionSelectPanel Behavior

`MissionSelectPanel` was added under `Assets/GAME/Scripts/UI`.

It is presentation-only:

- displays available missions
- creates simple selectable mission rows
- raises `OnMissionSelected(string missionId)`
- exposes `OnClosed`
- can optionally subscribe to `MissionSelectFlow.OnMissionListReady`
- can optionally forward selected ids to `MissionSelectFlow`

It does not load scenes directly and does not mutate mission, quest, reward, combat, or settlement state.

## 8. DailyFlowController Integration

`DailyFlowController` already had:

- `EnterOffice()`
- `EnterMission()`

Phase 1 additively added:

- `EnterMissionSelect()`

This sets `DayPhase.MissionSelect` through the existing `CalendarService` path. Existing scenes are not forced to call it.

## 9. SceneFlow Integration

`SceneFlowController.LoadScene(string sceneName)` already exists and remains the scene loading owner for the new Office flow.

`OfficeFlowController` delegates mission field loading to `SceneFlowController` when `loadMissionSceneOnSelection` is enabled. If no `SceneFlowController` is present, it logs and does not fall back to direct scene loading.

Spawn point handling is not implemented here because `SceneFlowController` does not currently own spawn ids. Existing `SceneTravelService` still owns its current spawn behavior for older case-file/story paths.

## 10. SaveLoad Compatibility Notes

`FutureDailySaveData` was extended additively with:

- `selectedMissionId`

`OfficeFlowController` implements `ISaveDataProvider` and `ISaveDataConsumer` to capture/restore that selected id only.

No full save/load execution path, slots, load UI, mission board persistence, or scene restore behavior was added.

## 11. Inspector Wiring Notes

No existing serialized fields were renamed or removed.

New optional `OfficeFlowController` fields:

- `dailyFlowController`
- `missionSelectFlow`
- `sceneFlowController`
- `enterOfficeOnEnable`
- `loadMissionSceneOnSelection`

New optional `MissionSelectFlow` fields:

- `missionBoard`
- `missionEntries`

New optional `MissionSelectPanel` fields:

- `root`
- `missionRowRoot`
- `missionButtonTemplate`
- `titleText`
- `emptyText`
- `closeButton`
- `missionSelectFlow`
- `subscribeToMissionSelectFlow`
- `forwardSelectionToFlow`

Suggested future hierarchy, documentation only:

```text
OfficeRoot
- OfficeFlowController
- MissionSelectFlow

GameUIRoot
- MissionSelectPanel
- CalendarWidget
- MorningBriefingPanel

Future:
OfficeRoot
- ShopFlow
- SupplyLoadoutFlow
- PartyManagementFlow
```

`UIScreenRouter` was not changed. Later routing should connect MissionSelect UI through Daily/Office ownership or an explicit UI root extension, not by rewriting current `GameState` routing in this phase.

## 12. Current Behavior Preserved

Preserved behavior:

- Title scene flow was not replaced.
- Existing `OfficeMenuController` and `CaseFileDocumentPanel` behavior remains unchanged.
- Existing `SceneTravelService` story/case-file travel remains unchanged.
- Combat entry, rewards, quests, settlement, DemoMission compatibility, and UI routing remain unchanged.
- Existing scenes continue working without an `OfficeRoot`, `MissionSelectFlow`, or `MissionSelectPanel`.
- Shop, supply, party management, and full calendar UI were not implemented.

## 13. Phase 2 Plan

1. Wire `OfficeRoot` in a test scene and validate lifecycle in Unity Play Mode.
2. Build a MissionBoardDefinition asset from current case-file or quest content.
3. Decide whether Title/Load should enter Office by scene load or by DailyFlow phase.
4. Add a proper UI root slot for MissionSelectPanel after UI routing ownership is agreed.
5. Add spawn point support to `SceneFlowController` or intentionally route mission entry through `SceneTravelService`.
6. Add mission start hooks into `QuestRuntime` only after mission data ownership is finalized.
7. Add Shop/Supply/Party flows as separate phases after Office/MissionSelect is validated.

## 14. Compile Validation Result

Command run:

```text
dotnet build Assembly-CSharp.csproj --no-restore --nologo
```

Result:

```text
Exit code: 1
0 warnings
0 errors
```

This matches the known Unity-generated project behavior documented in earlier reports: the available `dotnet` check reports no C# compile warnings or errors but exits nonzero.

Unity is not available on PATH in this shell, so Unity Play Mode validation was not run.

## 15. Unity Play Mode Risks

- New Office/MissionSelect components have not been scene-wired or Play Mode validated.
- `MissionSelectPanel` dynamic row creation needs Canvas/layout inspection.
- `MissionEntry` content requires stable ids and target field scenes before production use.
- `SceneFlowController` does not currently support spawn point ids.
- `selectedMissionId` restore only restores the id, not a full mission selection/session.
