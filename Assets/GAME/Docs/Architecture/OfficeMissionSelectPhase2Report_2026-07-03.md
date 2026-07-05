# Office / MissionSelect Phase 2 Report - 2026-07-03

## 1. Summary

Phase 2 tightens the passive Office / MissionSelect backbone so it is safer to wire in an Office scene while preserving all current gameplay behavior.

The key changes are:

- `MissionSelectPanel` no longer disables its own GameObject when `root` is missing.
- `MissionSelectFlow` filters duplicate mission ids deterministically.
- `OfficeFlowController` can subscribe to `MissionSelectPanel` selections.
- `OfficeFlowController` can delay field loading after mission selection for a future Supply/Shop step.
- selected mission target scene/spawn ids can be represented in save data.

No Shop, Supply, Party management, full Calendar UI, Title replacement, combat, reward, quest, settlement, CaseFile, or `SceneTravelService` behavior was implemented or changed.

## 2. Files Changed

- `Assets/GAME/Scripts/Office/OfficeFlowController.cs`
- `Assets/GAME/Scripts/Office/MissionSelectFlow.cs`
- `Assets/GAME/Scripts/UI/MissionSelectPanel.cs`
- `Assets/GAME/Scripts/NonCombat/Save/GameSaveData.cs`
- `Assets/GAME/Docs/Architecture/OfficeMissionSelectPhase2Report_2026-07-03.md`
- `Assets/GAME/Docs/Architecture/OfficeMissionSelectPhase2Report_2026-07-03.md.meta`

## 3. Phase 1 Issues Addressed

Phase 1 follow-up points addressed:

- `MissionSelectPanel.Hide()` no longer disables the panel GameObject when `root` is null.
- duplicate mission ids from `MissionBoardDefinitionSO` and local `missionEntries` are filtered.
- field loading can now be delayed after mission selection with `waitForSupplyBeforeFieldLoad`.
- spawn point ids remain represented, but `SceneFlowController` spawn routing is explicitly not implemented yet.

## 4. OfficeFlowController Changes

`OfficeFlowController` now has an optional `MissionSelectPanel` reference.

When enabled and assigned, it subscribes to:

- `MissionSelectPanel.OnMissionSelected`

It still subscribes to:

- `MissionSelectFlow.OnMissionSelected`

New behavior:

- `OpenMissionSelectPanel()` enters `DayPhase.MissionSelect`, pulls available missions from `MissionSelectFlow`, and shows `MissionSelectPanel` when assigned.
- If no panel is assigned, it falls back to the Phase 1 event-based `MissionSelectFlow.RequestMissionSelection()` path.
- selected mission id, target field scene, and target spawn point are stored in controller state.
- repeated same-selection field loads are guarded after a field load has already started.

## 5. MissionSelectFlow Duplicate Id Behavior

`MissionSelectFlow.GetAvailableMissions(...)` now keeps a deterministic first-valid-entry-wins list.

Order remains:

1. `MissionBoardDefinitionSO.Missions`
2. local `missionEntries`

If a duplicate mission id is found, the later entry is ignored and a one-time warning is logged. Selection uses the filtered list, so duplicate ids do not throw and do not produce ambiguous results.

## 6. MissionSelectPanel Safety Behavior

`MissionSelectPanel.Hide()` now:

- clears dynamic mission rows
- hides `root` when assigned
- does not disable `gameObject` when `root` is null

This avoids disabling the subscriber object during `Awake()` and missing later `MissionSelectFlow` events.

If `subscribeToMissionSelectFlow` is enabled and `root` is missing, the panel logs a one-time warning explaining that a child root should be assigned for safe visual hiding.

## 7. MissionSelectPanel Integration Notes

`MissionSelectPanel` remains presentation-only.

It still:

- displays available missions
- raises selected mission ids
- can optionally subscribe to `MissionSelectFlow.OnMissionListReady`
- can optionally forward selected ids to `MissionSelectFlow`

It does not load scenes, enter day phases, grant rewards, mutate quests, start combat, or touch settlement flow.

Recommended setup is to assign `MissionSelectPanel` to `OfficeFlowController` and let `OfficeFlowController` delegate selection to `MissionSelectFlow`.

## 8. Field Loading / Delayed Loading Behavior

`loadMissionSceneOnSelection` remains for Phase 1 compatibility.

New field:

- `waitForSupplyBeforeFieldLoad`

Effective behavior:

- if `loadMissionSceneOnSelection` is true and `waitForSupplyBeforeFieldLoad` is false, selection immediately calls `EnterSelectedMissionField()`.
- if `waitForSupplyBeforeFieldLoad` is true, selected mission state is stored and field loading is delayed.
- a future Supply/Shop button can call `EnterSelectedMissionField()` after preparation is complete.

Supply and Shop are not implemented in this phase.

## 9. SceneFlow / Spawn Point Notes

`SceneFlowController` still supports scene-name loading only.

`OfficeFlowController.EnterSelectedMissionField()` delegates scene loading to:

- `SceneFlowController.LoadScene(string sceneName)`

If the selected mission has a `targetSpawnPointId`, `OfficeFlowController` logs a one-time warning that spawn routing is not supported by `SceneFlowController` yet. Existing `SceneTravelService` behavior is preserved and was not changed.

## 10. SaveLoad Compatibility Notes

`FutureDailySaveData` was extended additively with:

- `selectedMissionTargetFieldSceneName`
- `selectedMissionTargetSpawnPointId`

Existing field remains:

- `selectedMissionId`

`OfficeFlowController` captures/restores these values only. It does not implement full mission session persistence, mission board persistence, scene restore, or save slot/load UI.

## 11. Title / Load Integration Notes

`TitleSceneController` was not changed.

Current Title behavior remains:

- title request flow
- direct dungeon scene load through `SceneFlowController` when available
- fallback direct scene load
- DemoMission progress reset

Future Title/Load integration should route into Office by scene selection or explicit OfficeRoot bootstrapping after Unity Play Mode validation.

## 12. Inspector Wiring Notes

No existing serialized fields were renamed or removed.

New optional `OfficeFlowController` fields:

- `missionSelectPanel`
- `waitForSupplyBeforeFieldLoad`

Existing relevant fields remain:

- `dailyFlowController`
- `missionSelectFlow`
- `sceneFlowController`
- `enterOfficeOnEnable`
- `loadMissionSceneOnSelection`

Recommended future hierarchy, documentation only:

```text
OfficeRoot
- OfficeFlowController
- MissionSelectFlow
- CalendarService
- DailyFlowController

GameUIRoot
- MissionSelectPanel
- DaySettlementPanel
- CalendarWidget
- MorningBriefingPanel

Future:
OfficeRoot
- ShopFlow
- SupplyLoadoutFlow
- PartyManagementFlow
```

For `MissionSelectPanel`, assign a child `root` GameObject when using flow subscription. This lets the panel hide visuals without disabling its own event subscriber GameObject.

## 13. Current Behavior Preserved

Preserved behavior:

- existing scenes still work without `OfficeRoot`
- Title flow was not replaced
- CaseFile and `SceneTravelService` behavior was not changed
- combat behavior was not changed
- reward behavior was not changed
- quest behavior was not changed
- settlement behavior was not changed
- no Shop, Supply, Party, or full Calendar UI was added

## 14. Phase 3 Plan

1. Wire an Office scene with `OfficeRoot`, `MissionSelectFlow`, and `MissionSelectPanel`.
2. Validate panel lifecycle and dynamic rows in Unity Play Mode.
3. Decide whether MissionSelectPanel should be routed through `GameUIRootController` or remain Office-local.
4. Add a Supply/Loadout placeholder that calls `EnterSelectedMissionField()` after mission selection.
5. Add spawn point support to `SceneFlowController`, or formally route Office mission entry through `SceneTravelService`.
6. Add mission start integration with `QuestRuntime` only after mission data ownership is finalized.
7. Add Shop and Party flows as separate phases after Office/MissionSelect is scene-tested.

## 15. Compile Validation Result

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

## 16. Unity Play Mode Risks

- `MissionSelectPanel` still needs Canvas/layout validation.
- Dynamic button creation is minimal and may need prefab styling.
- Scene wiring has not been validated with an actual `OfficeRoot`.
- duplicate mission id filtering is id-string based and depends on stable mission ids.
- spawn point ids are captured but not consumed by `SceneFlowController`.
- delayed field loading stores selected mission state only, not a full mission session.
