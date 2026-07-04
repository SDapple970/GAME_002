# RewardService Consolidation Phase 2 Report - 2026-07-03

## 1. Summary

Phase 2 tightens the production combat reward route so combat rewards are represented as `RewardGrantRequest`, applied by `RewardService`, and displayed through `RewardUIPanel` using `RewardGrantResult`.

`RewardUIPanel` remains presentation-only. `CombatRewardUIBinder` remains a binding/orchestration layer and no longer calls the legacy `GrantCombatResult(...)` wrapper for the production combat path. Combat damage and resolution rules were not changed.

## 2. Files Changed

- `Assets/GAME/Scripts/Reward/RewardGrantResult.cs`
- `Assets/GAME/Scripts/Reward/RewardGrantResult.cs.meta`
- `Assets/GAME/Scripts/Reward/RewardResult.cs`
- `Assets/GAME/Scripts/Reward/RewardService.cs`
- `Assets/GAME/Scripts/Combat/Runtime/UI/CombatRewardUIBinder.cs`
- `Assets/GAME/Scripts/UI/RewardUIPanel.cs`
- `Assets/GAME/Scripts/NonCombat/Reward/RewardApplier.cs`
- `Assets/GAME/Docs/Architecture/RewardServiceConsolidationPhase2Report_2026-07-03.md`
- `Assets/GAME/Docs/Architecture/RewardServiceConsolidationPhase2Report_2026-07-03.md.meta`

## 3. Existing Combat Reward Route Found

Before Phase 2:

```text
CombatEntryPoint.OnCombatEnded
-> CombatRewardUIBinder.HandleCombatEnded(...)
-> RewardService.GrantCombatResult(CombatResult)
-> CurrencyWallet / EXP placeholder
-> RewardUIPanel.Show(CombatResult)
```

`RewardUIPanel.Show(CombatResult)` displayed configured static victory/defeat reward lines. It did not mutate inventory, currency, quest, or progression state.

`RewardApplier.ApplyCombatResult(...)` was a legacy adapter that forwarded to `RewardService` when available, then fell back to direct `CurrencyWallet` mutation.

## 4. Final Combat Reward Route

Production combat route after Phase 2:

```text
CombatEntryPoint.OnCombatEnded
-> CombatRewardUIBinder.HandleCombatEnded(...)
-> CombatRewardUIBinder builds RewardGrantRequest
-> RewardService.GrantReward(RewardGrantRequest)
-> CurrencyWallet / InventoryService / EXP placeholder
-> RewardGrantResult
-> RewardUIPanel.Show(CombatResult, RewardGrantResult)
```

`CombatResult` remains the combat outcome data. It does not apply rewards directly.

## 5. RewardService Ownership Details

`RewardService` now exposes `GrantReward(RewardGrantRequest)` as the canonical request-to-result application method.

Existing public compatibility methods remain:

- `Grant(RewardGrantRequest)`
- `GrantCombatResult(CombatResult)`
- `GrantQuestCompletion(string questId, int gold, int exp)`
- `GrantMissionCompletion(string missionId, int gold, int exp)`

`RewardService` owns application of:

- gold through `CurrencyWallet`
- items through `InventoryService`
- EXP as a pending progression integration log

`RewardGrantResult` reports what was actually applied.

## 6. RewardUIPanel Behavior

`RewardUIPanel` now has an overload:

```text
Show(CombatResult result, RewardGrantResult grantResult)
```

This overload displays applied grant result rows such as gold, EXP, and item count. It does not mutate inventory, currency, quest, or progression systems.

The existing `Show(CombatResult result)` method remains for legacy/debug callers and continues to use configured static reward rows.

## 7. CombatRewardUIBinder Behavior

`CombatRewardUIBinder` now:

- receives `CombatResult` from `CombatEntryPoint.OnCombatEnded`
- keeps DemoMission enemy-defeat compatibility behavior
- builds a `RewardGrantRequest` from `CombatResult.TotalGold` and `CombatResult.TotalExp`
- calls `RewardService.GrantReward(...)`
- passes `RewardGrantResult` to `RewardUIPanel`
- logs once for missing `RewardService`
- logs once and clamps if combat reward data is negative

It does not directly mutate currency, inventory, quest, or progression state.

## 8. Quest Reward Route Compatibility

Phase 1 quest reward behavior remains intact:

- `QuestCompletionFlow` can grant QuestRuntime completion rewards through `RewardService`.
- Legacy `QuestManager` completion can grant `QuestDataSO` rewards through `RewardService`.
- Quest duplicate reward guards remain in their existing owners.

No Quest files were changed in Phase 2.

## 9. DemoMission Reward Compatibility

DemoMission compatibility remains intact:

- `CombatRewardUIBinder` still calls `DemoMissionRuntime.RegisterEnemyDefeated()` on combat victory when configured.
- `MissionCompletionController` remains a completion presentation compatibility layer and does not directly grant rewards.

No DemoMission reward removal or migration was performed.

## 10. Double-Grant Prevention

`RewardService` now tracks combat reward source ids and blocks duplicate combat grants.

Combat source ids are generated from the `CombatResult` object identity for the current runtime process. If the same `CombatResult` is handled repeatedly, the first grant applies and later grants return a `RewardGrantResult` marked as duplicate-blocked.

`RewardApplier` was tightened so it no longer falls back to direct `CurrencyWallet` mutation when `RewardService` is missing. It logs once instead.

## 11. SaveLoad Phase 1 Compatibility Notes

No new durable reward state was added in Phase 2.

RewardService remains an application owner, not a saved state owner. Durable reward results continue to land in:

- `CurrencyWallet`, represented by `CurrencySaveData`
- `InventoryService`, represented by `InventorySaveData`

No save/load file path, save UI, slots, or Daily/Calendar save behavior was changed.

## 12. Inspector Wiring Notes

No existing serialized fields were renamed or removed.

Existing optional references remain:

- `CombatRewardUIBinder.rewardService`
- `CombatRewardUIBinder.rewardPanel`
- `RewardService.currencyWallet`
- `RewardService.inventoryService`

Fallback lookup remains in place, so scenes should not require rewiring for this phase.

Recommended future hierarchy, not required in this phase:

```text
RewardRoot
- RewardService
- RewardUIPanel
- CurrencyWallet
- InventoryService
- CharacterProgressionService
```

## 13. Remaining Progression / EXP Integration Gaps

No `CharacterProgressionService` was found in the inspected runtime scripts.

EXP is still represented in `RewardGrantRequest` and `RewardGrantResult`, and `RewardService` logs the pending progression integration once. No full progression system was implemented in this task.

## 14. Phase 3 Reward Plan

1. Add a real `CharacterProgressionService` or identify the final progression owner.
2. Route EXP grants from `RewardService` into that owner.
3. Add a result presentation format for richer reward rows if item names/icons are needed.
4. Decide whether interaction fallback direct grants should remain for scenes without `RewardService`.
5. Convert broader mixed outcome routes such as `ChoiceRunner` only after flag/persona/chapter ownership is separated from item/currency reward ownership.
6. Add reward tables or reward definition assets only when production content requires them.

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

- `RewardUIPanel.Show(CombatResult, RewardGrantResult)` has not been scene-tested.
- Existing scenes with both `CombatRewardUIBinder` and `CombatDemoFlowController` may still show reward UI through more than one presentation listener, though only `RewardService` applies rewards.
- EXP still does not mutate progression state.
- Duplicate combat reward prevention uses runtime object identity, which is appropriate for current in-session combat end events but not a persisted save/load identity.
- Scene/prefab references should be checked in Unity because Play Mode validation was not available.
