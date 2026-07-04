# Daily Settlement Phase 1 Report - 2026-07-03

## 1. Summary

Phase 1 adds a minimal Daily / Calendar / DaySettlement backbone without changing current gameplay behavior or requiring scene rewiring.

The new runtime owners are passive unless called or wired:

- `CalendarService` owns day/date/phase data.
- `DailyFlowController` owns explicit day-phase transitions.
- `DaySettlementFlow` owns post-mission settlement request preparation.

No full calendar UI, office loop, mission select UI, shop, supply, settlement UI, or day simulation was implemented.

## 2. Files Changed

- `Assets/GAME/Scripts/Daily.meta`
- `Assets/GAME/Scripts/Daily/DayPhase.cs`
- `Assets/GAME/Scripts/Daily/DayPhase.cs.meta`
- `Assets/GAME/Scripts/Daily/CalendarService.cs`
- `Assets/GAME/Scripts/Daily/CalendarService.cs.meta`
- `Assets/GAME/Scripts/Daily/DailyFlowController.cs`
- `Assets/GAME/Scripts/Daily/DailyFlowController.cs.meta`
- `Assets/GAME/Scripts/Daily/DaySettlementRequest.cs`
- `Assets/GAME/Scripts/Daily/DaySettlementRequest.cs.meta`
- `Assets/GAME/Scripts/Daily/DaySettlementResult.cs`
- `Assets/GAME/Scripts/Daily/DaySettlementResult.cs.meta`
- `Assets/GAME/Scripts/Daily/DaySettlementFlow.cs`
- `Assets/GAME/Scripts/Daily/DaySettlementFlow.cs.meta`
- `Assets/GAME/Scripts/Quest/QuestCompletionFlow.cs`
- `Assets/GAME/Scripts/DemoMission/Runtime/MissionCompletionController.cs`
- `Assets/GAME/Scripts/NonCombat/Save/GameSaveData.cs`
- `Assets/GAME/Docs/Architecture/DailySettlementPhase1Report_2026-07-03.md`
- `Assets/GAME/Docs/Architecture/DailySettlementPhase1Report_2026-07-03.md.meta`

## 3. Existing Daily / Settlement Code Found

No existing Daily, Calendar, Settlement, or DayPhase runtime code was found under `Assets/GAME/Scripts`.

Existing architecture reports identified these systems as missing:

- `DailyFlowController`
- `CalendarService`
- `DaySettlementFlow`

SaveLoad Phase 1 already provided a placeholder `FutureDailySaveData`.

## 4. New Daily / Calendar / Settlement Backbone

Added `Assets/GAME/Scripts/Daily` with:

- `DayPhase`
- `CalendarService`
- `DailyFlowController`
- `DaySettlementRequest`
- `DaySettlementResult`
- `DaySettlementFlow`

These types are compile-time/runtime backbone only. They do not create UI, load scenes, start missions, grant rewards, or advance gameplay automatically.

## 5. DayPhase Ownership

`DayPhase` values:

- `None`
- `Morning`
- `Office`
- `MissionSelect`
- `FieldExploration`
- `Combat`
- `Reward`
- `Settlement`
- `Rest`
- `NightEvent`

`CalendarService` owns the current `DayPhase`.

## 6. CalendarService Behavior

`CalendarService` owns:

- current day
- current week
- current chapter placeholder
- current `DayPhase`

It exposes:

- `SetPhase(DayPhase nextPhase)`
- `AdvanceDay()`
- `SetChapter(string chapterId)`
- `OnDayPhaseChanged`
- `OnDayAdvanced`

It implements SaveLoad Phase 1 hooks:

- `ISaveDataProvider`
- `ISaveDataConsumer`

Saved data lands in `GameSaveData.futureDaily`.

## 7. DailyFlowController Behavior

`DailyFlowController` owns explicit phase transition methods:

- `BeginNewDay()`
- `EnterOffice()`
- `EnterMission()`
- `EnterSettlement()`
- `CompleteSettlement()`

It forwards phase changes through `OnDayPhaseChanged`.

It does not manipulate `GameState`, scenes, UI, combat, rewards, or quests.

## 8. DaySettlementFlow Behavior

`DaySettlementFlow` owns settlement preparation:

- accepts `DaySettlementRequest`
- emits `OnSettlementReady`
- completes active settlement through `CompleteSettlement()`
- emits `OnSettlementCompleted`
- blocks duplicate active/completed settlement ids
- logs invalid requests once

`DaySettlementRequest` carries:

- settlement id
- quest id
- mission id
- reward summary copied from `RewardGrantResult` when available

`DaySettlementFlow` implements SaveLoad Phase 1 hooks for `completedSettlementIds`.

## 9. Quest / Mission Completion Integration

`QuestCompletionFlow` now has optional fields:

- `daySettlementFlow`
- `notifyDaySettlementOnQuestCompletion`

When enabled, quest completion can prepare a settlement request with the quest id and `RewardGrantResult`. The default is disabled, so current behavior is preserved.

`MissionCompletionController` now has optional fields:

- `daySettlementFlow`
- `notifyDaySettlementOnMissionComplete`

When enabled, mission completion can prepare a settlement request with the DemoMission-compatible mission id. The default is disabled, so current mission completion UI behavior is preserved.

## 10. RewardService Integration Notes

`RewardService` remains the reward application owner.

Daily/Settlement Phase 1 does not grant or re-grant rewards. Settlement requests only carry a reward summary copied from `RewardGrantResult` for future presentation.

Combat reward behavior was not changed.

## 11. SaveLoad Phase 1 Compatibility

`FutureDailySaveData` was extended additively with:

- `weekIndex`
- `currentChapterId`
- `currentDayPhase`
- `completedSettlementIds`

Existing fields remain:

- `dayIndex`
- `calendarDateId`
- `completedDailyActionIds`

No save file path, save UI, slot system, or cloud save behavior was added.

## 12. Inspector Wiring Notes

No existing serialized fields were renamed or removed.

New optional serialized fields:

- `DailyFlowController.calendarService`
- `DailyFlowController.advanceDayWhenBeginningNewDay`
- `DaySettlementFlow.dailyFlowController`
- `DaySettlementFlow.calendarService`
- `DaySettlementFlow.enterSettlementPhaseOnRequest`
- `QuestCompletionFlow.daySettlementFlow`
- `QuestCompletionFlow.notifyDaySettlementOnQuestCompletion`
- `MissionCompletionController.daySettlementFlow`
- `MissionCompletionController.notifyDaySettlementOnMissionComplete`

Existing scenes continue working without a `DailyRoot` because the new hooks are optional and default disabled.

Recommended future hierarchy, not required in this phase:

```text
DailyRoot
- CalendarService
- DailyFlowController
- DaySettlementFlow
```

Optional future UI:

```text
GameUIRoot
- DaySettlementPanel
- CalendarWidget
- MorningBriefingPanel
```

## 13. Current Behavior Preserved

Preserved behavior:

- combat reward route remains unchanged
- QuestRuntime progress behavior remains unchanged
- DemoMission compatibility remains active
- Mission completion panel behavior remains unchanged by default
- no current scene flow changes were made
- no UI routing changes were made
- no shop, supply, mission select, calendar UI, or office loop was added

## 14. Phase 2 Plan

1. Add a non-invasive `DailyRoot` prefab or scene object after Play Mode validation.
2. Add a settlement panel that listens to `DaySettlementFlow.OnSettlementReady`.
3. Decide when quest/mission completion should enable settlement notifications by default.
4. Add clear mapping between `GameState` and `DayPhase` only after current UI routing is validated.
5. Add calendar UI widgets after day state is confirmed stable.
6. Add mission select / office loop separately from settlement.
7. Add shop/supply systems only after inventory and reward ownership are stable.
8. Extend save/load once actual daily gameplay state exists.

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

This matches the known Unity-generated project behavior in this repository: the available `dotnet` check reports no C# compile warnings or errors but exits nonzero.

Unity is not available on PATH in this shell, so Unity Play Mode validation was not run.

## 16. Unity Play Mode Risks

- New Daily components are not wired into scenes yet.
- Optional Quest/Mission settlement notifications are default disabled and need Inspector validation before use.
- `CalendarService` and `DaySettlementFlow` SaveLoad hooks have not been scene-tested.
- No settlement UI exists yet, so settlement events are currently headless.
- Future phase mapping between `GameState` and `DayPhase` must avoid double-owning combat/reward UI flow.
