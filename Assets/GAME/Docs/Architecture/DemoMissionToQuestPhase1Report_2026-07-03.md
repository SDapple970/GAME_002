# DemoMission To Quest Phase 1 Report - 2026-07-03

## 1. Summary

Phase 1 added a minimal Quest event ownership path while preserving the existing DemoMission runtime, scene references, and UI flow. DemoMission remains the compatibility layer for current scenes. Existing mission progress actions now publish Quest-style events that can be consumed by the Quest tracker/runtime path.

No DemoMission files were deleted, moved, or renamed.

## 2. Files Changed

- `Assets/GAME/Scripts/DemoMission/Runtime/DemoMissionRuntime.cs`
- `Assets/GAME/Scripts/DemoMission/Runtime/RescueNpcActor.cs`
- `Assets/GAME/Scripts/Quest/QuestRuntime.cs`
- `Assets/GAME/Scripts/Quest/QuestObjectiveTracker.cs`
- `Assets/GAME/Scripts/Quest/QuestCompletionFlow.cs`
- `Assets/GAME/Scripts/Quest/QuestEvent.cs`
- `Assets/GAME/Scripts/Quest/QuestEventType.cs`
- `Assets/GAME/Scripts/Quest/QuestEventChannel.cs`
- `Assets/GAME/Scripts/Quest/QuestDefinitionSO.cs`
- `Assets/GAME/Scripts/Quest/QuestObjectiveDefinition.cs`

## 3. Existing DemoMission Flow

Current compatibility flow remains:

```text
CaseFileAcceptController / scene setup
-> DemoMissionRuntime.SetCurrentMission(...)
-> combat victory or FieldEnemy compatibility hook
-> DemoMissionRuntime.RegisterEnemyDefeated()
-> RescueNpcActor / DemoRescueNpcEndFlow
-> DemoMissionRuntime.RegisterNpcRescued()
-> DemoMissionRuntime.OnMissionProgressChanged / OnMissionCompleted
-> MissionObjectiveTracker
-> MissionCompletionController
-> MissionCompletePanel / UIOnly state
```

`MissionCompletionController` still handles current demo completion UI and input lock behavior. It was not coupled further to Reward, SceneFlow, or new Quest systems.

## 4. Existing Or Newly Added Quest Files

Existing Quest files reused:

- `QuestRuntime.cs`
- `QuestObjectiveTracker.cs`
- `QuestCompletionFlow.cs`
- `QuestDataSO.cs`
- `QuestManager.cs`
- `QuestProgress.cs`
- `QuestStepData.cs`

New Quest files added:

- `QuestEvent.cs`
- `QuestEventType.cs`
- `QuestEventChannel.cs`
- `QuestDefinitionSO.cs`
- `QuestObjectiveDefinition.cs`

The requested `Quest/Runtime` and `Quest/Data` folders were not created because this environment denied creating new subfolders under `Assets/GAME/Scripts/Quest`. The new files were added to the existing Quest folder instead.

## 5. Compatibility Bridge Design

Bridge direction is one-way:

```text
DemoMission compatibility layer
-> QuestEventChannel
-> QuestObjectiveTracker
-> QuestRuntime
-> QuestCompletionFlow
```

Published DemoMission events:

- Enemy defeated: `QuestEventType.Kill`, objective id `enemy_defeated`
- NPC talked/interacted: `QuestEventType.Talk`, objective id `npc_talked`
- NPC rescued: `QuestEventType.Rescue`, objective id `npc_rescued`

`QuestRuntime` now supports two paths:

- Existing compatibility methods that delegate to `MissionManager`
- New event-driven progress for `QuestDefinitionSO` / `QuestObjectiveDefinition`

If a `QuestDefinitionSO` is configured on `QuestRuntime`, events only progress matching objectives. If no definition is configured for a compatibility mission id, events can still be represented and counted internally, but they do not auto-complete a quest definition.

## 6. What Still Depends On DemoMission

- `DemoMissionRuntime` remains the current runtime state owner for demo mission progress.
- `MissionObjectiveTracker` still reads DemoMission progress and updates its objective text.
- `MissionCompletionController` still opens the mission complete panel and changes state to `UIOnly`.
- `RescueNpcActor` still controls current rescue interaction behavior.
- `CombatRewardUIBinder` still calls `DemoMissionRuntime.RegisterEnemyDefeated()` on victory for compatibility.
- DemoMission UI panels and case-file accept flow still use DemoMission data directly.

## 7. What Should Migrate In Phase 2

- Convert demo mission data into `QuestDefinitionSO` assets or a mapper from `DemoMissionDefinitionSO` to Quest definitions.
- Move objective text/state display from `MissionObjectiveTracker` to a Quest-backed tracker/HUD.
- Route combat result, rescue, dialogue, and interaction outcomes through Quest events first, with DemoMission observing for compatibility only.
- Move completion orchestration from `MissionCompletionController` into `QuestCompletionFlow` once scene references are validated.
- Move reward routing out of DemoMission/combat UI compatibility and toward a Quest completion result.

## 8. Inspector / Scene Wiring Notes

No existing serialized fields were renamed or removed.

No immediate scene rewiring is required for existing DemoMission behavior.

For Quest event consumption in a scene, add or confirm a Quest root containing:

- `QuestRuntime`
- `QuestObjectiveTracker`
- `QuestCompletionFlow` if completion orchestration should be observed

Assign `QuestDefinitionSO` assets to `QuestRuntime.questDefinitions` when ready. The `questId` should match the current `DemoMissionDefinitionSO.missionId` for compatibility events to update the new Quest runtime.

## 9. Compile Validation Result

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

## 10. Unity Play Mode Risks

- Quest event consumption requires `QuestRuntime` and `QuestObjectiveTracker` to exist in the active scene or a persistent root. Existing DemoMission behavior does not require this.
- Current DemoMission completion UI still owns completion presentation.
- `CombatRewardUIBinder` can still increment DemoMission enemy defeat count on combat victory; verify no duplicate enemy-defeat calls occur in the active scene.
- Existing garbled localized strings in `MissionObjectiveTracker` were not changed in this phase.

## 11. Recommended Next Task

Create one QuestDefinitionSO asset for the current DemoMission content and wire a non-destructive Quest root in the main dungeon scene. Then validate that enemy kill, NPC talk, and NPC rescue events update `QuestRuntime` while DemoMission UI and completion behavior continue to work.
