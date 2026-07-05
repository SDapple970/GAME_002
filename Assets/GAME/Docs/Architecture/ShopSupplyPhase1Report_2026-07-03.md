# Shop / Supply Phase 1 Report - 2026-07-03

## 1. Summary

Phase 1 adds a minimal Shop / Supply backbone that can sit between MissionSelect and Field entry without replacing current Office/MissionSelect behavior.

The new runtime owners are passive unless wired:

- `ShopService` validates and applies simple item purchases.
- `ShopInventorySO` and `ShopItemEntry` provide static shop data.
- `SupplyLoadoutService` owns selected pre-mission supply items.
- `PreMissionSupplyFlow` owns the pre-mission supply completion step.
- `OfficeFlowController` remains the selected mission and field entry owner.

No full shop UI, economy balancing, equipment system, party management, combat, quest, reward, settlement, title, CaseFile, or `SceneTravelService` behavior was changed.

## 2. Files Changed

- `Assets/GAME/Scripts/Shop.meta`
- `Assets/GAME/Scripts/Shop/ShopService.cs`
- `Assets/GAME/Scripts/Shop/ShopService.cs.meta`
- `Assets/GAME/Scripts/Shop/ShopInventorySO.cs`
- `Assets/GAME/Scripts/Shop/ShopInventorySO.cs.meta`
- `Assets/GAME/Scripts/Shop/ShopItemEntry.cs`
- `Assets/GAME/Scripts/Shop/ShopItemEntry.cs.meta`
- `Assets/GAME/Scripts/Shop/PriceRule.cs`
- `Assets/GAME/Scripts/Shop/PriceRule.cs.meta`
- `Assets/GAME/Scripts/Shop/ShopPurchaseRequest.cs`
- `Assets/GAME/Scripts/Shop/ShopPurchaseRequest.cs.meta`
- `Assets/GAME/Scripts/Shop/ShopPurchaseResult.cs`
- `Assets/GAME/Scripts/Shop/ShopPurchaseResult.cs.meta`
- `Assets/GAME/Scripts/Supply.meta`
- `Assets/GAME/Scripts/Supply/SupplyLoadout.cs`
- `Assets/GAME/Scripts/Supply/SupplyLoadout.cs.meta`
- `Assets/GAME/Scripts/Supply/SupplyLoadoutService.cs`
- `Assets/GAME/Scripts/Supply/SupplyLoadoutService.cs.meta`
- `Assets/GAME/Scripts/Supply/PreMissionSupplyFlow.cs`
- `Assets/GAME/Scripts/Supply/PreMissionSupplyFlow.cs.meta`
- `Assets/GAME/Scripts/Office/OfficeFlowController.cs`
- `Assets/GAME/Scripts/NonCombat/Save/GameSaveData.cs`
- `Assets/GAME/Docs/Architecture/ShopSupplyPhase1Report_2026-07-03.md`
- `Assets/GAME/Docs/Architecture/ShopSupplyPhase1Report_2026-07-03.md.meta`

## 3. Existing Shop / Supply Code Found

No existing runtime `ShopService`, `ShopInventorySO`, `SupplyLoadoutService`, or `PreMissionSupplyFlow` was found.

Existing systems reused:

- `CurrencyWallet` under `Assets/GAME/Scripts/NonCombat/Inventory`
- `InventoryService` under `Assets/GAME/Scripts/NonCombat/Inventory`
- `OfficeFlowController`
- `MissionSelectResult`
- `FutureDailySaveData`

The task referenced `Assets/GAME/Scripts/Inventory`, but this checkout stores inventory/currency services under `Assets/GAME/Scripts/NonCombat/Inventory`.

## 4. ShopService Behavior

`ShopService` owns simple purchase validation/application.

It can:

- expose available entries from `ShopInventorySO`
- expose optional local entries
- validate `ShopPurchaseRequest`
- resolve `CurrencyWallet` and `InventoryService` through optional references or safe lookup
- spend gold through `CurrencyWallet.TrySpendGold(...)`
- add items through `InventoryService.AddItem(...)`
- return `ShopPurchaseResult`

It does not own UI, mission flow, rewards, quest progress, save slots, or economy balancing.

Defensive one-time logs were added for:

- missing `InventoryService`
- missing `CurrencyWallet`
- insufficient currency
- invalid shop item id

## 5. ShopInventorySO / ShopItemEntry Data Design

`ShopInventorySO` is static data only.

It stores a list of `ShopItemEntry`.

`ShopItemEntry` stores:

- item id
- display name
- price
- quantity granted per purchase
- unlocked flag

No runtime purchase state is stored in the ScriptableObject.

`PriceRule` is a minimal serializable helper for future tuning. It applies a multiplier and flat adjustment to a base price.

## 6. SupplyLoadoutService Behavior

`SupplyLoadoutService` owns selected pre-mission supply items.

It exposes:

- `AddItem(...)`
- `RemoveItem(...)`
- `ClearLoadout()`
- `GetSnapshot()`

`SupplyLoadout` stores item ids and counts in parallel lists for Unity/save compatibility.

The service does not remove items from inventory, grant rewards, load scenes, start combat, or manage party/equipment.

## 7. PreMissionSupplyFlow Behavior

`PreMissionSupplyFlow` owns the pre-mission supply step.

It can:

- receive a selected `MissionSelectResult`
- receive a selected mission id
- forward add/remove/clear calls to `SupplyLoadoutService`
- publish `OnSupplyLoadoutChanged`
- publish `OnSupplyCompleted`

It does not load scenes directly and does not force UI.

If supply is confirmed without a selected mission, it logs a one-time warning and does not complete.

## 8. OfficeFlowController Integration

`OfficeFlowController` now has an optional `PreMissionSupplyFlow` reference.

When `waitForSupplyBeforeFieldLoad` is enabled:

- selected mission state is still stored in `OfficeFlowController`
- selected mission data is passed to `PreMissionSupplyFlow`
- field loading remains delayed
- if `PreMissionSupplyFlow` is missing, a one-time warning is logged
- when `PreMissionSupplyFlow.OnSupplyCompleted` fires, `OfficeFlowController` calls `EnterSelectedMissionField()` only when `loadMissionSceneOnSelection` is true

When `waitForSupplyBeforeFieldLoad` is disabled, Phase 2 Office/MissionSelect behavior is preserved.

## 9. SaveLoad Compatibility Notes

`FutureDailySaveData` was extended additively with:

- `selectedSupplyItemIds`
- `selectedSupplyItemCounts`

`SupplyLoadoutService` implements `ISaveDataProvider` and `ISaveDataConsumer` for those fields.

No full inventory persistence was changed. Existing item storage remains owned by `InventoryService`, and durable inventory state remains represented by existing `InventorySaveData`.

No ScriptableObject references are saved.

## 10. UI Behavior

No `ShopPanel` or `SupplyPanel` was added in this phase.

This keeps Phase 1 focused on runtime ownership and avoids introducing incomplete UI routing. Future UI should remain presentation-only:

- display shop or supply entries
- raise purchase/select/confirm events
- delegate mutation to `ShopService`, `SupplyLoadoutService`, or `PreMissionSupplyFlow`
- never mutate `CurrencyWallet`, `InventoryService`, `RewardService`, or `SceneFlowController` directly

`UIScreenRouter` was not changed.

## 11. Inspector Wiring Notes

No existing serialized fields were renamed or removed.

New optional `OfficeFlowController` field:

- `preMissionSupplyFlow`

New optional `ShopService` fields:

- `shopInventory`
- `localItems`
- `priceRule`
- `currencyWallet`
- `inventoryService`

New optional `PreMissionSupplyFlow` field:

- `supplyLoadoutService`

Suggested future hierarchy, documentation only:

```text
OfficeRoot
- OfficeFlowController
- MissionSelectFlow
- PreMissionSupplyFlow
- SupplyLoadoutService
- ShopService

GameUIRoot
- MissionSelectPanel
- ShopPanel
- SupplyPanel
```

Existing scenes continue working without `ShopRoot` or `SupplyRoot`.

## 12. Current Behavior Preserved

Preserved behavior:

- Office/MissionSelect default behavior remains unchanged.
- Title flow was not changed.
- CaseFile behavior was not changed.
- `SceneTravelService` behavior was not changed.
- Combat behavior was not changed.
- Quest behavior was not changed.
- Reward behavior was not changed.
- Settlement behavior was not changed.
- Full shop UI, equipment, party management, and economy balancing were not implemented.

## 13. Phase 2 Plan

1. Wire `PreMissionSupplyFlow` in an Office test scene.
2. Add a presentation-only `SupplyPanel` after Play Mode validation.
3. Add a presentation-only `ShopPanel` after shop data and UI routing are agreed.
4. Decide whether supply loadout should consume inventory items before mission entry or only mark intended carry-ins.
5. Add item definition data if display names/icons need to come from a central item catalog.
6. Add purchase stock/availability only after shop content exists.
7. Keep Party/equipment work separate from Shop/Supply.

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

- New Shop/Supply components have not been scene-wired or Play Mode validated.
- Purchase behavior depends on a scene having `CurrencyWallet` and `InventoryService`.
- Supply loadout currently records selected item ids/counts only; it does not reserve or consume inventory.
- `PreMissionSupplyFlow` has no UI yet.
- `OfficeFlowController` supply handoff needs Inspector validation with `waitForSupplyBeforeFieldLoad` enabled.
