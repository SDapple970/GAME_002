# SaveLoad Phase 1 Report - 2026-07-03

## 1. Summary

Phase 1 defines a minimal, versioned save data contract for current production runtime systems while leaving the existing file save path intact.

`SaveLoadService` remains the production coordinator. Existing `SaveManager.Save()` / `Load()` behavior is preserved for compatibility. No save UI, slots, cloud save, Daily/Calendar flow, or scene rewiring was added.

No ScriptableObject definitions are saved directly. New save data stores IDs and runtime values only.

## 2. Files Changed

- `Assets/GAME/Scripts/Core/SaveLoadService.cs`
- `Assets/GAME/Scripts/NonCombat/Save/GameSaveData.cs`
- `Assets/GAME/Scripts/NonCombat/Save/GameSaveData.cs.meta`
- `Assets/GAME/Scripts/NonCombat/Save/SaveContracts.cs`
- `Assets/GAME/Scripts/NonCombat/Save/SaveContracts.cs.meta`
- `Assets/GAME/Scripts/NonCombat/Save/SaveSerializer.cs`
- `Assets/GAME/Scripts/Quest/QuestRuntime.cs`
- `Assets/GAME/Scripts/NonCombat/Inventory/CurrencyWallet.cs`
- `Assets/GAME/Scripts/NonCombat/Inventory/InventoryService.cs`
- `Assets/GAME/Scripts/DemoMission/Runtime/DemoMissionRuntime.cs`
- `Assets/GAME/Docs/Architecture/SaveLoadPhase1Report_2026-07-03.md`
- `Assets/GAME/Docs/Architecture/SaveLoadPhase1Report_2026-07-03.md.meta`

## 3. Existing Save/Load Code Found

Existing production wrapper:

- `Assets/GAME/Scripts/Core/SaveLoadService.cs`

Existing compatibility implementation:

- `Assets/GAME/Scripts/NonCombat/Save/SaveData.cs`
- `Assets/GAME/Scripts/NonCombat/Save/SaveManager.cs`
- `Assets/GAME/Scripts/NonCombat/Save/SaveSerializer.cs`

Current behavior preserved:

- `SaveLoadService.Save()` still blocks unsafe game states and delegates to `SaveManager.Save()`.
- `SaveLoadService.Load()` still delegates to `SaveManager.Load()`.
- `SaveManager` still writes `game_save.json` in `Application.persistentDataPath`.
- `SaveManager` still has debug `F5` / `F6` hotkeys.

## 4. New Save Data Models

Added `GameSaveData` as the future root contract:

- `SaveHeaderData`
- `QuestSaveData`
- `InventorySaveData`
- `CurrencySaveData`
- `PartySaveData`
- `ProgressionSaveData`
- `DemoMissionSaveData`
- `FutureDailySaveData`

Added simple coordination interfaces:

- `ISaveDataProvider`
- `ISaveDataConsumer`

Added serializer support:

- `SaveSerializer.ToJson(GameSaveData data)`
- `SaveSerializer.FromGameSaveJson(string json)`

The existing `SaveData` model and `SaveSerializer.ToJson(SaveData)` / `FromJson(...)` path remain unchanged.

## 5. Quest Save Plan

`QuestRuntime` now implements `ISaveDataProvider` and `ISaveDataConsumer`.

Saved values:

- quest id
- completion state
- objective id
- objective progress
- objective required count

Quest definitions and objective definitions are not serialized. They remain ScriptableObject/static data and are resolved by id at runtime.

## 6. Reward / Inventory / Currency Save Plan

`RewardService` is not saved directly because it is an application owner, not durable state.

Durable reward-related state is represented by:

- `CurrencySaveData.gold`
- `InventorySaveData.items`

Runtime hooks added:

- `CurrencyWallet.CaptureSaveData(...)`
- `CurrencyWallet.RestoreSaveData(...)`
- `InventoryService.CaptureSaveData(...)`
- `InventoryService.RestoreSaveData(...)`

The existing `RewardService` grant route is unchanged.

## 7. Party / Progression Save Plan

`PartySaveData` exists as a placeholder contract:

- `memberIds`
- `memberLevels`

No `Assets/GAME/Scripts/Party` folder or `PartyRuntime` was found, so no party runtime hook was added.

`ProgressionSaveData` exists as a placeholder contract:

- `personaStats`
- `completedObjectiveIds`

The existing legacy `SaveManager` already saves and restores `PersonaStatusManager` data. `PersonaStatusManager.cs` was not edited in this phase because the file is not UTF-8 clean in this checkout, and rewriting it would be unnecessary risk for a save contract phase.

## 8. DemoMission Compatibility Notes

`DemoMissionRuntime` remains a compatibility layer and now implements `ISaveDataProvider` only.

Captured adapter values:

- current mission id
- enemy defeat count
- NPC rescued flag
- completion flag

No restore hook was added for DemoMission because restoring `DemoMissionDefinitionSO` safely requires an id-to-definition registry that does not exist yet. QuestRuntime remains the preferred future owner of mission progress.

## 9. Future Daily / Calendar Save Hooks

`FutureDailySaveData` is a placeholder only.

Fields:

- `dayIndex`
- `calendarDateId`
- `completedDailyActionIds`

No DailyFlow, CalendarService, or DaySettlementFlow implementation was added.

## 10. Inspector / Scene Wiring Notes

No existing serialized fields were renamed or removed.

No scene rewiring is required. `SaveLoadService` discovers save providers/consumers with `FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)` when its new snapshot methods are called.

Existing scenes using `SaveLoadService.Save()` / `Load()` continue to use the legacy `SaveManager` path.

## 11. Compile Validation Result

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

This matches the known Unity-generated project behavior in this repository: the available `dotnet` check reports no C# compile warnings or errors but exits nonzero.

Unity is not available on PATH in this shell, so Unity Play Mode validation was not run.

## 12. Unity Play Mode Risks

- `SaveLoadService.CaptureGameSaveDataSnapshot()` has not been scene-tested.
- Provider discovery depends on runtime objects existing in the loaded scene or persistent roots.
- `QuestRuntime.RestoreSaveData(...)` restores runtime progress but does not raise UI refresh events yet.
- DemoMission restore is intentionally not implemented.
- Party and future Daily data are contract placeholders only.
- The legacy `SaveManager` still owns actual disk save/load in this phase.

## 13. Phase 2 Plan

1. Add a file persistence path for `GameSaveData` behind `SaveLoadService`.
2. Decide whether to migrate or wrap legacy `SaveManager` debug hotkeys under Debugging.
3. Add a stable id registry for mission/quest definitions if DemoMission restore is still required.
4. Add PartyRuntime provider/consumer after PartyRuntime exists.
5. Add real progression provider/consumer once the progression owner is UTF-8 clean or migrated.
6. Add Daily/Calendar provider/consumer only after DailyFlow/CalendarService exist.
7. Add save schema migration handling before changing schema version beyond `1`.
8. Add UI and save slots only after the root contract is validated in Unity Play Mode.
