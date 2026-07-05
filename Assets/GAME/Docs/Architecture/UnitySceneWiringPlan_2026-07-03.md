# Unity Scene Wiring Plan - 2026-07-03

## 1. Target Hierarchy

```text
RuntimeRoot
- CoreRoot
- GameStateRoot
- InputRoot
- CombatRoot
- QuestRoot
- RewardRoot
- DailyRoot
- OfficeRoot
- ShopSupplyRoot
- SaveLoadRoot
- GameUIRoot
- PlayerRoot
- FieldRoot
- DebugRoot
```

This hierarchy is for a future integrated test scene. It should not replace existing production/demo scenes until Play Mode validation passes.

## 2. Required GameObjects

### RuntimeRoot

- Required GameObject name: `RuntimeRoot`
- Required components: none
- Optional components: none
- Required Inspector references: none
- Optional Inspector references: none
- Fallback notes: parent only
- Do not connect yet: no gameplay logic directly on this root

### CoreRoot

- Required GameObject name: `CoreRoot`
- Required components: `GameFlowController`, `SceneFlowController`, `RuntimeBootstrapper`
- Optional components: none
- Required Inspector references: assign only if component exposes them in current scene
- Optional Inspector references: none
- Fallback notes: `SceneFlowController` uses `Instance`; avoid duplicate scene/persistent instances
- Do not connect yet: spawn-point scene loading; `SceneFlowController` has no spawn API

### GameStateRoot

- Required GameObject name: `GameStateRoot`
- Required components: `GameStateMachine`
- Optional components: none
- Required Inspector references: none
- Optional Inspector references: none
- Fallback notes: many systems depend on `GameStateMachine.Instance`
- Do not connect yet: do not add new GameState values for Daily phases

### InputRoot

- Required GameObject name: `InputRoot`
- Required components: `InputService`, `InputRouter`, `GameInputInstaller`
- Optional components: `InputDeviceWatcher`, `RebindSaveLoad`
- Required Inspector references: wire installer/router/service according to existing prefab pattern
- Optional Inspector references: device watcher UI hooks
- Fallback notes: input route should use current production installer; old player/direct input scripts may still exist
- Do not connect yet: do not remove old input bridges until active player prefab is validated

### CombatRoot

- Required GameObject name: `CombatRoot`
- Required components: `CombatEntryPoint`, `CombatFlowOrchestrator`, `CombatDirector`, `CombatStateSyncer`, `CombatFormationManager`
- Optional components: `CombatUIRootController`, `CombatPlanningHUD`, `CombatRewardUIBinder`
- Required Inspector references: `CombatEntryPoint.director`, `CombatEntryPoint.flowOrchestrator`, skill definitions, combat UI references
- Optional Inspector references: opening effects, debug combat tools
- Fallback notes: `CombatEntryPoint` can find `CombatFlowOrchestrator`; explicit references are safer
- Do not connect yet: legacy battle transition route as production entry

### QuestRoot

- Required GameObject name: `QuestRoot`
- Required components: `QuestRuntime`, `QuestObjectiveTracker`, `QuestCompletionFlow`
- Optional components: DemoMission compatibility trackers where required by test scene
- Required Inspector references: `QuestRuntime.questDefinitions` for production test missions
- Optional Inspector references: `QuestCompletionFlow.rewardService`, `QuestCompletionFlow.daySettlementFlow`
- Fallback notes: DemoMission can find `QuestRuntime`; explicit reference is safer
- Do not connect yet: do not remove DemoMission runtime or mission completion UI

### RewardRoot

- Required GameObject name: `RewardRoot`
- Required components: `RewardService`, `InventoryService`, `CurrencyWallet`
- Optional components: `CurrencyHUD`
- Required Inspector references: `RewardService.currencyWallet`, `RewardService.inventoryService`
- Optional Inspector references: HUD text
- Fallback notes: `RewardService` can fall back to singleton wallet/inventory
- Do not connect yet: EXP progression owner; none exists yet

### DailyRoot

- Required GameObject name: `DailyRoot`
- Required components: `CalendarService`, `DailyFlowController`, `DaySettlementFlow`
- Optional components: none
- Required Inspector references: `DailyFlowController.calendarService`, `DaySettlementFlow.dailyFlowController`, `DaySettlementFlow.calendarService`
- Optional Inspector references: `DaySettlementFlow.enterSettlementPhaseOnRequest`
- Fallback notes: `CalendarService` and `DaySettlementFlow` have singleton/fallback lookup
- Do not connect yet: full calendar UI, automatic day loop, office/day phase bootstrap

### OfficeRoot

- Required GameObject name: `OfficeRoot`
- Required components: `OfficeFlowController`, `MissionSelectFlow`
- Optional components: existing `OfficeMenuController`, `OfficeHotspot2D` objects
- Required Inspector references: `OfficeFlowController.dailyFlowController`, `missionSelectFlow`, `sceneFlowController`
- Optional Inspector references: `missionSelectPanel`, `preMissionSupplyFlow`
- Fallback notes: `OfficeFlowController` can find dependencies but should be explicit in the integrated test scene
- Do not connect yet: existing `CaseFileDocumentPanel` travel as the same launch button as new MissionSelect

### ShopSupplyRoot

- Required GameObject name: `ShopSupplyRoot`
- Required components: `ShopService`, `SupplyLoadoutService`, `PreMissionSupplyFlow`
- Optional components: none
- Required Inspector references: `ShopService.currencyWallet`, `ShopService.inventoryService`, `PreMissionSupplyFlow.supplyLoadoutService`
- Optional Inspector references: `ShopService.shopInventory`, `ShopService.priceRule`
- Fallback notes: shop/supply services can discover core dependencies, but explicit wiring is safer
- Do not connect yet: full ShopPanel, SupplyPanel, equipment, party management

### SaveLoadRoot

- Required GameObject name: `SaveLoadRoot`
- Required components: `SaveLoadService`
- Optional components: `SaveManager` for legacy path
- Required Inspector references: `SaveLoadService.saveManager` if testing legacy save/load buttons
- Optional Inspector references: none
- Fallback notes: `SaveLoadService` discovers save providers/consumers globally
- Do not connect yet: save slots, cloud save, schema migration UI

### GameUIRoot

- Required GameObject name: `GameUIRoot`
- Required components: `GameUIRootController`, `UIScreenRouter`
- Optional components: `MissionSelectPanel`, `DaySettlementPanel`, `RewardUIPanel`, `CombatPlanningHUD`, `CombatUIRootController`
- Required Inspector references: `UIScreenRouter.uiRoot`, `UIScreenRouter.stateMachine`, `GameUIRootController` major roots
- Optional Inspector references: `MissionSelectPanel.missionSelectFlow`, `DaySettlementPanel.daySettlementFlow`, button templates
- Fallback notes: `GameUIRootController` auto-binds by component/name, but explicit roots are safer
- Do not connect yet: ShopPanel/SupplyPanel; they do not exist yet

### PlayerRoot

- Required GameObject name: `PlayerRoot`
- Required components: active player controller stack used by the current scene, `PlayerFieldAttackController`
- Optional components: old player/input bridge components if required by scene prefab
- Required Inspector references: field attack target layers/entry references according to current prefab
- Optional Inspector references: combat entry fallback if discoverable
- Fallback notes: player scripts often rely on `GameStateMachine` gating
- Do not connect yet: remove old movement/input stack

### FieldRoot

- Required GameObject name: `FieldRoot`
- Required components: field enemies, encounter triggers, interactable objects, spawn points if using `SceneTravelService`
- Optional components: `CombatEncounterGroup`, `CombatEncounterTrigger2D`, `StoryInteraction` components
- Required Inspector references: encounter field objects, player/enemy combat adapters, quest event data
- Optional Inspector references: opening effects
- Fallback notes: combat field objects must have HP/skill/keyword adapters as required by combat factory
- Do not connect yet: duplicate legacy `BattleTrigger2D` and production trigger on the same encounter

### DebugRoot

- Required GameObject name: `DebugRoot`
- Required components: none
- Optional components: `CombatFieldCallDebug`, `CombatStartSmokeTest`, `VerticalSliceSceneValidator`, other debug-only tools
- Required Inspector references: only for the specific test tool being used
- Optional Inspector references: debug hotkeys
- Fallback notes: debug providers may be found by save snapshot if they implement provider interfaces in future
- Do not connect yet: debug tools in production build scenes

## 3. Components Per GameObject

Minimum integrated test scene component list:

- `GameStateMachine`
- `SceneFlowController`
- `RuntimeBootstrapper`
- `CombatEntryPoint`
- `CombatFlowOrchestrator`
- `CombatDirector`
- `CombatRewardUIBinder`
- `QuestRuntime`
- `QuestObjectiveTracker`
- `QuestCompletionFlow`
- `RewardService`
- `InventoryService`
- `CurrencyWallet`
- `CalendarService`
- `DailyFlowController`
- `DaySettlementFlow`
- `OfficeFlowController`
- `MissionSelectFlow`
- `ShopService`
- `SupplyLoadoutService`
- `PreMissionSupplyFlow`
- `SaveLoadService`
- `GameUIRootController`
- `UIScreenRouter`
- `MissionSelectPanel`
- `DaySettlementPanel`
- `RewardUIPanel`

## 4. Inspector References

Set these explicitly in the integrated test scene:

- `UIScreenRouter.stateMachine -> GameStateMachine`
- `UIScreenRouter.uiRoot -> GameUIRootController`
- `GameUIRootController.combatRoot -> Combat UI root`
- `GameUIRootController.rewardRoot -> RewardUIPanel root`
- `CombatEntryPoint.director -> CombatDirector`
- `CombatEntryPoint.flowOrchestrator -> CombatFlowOrchestrator`
- `CombatRewardUIBinder.rewardService -> RewardService`
- `CombatRewardUIBinder.rewardPanel -> RewardUIPanel`
- `RewardService.currencyWallet -> CurrencyWallet`
- `RewardService.inventoryService -> InventoryService`
- `DailyFlowController.calendarService -> CalendarService`
- `DaySettlementFlow.dailyFlowController -> DailyFlowController`
- `DaySettlementFlow.calendarService -> CalendarService`
- `OfficeFlowController.dailyFlowController -> DailyFlowController`
- `OfficeFlowController.missionSelectFlow -> MissionSelectFlow`
- `OfficeFlowController.preMissionSupplyFlow -> PreMissionSupplyFlow`
- `OfficeFlowController.sceneFlowController -> SceneFlowController`
- `MissionSelectPanel.missionSelectFlow -> MissionSelectFlow`
- `DaySettlementPanel.daySettlementFlow -> DaySettlementFlow`
- `ShopService.currencyWallet -> CurrencyWallet`
- `ShopService.inventoryService -> InventoryService`
- `PreMissionSupplyFlow.supplyLoadoutService -> SupplyLoadoutService`

## 5. Optional References

Optional but useful:

- `OfficeFlowController.missionSelectPanel`
- `MissionSelectPanel.missionButtonTemplate`
- `ShopService.shopInventory`
- `ShopService.priceRule`
- `QuestCompletionFlow.rewardService`
- `QuestCompletionFlow.daySettlementFlow`
- `MissionCompletionController.daySettlementFlow`
- `DaySettlementPanel.confirmButton`
- `MissionSelectPanel.closeButton`
- `SaveLoadService.saveManager`

Leave unset for now:

- Shop/Supply UI panel references, because those panels are not implemented.
- Character progression service references, because no final owner exists.
- Party/equipment references, because that system is not implemented.

## 6. Scene Setup Order

1. Create `RuntimeRoot` and child roots.
2. Add `GameStateMachine` first.
3. Add `SceneFlowController`.
4. Add `RewardRoot` with `CurrencyWallet`, `InventoryService`, and `RewardService`.
5. Add `DailyRoot` with calendar/daily/settlement components.
6. Add `QuestRoot` and assign test quest definitions.
7. Add `CombatRoot` and wire combat entry/HUD/reward binder.
8. Add `OfficeRoot` and assign `MissionSelectFlow` mission entries.
9. Add `ShopSupplyRoot` and wire supply/shop services.
10. Add `GameUIRoot` and assign root panels.
11. Add player and field encounter test objects.
12. Add debug tools only after the core route works.
13. Press Play and validate root initialization before clicking any flow buttons.

## 7. Minimal Play Mode Test Route

Office route:

```text
Start scene
-> confirm GameStateMachine.Current is Exploration
-> call OfficeFlowController.EnterOffice()
-> call OfficeFlowController.OpenMissionSelectPanel()
-> select a mission
-> if waitForSupplyBeforeFieldLoad is false: field scene load starts
-> if true: call PreMissionSupplyFlow.CompleteSupplyStep()
-> field scene load starts
```

Combat route:

```text
Enter field scene
-> trigger CombatEncounterTrigger2D or PlayerFieldAttackController
-> CombatEntryPoint.StartCombat(...)
-> GameStateMachine.Current becomes CombatPlanning
-> planning HUD appears
-> complete combat
-> RewardService grants reward once
-> RewardUIPanel displays result
```

Settlement route:

```text
Complete QuestRuntime quest
-> QuestCompletionFlow grants reward
-> optional DaySettlementFlow.PrepareSettlement(...)
-> DaySettlementPanel displays result when subscribed
-> panel confirm calls DaySettlementFlow.CompleteSettlement() only if explicitly configured
```

Save snapshot route:

```text
Call SaveLoadService.CaptureGameSaveDataSnapshot()
-> verify quest, currency, inventory, daily, settlement, selected mission, and supply fields are populated
```

## 8. Common Failure Symptoms

- Mission select click does nothing: `OfficeFlowController` is not subscribed to `MissionSelectPanel`, or `MissionSelectPanel.forwardSelectionToFlow`/Office subscription is not configured.
- Mission panel disappears permanently: `MissionSelectPanel.root` is missing and scene disables the panel object externally.
- Field load does not start: `waitForSupplyBeforeFieldLoad` is enabled and `PreMissionSupplyFlow.CompleteSupplyStep()` was not called.
- Field load warns about spawn: mission has spawn id, but `SceneFlowController` does not support spawn routing.
- Shop purchase fails: missing `CurrencyWallet`, missing `InventoryService`, insufficient gold, or item id not in shop data.
- Combat does not start: GameState is not `Exploration`, duplicate combat active, or combat field objects lack required adapters.
- Combat UI does not show: `UIScreenRouter` or `GameUIRootController.combatRoot` missing/misassigned.
- Reward not granted: `RewardService` missing wallet/inventory or combat result source duplicate-blocked.
- Settlement not shown: settlement notifications are disabled or `DaySettlementPanel` is not subscribed.
- Save snapshot missing data: provider object is absent, disabled incorrectly, or not implementing provider/consumer.

## 9. Debug Checklist

- Confirm only one active `GameStateMachine`.
- Confirm only one active `SceneFlowController`.
- Confirm only one active `RewardService`, `CurrencyWallet`, `InventoryService`, `CalendarService`, and `DaySettlementFlow`.
- Confirm `GameStateMachine.Current` before and after combat start.
- Confirm `CalendarService.CurrentPhase` after Office, MissionSelect, Field, Settlement transitions.
- Confirm mission ids are stable and not duplicated.
- Confirm mission target scene names exist in Build Settings.
- Confirm `MissionSelectPanel.root` is assigned.
- Confirm `waitForSupplyBeforeFieldLoad` matches the test route.
- Confirm `PreMissionSupplyFlow.OnSupplyCompleted` reaches `OfficeFlowController`.
- Confirm `RewardService` grants once per combat.
- Confirm `QuestRuntime.OnQuestCompleted` fires once.
- Confirm `DaySettlementFlow` blocks duplicate settlement ids.
- Confirm `SaveLoadService.CaptureGameSaveDataSnapshot()` sees expected providers.
- Keep legacy `SceneTravelService` and production `SceneFlowController` routes visually separate during testing.
