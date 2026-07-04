# RewardService Consolidation Phase 1 Report - 2026-07-03

## 1. Summary

Phase 1 promotes `RewardService` as the preferred owner for applying rewards while keeping existing DemoMission, Quest, interaction, and UI compatibility paths intact.

Combat rewards were already routed through `RewardService`; this pass tightened the service into a request-based grant owner and added Quest completion routing. Existing reward UI remains presentation-only for combat rewards.

No files were deleted, moved, or renamed. Daily, Calendar, Settlement, combat damage, and dialogue behavior were not changed.

## 2. Files Changed

- `Assets/GAME/Scripts/Reward/RewardService.cs`
- `Assets/GAME/Scripts/Reward/RewardResult.cs`
- `Assets/GAME/Scripts/Reward/RewardGrantRequest.cs`
- `Assets/GAME/Scripts/Reward/RewardGrantRequest.cs.meta`
- `Assets/GAME/Scripts/Reward/RewardSourceType.cs`
- `Assets/GAME/Scripts/Reward/RewardSourceType.cs.meta`
- `Assets/GAME/Scripts/Quest/QuestDefinitionSO.cs`
- `Assets/GAME/Scripts/Quest/QuestRuntime.cs`
- `Assets/GAME/Scripts/Quest/QuestCompletionFlow.cs`
- `Assets/GAME/Scripts/Quest/QuestManager.cs`
- `Assets/GAME/Scripts/Interaction/RewardInteractionEventSO.cs`
- `Assets/GAME/Scripts/Interaction/RandomLootInteractionEventSO.cs`
- `Assets/GAME/Docs/Architecture/RewardServiceConsolidationPhase1Report_2026-07-03.md`

## 3. Current Reward Routes Found

- Combat victory: `CombatRewardUIBinder -> RewardService.GrantCombatResult(...) -> CurrencyWallet`; EXP is logged because no character progression service exists yet.
- Combat reward presentation: `CombatRewardUIBinder -> RewardUIPanel.Show(...)`.
- Legacy combat reward adapter: `RewardApplier.ApplyCombatResult(...)` forwards to `RewardService` when available, then falls back to `CurrencyWallet`.
- QuestRuntime completion: `QuestRuntime.OnQuestCompleted -> QuestCompletionFlow -> RewardService`.
- Legacy QuestManager completion: `QuestManager.CompleteQuest(...) -> RewardService` when `QuestDataSO` has reward values.
- Simple interaction item rewards: `RewardInteractionEventSO` and `RandomLootInteractionEventSO` now prefer `RewardService`, with the old `InventoryService` fallback preserved.
- Remaining direct route: `ChoiceRunner` still applies mixed outcomes directly for gold, item, persona, flag, and chapter changes. This was left intact because it is broader than reward grant ownership.

`RewardDefinitionSO` and `RewardTableSO` were not present in the inspected scripts, so no duplicate reward definition/table type was added.

## 4. RewardService Ownership Design

`RewardService` now exposes:

- `Grant(RewardGrantRequest request)`
- `GrantCombatResult(CombatResult result)`
- `GrantQuestCompletion(string questId, int gold, int exp)`
- `GrantMissionCompletion(string missionId, int gold, int exp)`

`RewardGrantRequest` carries a simple source type, source id, gold, EXP, and one optional item id/count. `RewardResult` now reports applied gold, EXP, and optional item data.

`RewardService` forwards to existing systems only when available:

- `CurrencyWallet` for gold
- `InventoryService` for items
- Character progression is not implemented yet, so EXP is accepted and logged once as a missing integration.

## 5. QuestCompletionFlow Reward Route

`QuestDefinitionSO` now has optional `rewardGold` and `rewardExp` fields. `QuestRuntime.TryGetQuestReward(...)` exposes that data to orchestration code.

`QuestCompletionFlow` now resolves an optional `RewardService`, listens to `QuestRuntime.OnQuestCompleted`, and grants a quest completion reward when:

- `grantRewardOnCompletion` is enabled,
- a matching `QuestDefinitionSO` has nonzero reward data, or
- the optional fallback reward fields are configured.

Missing `RewardService` and missing reward definition data are logged once.

## 6. MissionCompletionController Compatibility Behavior

`MissionCompletionController` remains a completion presentation compatibility layer. It does not directly grant rewards in this phase.

Mission completion reward application should be owned by `QuestCompletionFlow`/`RewardService` when a Quest-backed reward exists. DemoMission compatibility UI remains unchanged and still uses its existing duplicate completion guard.

## 7. Combat Reward Route Status

Combat victory rewards remain on the existing production route:

```text
CombatEntryPoint.OnCombatEnded
-> CombatRewardUIBinder
-> RewardService.GrantCombatResult(...)
-> CurrencyWallet / EXP placeholder
-> RewardUIPanel.Show(...)
```

Combat result building and combat resolution rules were not changed.

## 8. RewardUIPanel Behavior

`RewardUIPanel` was not changed. It still displays combat result reward rows and field reward messages. Its legacy `ApplyReward(...)` hook remains empty, with reward granting owned by `RewardService`/reward flow.

## 9. Double-Grant Prevention

- `QuestCompletionFlow` tracks rewarded quest ids and blocks repeated reward grants from repeated completion signals.
- `QuestManager` tracks rewarded legacy `QuestId` values and blocks repeated legacy quest reward grants.
- `MissionCompletionController` already blocks duplicate completion presentation and does not grant rewards directly.
- `RewardApplier` still falls back to direct `CurrencyWallet` only when no `RewardService` exists.

## 10. Missing Inventory/Currency/Progression Integration Notes

- `CurrencyWallet` exists and is used for gold.
- `InventoryService` exists and is used for item grants.
- `CharacterProgressionService` was not found. EXP is represented in requests/results and logged once by `RewardService`, but no progression state is mutated yet.
- `ChoiceRunner` still directly mutates mixed outcome systems and should be handled in a later, broader non-combat outcome pass.

## 11. Inspector Wiring Notes

No existing serialized fields were renamed or removed.

New optional serialized fields:

- `RewardService.inventoryService`
- `QuestDefinitionSO.rewardGold`
- `QuestDefinitionSO.rewardExp`
- `QuestCompletionFlow.rewardService`
- `QuestCompletionFlow.grantRewardOnCompletion`
- `QuestCompletionFlow.fallbackRewardGold`
- `QuestCompletionFlow.fallbackRewardExp`

Existing scenes should continue working without manual rewiring because `RewardService`, `QuestCompletionFlow`, and combat reward routing use singleton or `FindFirstObjectByType` fallbacks.

Recommended future hierarchy, not required in this phase:

```text
RewardRoot
- RewardService
- RewardUIPanel
- CurrencyWallet
- InventoryService
- CharacterProgressionService
```

## 12. Phase 2 Reward Migration Plan

1. Add or identify production reward definition assets if `RewardDefinitionSO` / `RewardTableSO` become necessary.
2. Add a real `CharacterProgressionService` integration for EXP.
3. Convert `ChoiceRunner` mixed reward outcomes to request `RewardService` for gold/items while preserving flag, persona, and chapter ownership.
4. Decide whether interaction reward fallbacks should remain or require a `RewardService` in production scenes.
5. Add a reward result presentation path so `RewardUIPanel` can display actual `RewardResult` values instead of static combat lines.
6. After DemoMission scene references are validated, migrate any mission reward data into `QuestDefinitionSO` or reward definition assets.

## 13. Compile Validation Result

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

This matches the previously observed Unity-generated project behavior in this repository: the available `dotnet` check reports no C# compile warnings or errors but exits nonzero.

## 14. Unity Play Mode Risks

Unity is not available on PATH in this environment, so Unity Play Mode validation was not run.

Manual validation should confirm:

- Combat victory grants gold once through `RewardService`.
- QuestRuntime completion grants configured `QuestDefinitionSO` rewards once.
- Legacy `QuestManager` completion grants `QuestDataSO` rewards once.
- Mission completion UI still appears once and does not grant duplicate rewards.
- Reward UI still appears after combat and field interaction reward messages still display.
- Scenes without a wired `InventoryService`, `CurrencyWallet`, or `RewardService` only log the intended defensive warnings.
