# DemoMission To Quest Phase 2 Report - 2026-07-03

## 1. Summary

Phase 2 promotes `QuestRuntime` as the preferred owner of mission objective progress while preserving DemoMission scene compatibility. Existing DemoMission public methods remain usable, and current scenes can continue to run without a wired Quest root.

No DemoMission files were deleted, moved, or renamed. Full DemoMission removal was not performed.

## 2. Files Changed

- `Assets/GAME/Scripts/Quest/QuestRuntime.cs`
- `Assets/GAME/Scripts/Quest/QuestEventType.cs`
- `Assets/GAME/Scripts/DemoMission/Runtime/DemoMissionRuntime.cs`
- `Assets/GAME/Scripts/DemoMission/Runtime/MissionObjectiveTracker.cs`
- `Assets/GAME/Scripts/DemoMission/Runtime/MissionCompletionController.cs`
- `Assets/GAME/Docs/Architecture/DemoMissionToQuestPhase2Report_2026-07-03.md`

## 3. Current DemoMission Compatibility Behavior

These existing public methods remain intact:

- `DemoMissionRuntime.SetCurrentMission(...)`
- `DemoMissionRuntime.SetMission(...)`
- `DemoMissionRuntime.ResetMissionProgress()`
- `DemoMissionRuntime.RegisterEnemyDefeated()`
- `DemoMissionRuntime.RegisterNpcRescued()`
- `DemoMissionRuntime.HasRequiredEnemyKills()`
- `DemoMissionRuntime.IsMissionComplete()`

DemoMission still keeps compatibility counters and events for existing scenes. When `QuestRuntime` is available, DemoMission queries Quest progress for completion checks; when it is unavailable, DemoMission falls back to its original counters.

## 4. QuestRuntime Ownership Changes

`QuestRuntime` now supports runtime compatibility objective requirements without depending on DemoMission types:

- `ConfigureCompatibilityQuest(...)`
- `ResetQuestProgress(...)`
- `GetObjectiveRequiredCount(...)`
- `GetObjectiveProgress(...)`
- `HasQuest(...)`
- `IsQuestComplete(...)`

The current DemoMission objective set is represented as:

- `enemy_defeated`: kill count objective
- `npc_talked`: talk/interact objective, optional by default
- `npc_rescued`: rescue objective
- `mission_completed`: completion marker event

`QuestRuntime.OnQuestCompleted` now fires for both `QuestDefinitionSO`-backed quests and runtime compatibility quests.

## 5. Event Bridge Changes

`DemoMissionRuntime` now resolves an optional `QuestRuntime` when bridge mode is enabled.

Bridge behavior:

- If `QuestRuntime` exists, DemoMission forwards events directly to `QuestRuntime.ApplyEvent(...)`.
- If `QuestRuntime` is missing, DemoMission logs once and falls back to `QuestEventChannel.Publish(...)`.
- Direct forwarding avoids double-applying the same DemoMission event through both direct runtime calls and the static event channel.

Published events:

- Enemy defeated -> `QuestEventType.Kill / enemy_defeated`
- NPC talked -> `QuestEventType.Talk / npc_talked`
- NPC rescued -> `QuestEventType.Rescue / npc_rescued`
- Mission completed -> `QuestEventType.MissionCompleted / mission_completed`

## 6. MissionObjectiveTracker Behavior

`MissionObjectiveTracker` remains a DemoMission compatibility component.

Preferred behavior:

- If `QuestRuntime` has a quest matching `DemoMissionRuntime.CurrentQuestId`, tracker display reads QuestRuntime progress.
- If QuestRuntime is missing or has no matching quest, tracker falls back to DemoMission counters and old behavior.

The tracker subscribes to:

- `DemoMissionRuntime.OnMissionProgressChanged`
- `DemoMissionRuntime.OnMissionCompleted`
- `QuestRuntime.OnObjectiveProgressChanged`
- `QuestRuntime.OnQuestCompleted`

## 7. MissionCompletionController Behavior

`MissionCompletionController` remains scene-compatible and still opens the existing mission complete panel.

It now also listens to `QuestRuntime.OnQuestCompleted` when QuestRuntime is present. Existing DemoMission and `MissionObjectiveTracker` completion events remain subscribed for compatibility.

The controller uses one `_handled` guard and one wait coroutine guard so duplicate completion signals do not show the completion UI more than once.

## 8. Double-Count / Double-Completion Prevention

Double-count prevention:

- DemoMission forwards directly to QuestRuntime when QuestRuntime exists.
- DemoMission only publishes to `QuestEventChannel` as a fallback when no QuestRuntime is found.
- Mission objective display treats QuestRuntime as authoritative when available and does not sum Quest and DemoMission counters.

Double-completion prevention:

- `QuestRuntime` ignores events after a quest is complete.
- `DemoMissionRuntime` keeps its existing `_completionRaised` guard.
- `MissionObjectiveTracker` keeps its `_completedRaised` guard.
- `MissionCompletionController` keeps its `_handled` guard and now logs once when duplicate completion is blocked.

## 9. Inspector Wiring Notes

No existing serialized fields were renamed or removed.

New optional serialized fields:

- `DemoMissionRuntime.questRuntime`
- `DemoMissionRuntime.bridgeToQuestRuntime`
- `DemoMissionRuntime.requireNpcTalkForQuestCompletion`
- `DemoMissionRuntime.requireNpcRescueForQuestCompletion`
- `MissionObjectiveTracker.questRuntime`
- `MissionObjectiveTracker.preferQuestRuntime`
- `MissionCompletionController.questRuntime`

Existing scenes should continue working without manual rewiring because all new Quest references fall back to `FindFirstObjectByType<QuestRuntime>()`.

Recommended future hierarchy, not required in this phase:

```text
QuestRoot
- QuestRuntime
- QuestObjectiveTracker
- QuestCompletionFlow
```

`QuestEventChannel` is static and does not require a scene object.

## 10. Remaining DemoMission Dependencies

- DemoMission data assets still use `DemoMissionDefinitionSO`.
- Case-file accept UI still sets `DemoMissionRuntime` mission data.
- Combat reward compatibility still calls `DemoMissionRuntime.RegisterEnemyDefeated()`.
- Rescue interaction flow still lives in `RescueNpcActor` and `DemoRescueNpcEndFlow`.
- Mission completion presentation still lives in `MissionCompletionController` and `MissionCompletePanel`.

## 11. Phase 3 Migration Plan

1. Create QuestDefinitionSO assets for existing DemoMission content.
2. Wire a non-destructive QuestRoot in the active dungeon scene.
3. Move combat victory progress from `CombatRewardUIBinder -> DemoMissionRuntime` to direct Quest events.
4. Move rescue/talk progress from DemoMission actors to Quest events first, with DemoMission observing for compatibility.
5. Move mission completion UI trigger from `MissionCompletionController` to `QuestCompletionFlow` after scene references are validated.
6. Only after scenes and prefabs no longer reference DemoMission progress owners, plan DemoMission removal or archival.

## 12. Compile Validation Result

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

This matches the known Unity-generated project behavior in this repository: the available `dotnet` check reports no C# compile warnings or errors but exits nonzero. Unity is not available on PATH here, so Unity Play Mode validation was not run.

## 13. Unity Play Mode Risks

- QuestRuntime-backed display requires a `QuestRuntime` object to exist in the scene or a persistent root.
- Existing DemoMission UI remains active and should be checked for duplicate completion panel display.
- `CombatRewardUIBinder` still increments DemoMission compatibility progress on combat victory.
- `MissionObjectiveTracker` still contains legacy fallback text for scenes without QuestRuntime.
- Scene/prefab serialized references were preserved, but new optional fields need Inspector validation in Unity.
