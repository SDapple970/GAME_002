# Daily Settlement Phase 2 Report - 2026-07-03

## 1. Summary

Phase 2 keeps the Daily / DaySettlement work passive and conservative while making settlement data more explicit.

`DaySettlementFlow` can now prepare settlement data with quest/mission ids, display titles, reward grant summaries, source type, and calendar snapshots. Quest and DemoMission completion hooks remain optional and default disabled. A new `DaySettlementPanel` is presentation-only and is not required by existing scenes.

No current combat reward behavior, QuestRuntime progress behavior, mission completion UI behavior, scene routing, shop, supply, mission select, office loop, or calendar UI was changed.

## 2. Files Changed

- `Assets/GAME/Scripts/Daily/DaySettlementSourceType.cs`
- `Assets/GAME/Scripts/Daily/DaySettlementSourceType.cs.meta`
- `Assets/GAME/Scripts/Daily/DaySettlementRequest.cs`
- `Assets/GAME/Scripts/Daily/DaySettlementResult.cs`
- `Assets/GAME/Scripts/Daily/DaySettlementFlow.cs`
- `Assets/GAME/Scripts/Quest/QuestRuntime.cs`
- `Assets/GAME/Scripts/Quest/QuestCompletionFlow.cs`
- `Assets/GAME/Scripts/DemoMission/Runtime/MissionCompletionController.cs`
- `Assets/GAME/Scripts/UI/DaySettlementPanel.cs`
- `Assets/GAME/Scripts/UI/DaySettlementPanel.cs.meta`
- `Assets/GAME/Docs/Architecture/DailySettlementPhase2Report_2026-07-03.md`
- `Assets/GAME/Docs/Architecture/DailySettlementPhase2Report_2026-07-03.md.meta`

## 3. Existing Phase 1 Daily / Settlement Behavior

Phase 1 behavior remains intact:

- `CalendarService` owns day/week/chapter/phase data.
- `DailyFlowController` owns explicit day phase transitions.
- `DaySettlementFlow` accepts settlement requests and completes the active settlement.
- Quest and mission settlement notifications are optional and default disabled.
- `completedSettlementIds` are represented in `GameSaveData.FutureDailySaveData`.

## 4. DaySettlementRequest Changes

`DaySettlementRequest` now additively carries:

- `sourceType` through `DaySettlementSourceType`
- `displayTitle`
- day/week/chapter/phase snapshot fields
- reward summary fields copied from `RewardGrantResult`

Existing fields were not renamed or removed.

Factory helpers now support title-aware quest and mission settlement requests. `ToRewardGrantResult()` and `ApplyRewardGrantResult(...)` keep `RewardGrantResult` usable as settlement summary input without changing reward application behavior.

## 5. DaySettlementResult Changes

`DaySettlementResult` now additively represents:

- settlement id
- source type
- quest id / mission id
- resolved completed quest-or-mission id
- display title
- reward source and reward summary
- completed flag
- completed day/week/chapter/phase
- next recommended phase

The current recommended next phase is `Rest`, matching Phase 1 `DailyFlowController.CompleteSettlement()` behavior.

## 6. QuestCompletionFlow Integration

`QuestCompletionFlow` still only notifies settlement when `notifyDaySettlementOnQuestCompletion` is enabled.

When enabled, it:

- safely resolves `DaySettlementFlow`
- grants quest rewards through the existing `RewardService` path when configured
- copies the resulting `RewardGrantResult` into the settlement request
- resolves the quest title from `QuestRuntime.TryGetQuestTitle(...)` when available
- does not stop existing quest completion behavior
- does not show settlement UI

## 7. MissionCompletionController Compatibility Integration

`MissionCompletionController` still only notifies settlement when `notifyDaySettlementOnMissionComplete` is enabled.

When enabled, it:

- safely resolves `DaySettlementFlow`
- creates a DemoMission-compatible settlement request from `DemoMissionRuntime.CurrentQuestId`
- carries `DemoMissionDefinitionSO.missionTitle` when available
- does not replace `MissionCompletePanel`
- does not grant rewards
- does not force settlement UI

## 8. RewardGrantResult Settlement Summary Handling

`RewardService` remains the only reward application owner.

Settlement request/result types carry copied reward summary data only:

- gold
- EXP
- item id
- item count
- duplicate reward blocked flag
- reward source type/id

No reward is granted or re-granted by `DaySettlementFlow` or `DaySettlementPanel`.

## 9. DaySettlementPanel Behavior

`DaySettlementPanel` was added under `Assets/GAME/Scripts/UI`.

It is presentation-only:

- displays a settlement title
- displays reward summary text/rows
- exposes `OnConfirmed` and `OnClosed`
- can optionally subscribe to `DaySettlementFlow.OnSettlementReady`
- can optionally call `DaySettlementFlow.CompleteSettlement()` on confirm
- does not grant rewards directly
- does not advance the day directly

All references are optional. Existing scenes do not need this panel.

## 10. Duplicate Settlement Prevention

`DaySettlementFlow` now guards duplicate settlements by:

- explicit settlement id
- resolved quest/mission completion key
- active request presence
- completed settlement ids restored from save data

This prevents a QuestCompletionFlow settlement and a DemoMission compatibility settlement from producing two results for the same quest/mission id.

Duplicate request blocking logs once.

## 11. SaveLoad Compatibility Notes

No new save contract fields were required.

`FutureDailySaveData.completedSettlementIds` remains the durable representation for completed settlement ids. On restore, `DaySettlementFlow` rebuilds simple completion keys from saved settlement ids so duplicate protection still works for restored data.

No full save/load execution path was implemented.

## 12. Inspector Wiring Notes

Existing optional fields remain:

- `QuestCompletionFlow.daySettlementFlow`
- `QuestCompletionFlow.notifyDaySettlementOnQuestCompletion`
- `MissionCompletionController.daySettlementFlow`
- `MissionCompletionController.notifyDaySettlementOnMissionComplete`
- `DaySettlementFlow.dailyFlowController`
- `DaySettlementFlow.calendarService`
- `DaySettlementFlow.enterSettlementPhaseOnRequest`

New optional panel fields:

- `DaySettlementPanel.root`
- `DaySettlementPanel.titleText`
- `DaySettlementPanel.rewardSummaryText`
- `DaySettlementPanel.rewardRowRoot`
- `DaySettlementPanel.confirmButton`
- `DaySettlementPanel.closeButton`
- `DaySettlementPanel.daySettlementFlow`
- `DaySettlementPanel.subscribeToSettlementFlow`
- `DaySettlementPanel.completeSettlementOnConfirm`

Suggested future hierarchy, documentation only:

```text
DailyRoot
- CalendarService
- DailyFlowController
- DaySettlementFlow

GameUIRoot
- DaySettlementPanel
- CalendarWidget
- MorningBriefingPanel
```

`UIScreenRouter` was not changed. Later routing should connect `DayPhase.Settlement` to a settlement panel through Daily/UI ownership, not by overloading current combat/reward `GameState` routing.

## 13. Current Behavior Preserved

Preserved behavior:

- combat reward granting still routes through `RewardService`
- `RewardUIPanel` behavior was not replaced
- QuestRuntime remains quest progress owner
- DemoMission compatibility remains active
- mission completion panel behavior remains unchanged by default
- settlement notifications remain default disabled
- no scene rewiring is required
- no Office/MissionSelect/Shop/Supply/calendar UI loop was implemented

## 14. Phase 3 Plan

1. Validate the optional settlement hooks in Unity Play Mode.
2. Add a scene-level `DailyRoot` only after component lifecycle is verified.
3. Decide whether quest or DemoMission settlement notifications should become enabled in specific scenes.
4. Wire `DaySettlementPanel` in a test scene and verify confirm/close behavior.
5. Route settlement UI from day phase changes after the Daily/UI boundary is agreed.
6. Add calendar widget and morning briefing separately from settlement result display.
7. Keep Mission Select, Office loop, Shop, and Supply as separate phases.

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

- New settlement request/result fields have not been inspected in Unity serialization.
- `DaySettlementPanel` has not been wired to a scene canvas yet.
- Optional settlement notifications are still disabled by default and need Inspector validation before use.
- The duplicate guard is intentionally simple and id-based; future content needs stable ids.
- Day phase to UI routing is still future work.
