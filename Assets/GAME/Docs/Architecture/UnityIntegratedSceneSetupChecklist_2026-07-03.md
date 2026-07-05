# Unity Integrated Scene Setup Checklist - 2026-07-03

## 1. Goal of the Test Scene

Create a dedicated Unity scene for Integration Checkpoint 1 wiring validation.

The goal is to verify that the existing backbone systems can coexist in one scene:

- Core state and scene loading
- Combat entry and reward display
- QuestRuntime progress
- Daily phase and settlement data
- Office mission selection
- Optional pre-mission supply delay
- Shop purchase validation through existing currency/inventory services
- Save snapshot capture through existing save provider interfaces

Do not use this scene to replace existing Title, CaseFile, DemoMission, or production gameplay scenes yet.

## 2. Required Scene Objects

Create a new scene such as:

```text
Assets/GAME/Scenes/IntegrationCheckpoint1.unity
```

Required high-level objects:

- `RuntimeRoot`
- `CoreRoot`
- `GameStateRoot`
- `InputRoot`
- `CombatRoot`
- `QuestRoot`
- `RewardRoot`
- `DailyRoot`
- `OfficeRoot`
- `ShopSupplyRoot`
- `SaveLoadRoot`
- `GameUIRoot`
- `PlayerRoot`
- `FieldRoot`
- `DebugRoot`

Keep all new objects scene-local for this test. Do not modify existing prefabs unless you intentionally make a duplicate test prefab.

## 3. Exact Hierarchy Structure

Create this exact hierarchy in the Unity Hierarchy:

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
  - Canvas
    - TitleRoot
    - FieldRootUI
    - DialogueRoot
    - ChoiceRoot
    - CombatRootUI
      - CombatPlanningPanel
    - RewardRootUI
      - RewardPanelRoot
    - MissionSelectRoot
      - MissionRows
      - MissionButtonTemplate
      - CloseButton
      - EmptyText
      - TitleText
    - DaySettlementRoot
      - SettlementRewardRows
      - ConfirmButton
      - SettlementCloseButton
      - SettlementTitleText
      - SettlementRewardSummaryText
- PlayerRoot
- FieldRoot
  - TestEnemy
  - TestEncounterTrigger
- DebugRoot
```

Notes:

- `FieldRootUI` is a UI root name. It is separate from the world `FieldRoot`.
- `MissionButtonTemplate` can be inactive after assignment.
- If you already have working UI prefabs, instantiate copies under `GameUIRoot/Canvas` and wire those instead.

## 4. Component-by-Component Setup

### RuntimeRoot

Checklist:

- [ ] Create empty GameObject named `RuntimeRoot`.
- [ ] Put all root objects under it.
- [ ] Add no components directly to `RuntimeRoot`.

### GameStateRoot

Checklist:

- [ ] Create child `GameStateRoot`.
- [ ] Add `GameStateMachine`.
- [ ] Confirm there is no other active `GameStateMachine` in the scene.

Expected initial state:

- `GameStateMachine.Current` defaults to `Exploration`.

### CoreRoot

Checklist:

- [ ] Create child `CoreRoot`.
- [ ] Add `RuntimeBootstrapper`.
- [ ] Add `GameFlowController`.
- [ ] Add `SceneFlowController`.
- [ ] Confirm there is no other active `SceneFlowController` in the scene.

Inspector:

- `RuntimeBootstrapper.initialStateMode`: keep default unless testing a specific state.
- `RuntimeBootstrapper.applyInitialStateOnStart`: keep enabled for this test.
- `RuntimeBootstrapper.createMissingCoreServices`: optional; prefer explicit scene objects in this test.

### InputRoot

Checklist:

- [ ] Create child `InputRoot`.
- [ ] Add `InputService`.
- [ ] Add `InputRouter`.
- [ ] Add `GameInputInstaller`.
- [ ] Optional: add `InputDeviceWatcher`.
- [ ] Optional: add `RebindSaveLoad`.

Inspector:

- Use existing project/prefab wiring for `GameInputInstaller` if available.
- If unsure, leave InputRoot out of the first non-interactive validation pass and use Inspector button calls/debug components for flow tests.

Do not:

- Do not remove old player input bridge components from existing prefabs during this checklist.

### RewardRoot

Checklist:

- [ ] Create child `RewardRoot`.
- [ ] Add `CurrencyWallet`.
- [ ] Add `InventoryService`.
- [ ] Add `RewardService`.

Inspector:

- `RewardService.currencyWallet` -> drag `RewardRoot/CurrencyWallet`.
- `RewardService.inventoryService` -> drag `RewardRoot/InventoryService`.
- On `CurrencyWallet`, set starting gold to a test value such as `500`.

Can remain null:

- None recommended for this integrated test; wire both wallet and inventory explicitly.

### DailyRoot

Checklist:

- [ ] Create child `DailyRoot`.
- [ ] Add `CalendarService`.
- [ ] Add `DailyFlowController`.
- [ ] Add `DaySettlementFlow`.

Inspector:

- `DailyFlowController.calendarService` -> `CalendarService`.
- `DailyFlowController.advanceDayWhenBeginningNewDay`: keep default unless specifically testing day advance.
- `DaySettlementFlow.dailyFlowController` -> `DailyFlowController`.
- `DaySettlementFlow.calendarService` -> `CalendarService`.
- `DaySettlementFlow.enterSettlementPhaseOnRequest`: keep disabled for first test.

Can remain null:

- None recommended in the integrated test scene.

### QuestRoot

Checklist:

- [ ] Create child `QuestRoot`.
- [ ] Add `QuestRuntime`.
- [ ] Add `QuestObjectiveTracker`.
- [ ] Add `QuestCompletionFlow`.

Inspector:

- `QuestRuntime.questDefinitions` -> assign one test `QuestDefinitionSO`.
- `QuestObjectiveTracker.questRuntime` -> `QuestRuntime`.
- `QuestCompletionFlow.questRuntime` -> `QuestRuntime`.
- `QuestCompletionFlow.rewardService` -> `RewardService`.
- `QuestCompletionFlow.daySettlementFlow` -> `DaySettlementFlow` if testing settlement.
- `QuestCompletionFlow.grantRewardOnCompletion`: enabled if testing reward grant.
- `QuestCompletionFlow.notifyDaySettlementOnQuestCompletion`: disabled for first smoke test, enabled for settlement test.

Can remain null:

- `QuestRuntime.missionManager` can remain null for pure `QuestDefinitionSO` event tests.
- `QuestCompletionFlow.daySettlementFlow` can remain null if settlement notification is disabled.

### CombatRoot

Checklist:

- [ ] Create child `CombatRoot`.
- [ ] Add `CombatEntryPoint`.
- [ ] Add `CombatFlowOrchestrator`.
- [ ] Add `CombatDirector`.
- [ ] Add `CombatStateSyncer`.
- [ ] Add `CombatFormationManager`.
- [ ] Add `CombatRewardUIBinder`.
- [ ] Optional: add `CombatUIRootController`.
- [ ] Optional: add `CombatPlanningHUD`.

Inspector:

- `CombatEntryPoint.director` -> `CombatDirector`.
- `CombatEntryPoint.flowOrchestrator` -> `CombatFlowOrchestrator`.
- `CombatEntryPoint.skillDefinitions` -> assign at least one existing `SkillDefinitionSO` if combat planning needs skills.
- `CombatFlowOrchestrator.entryPoint` -> `CombatEntryPoint`.
- `CombatDirector.entryPoint` -> `CombatEntryPoint`.
- `CombatStateSyncer.entryPoint` -> `CombatEntryPoint`.
- `CombatFormationManager.entryPoint` -> `CombatEntryPoint`.
- `CombatRewardUIBinder.entryPoint` -> `CombatEntryPoint`.
- `CombatRewardUIBinder.rewardService` -> `RewardService`.
- `CombatRewardUIBinder.rewardPanel` -> `RewardUIPanel`.
- `CombatRewardUIBinder.countEnemyDefeatOnVictory`: keep default for DemoMission compatibility checks.
- `CombatRewardUIBinder.restoreExplorationAfterRewardClosed`: keep default for first test.

CombatPlanningHUD fields if used:

- `entryPoint` -> `CombatEntryPoint`
- `flowOrchestrator` -> `CombatFlowOrchestrator`
- `panelPlanning` -> `CombatPlanningPanel`
- `skillListRoot` -> create child RectTransform under `CombatPlanningPanel`
- `targetListRoot` -> create child RectTransform under `CombatPlanningPanel`
- `buttonPrefab` -> simple UI Button prefab or copied Button from scene
- `confirmButton` -> UI Button
- `errorText` -> TMP text or leave null if not testing UI text
- `statusText` -> legacy Text or leave null if not testing UI text

Can remain null:

- `CombatEntryPoint.skillDefinitions` can be empty for minimal boot checks, but actual combat planning may show no available skills.
- Combat HUD references can remain null if the first test only validates owner initialization.

### OfficeRoot

Checklist:

- [ ] Create child `OfficeRoot`.
- [ ] Add `OfficeFlowController`.
- [ ] Add `MissionSelectFlow`.

Inspector:

- `OfficeFlowController.dailyFlowController` -> `DailyFlowController`.
- `OfficeFlowController.missionSelectFlow` -> `MissionSelectFlow`.
- `OfficeFlowController.missionSelectPanel` -> `MissionSelectPanel`.
- `OfficeFlowController.preMissionSupplyFlow` -> `PreMissionSupplyFlow`.
- `OfficeFlowController.sceneFlowController` -> `SceneFlowController`.
- `OfficeFlowController.enterOfficeOnEnable`: optional; keep disabled for manual test order.
- `OfficeFlowController.loadMissionSceneOnSelection`: enable if testing actual field scene loading.
- `OfficeFlowController.waitForSupplyBeforeFieldLoad`: enable only when testing Supply handoff.
- `MissionSelectFlow.missionBoard` -> assign a test `MissionBoardDefinitionSO`, or use local entries.
- `MissionSelectFlow.missionEntries`: add at least one local test mission if no mission board asset exists.

Minimal local mission entry:

- `missionId`: `test_mission_001`
- `title`: `Test Mission`
- `description`: `Integration checkpoint mission`
- `targetFieldSceneName`: use an existing scene name in Build Settings
- `targetSpawnPointId`: leave blank for now
- `unlocked`: enabled
- `questDefinition`: assign test quest if available
- `caseFile`: leave null for this route

Can remain null:

- `MissionSelectFlow.missionBoard` can remain null if local entries are configured.
- `OfficeFlowController.preMissionSupplyFlow` can remain null if `waitForSupplyBeforeFieldLoad` is disabled.

### ShopSupplyRoot

Checklist:

- [ ] Create child `ShopSupplyRoot`.
- [ ] Add `ShopService`.
- [ ] Add `SupplyLoadoutService`.
- [ ] Add `PreMissionSupplyFlow`.

Inspector:

- `ShopService.shopInventory` -> assign a test `ShopInventorySO`, or use local items.
- `ShopService.currencyWallet` -> `CurrencyWallet`.
- `ShopService.inventoryService` -> `InventoryService`.
- `ShopService.priceRule`: optional.
- `PreMissionSupplyFlow.supplyLoadoutService` -> `SupplyLoadoutService`.

Minimal local shop item:

- `itemId`: `potion_small`
- `displayName`: `Small Potion`
- `price`: `10`
- `quantity`: `1`
- `unlocked`: enabled

Can remain null:

- `ShopService.shopInventory` can remain null if local items are configured.
- `ShopService.priceRule` can remain null.

### SaveLoadRoot

Checklist:

- [ ] Create child `SaveLoadRoot`.
- [ ] Add `SaveLoadService`.
- [ ] Optional: add or reference existing `SaveManager` only if testing legacy save/load.

Inspector:

- `SaveLoadService.saveManager` -> optional `SaveManager`.

Can remain null:

- `saveManager` can remain null if testing only `CaptureGameSaveDataSnapshot()` through debug/Inspector tooling.

### GameUIRoot

Checklist:

- [ ] Create child `GameUIRoot`.
- [ ] Add `GameUIRootController`.
- [ ] Add `UIScreenRouter`.
- [ ] Under `GameUIRoot`, create a `Canvas`.
- [ ] Add `MissionSelectPanel` to `MissionSelectRoot` or to a parent object that stays active.
- [ ] Add `DaySettlementPanel` to `DaySettlementRoot` or to a parent object that stays active.
- [ ] Add `RewardUIPanel` to `RewardPanelRoot` or to a parent object that stays active.

Inspector:

- `UIScreenRouter.uiRoot` -> `GameUIRootController`.
- `UIScreenRouter.stateMachine` -> `GameStateMachine`.
- `GameUIRootController.titleRoot` -> `TitleRoot`.
- `GameUIRootController.fieldRoot` -> `FieldRootUI`.
- `GameUIRootController.dialogueRoot` -> `DialogueRoot`.
- `GameUIRootController.choiceRoot` -> `ChoiceRoot`.
- `GameUIRootController.combatRoot` -> `CombatRootUI`.
- `GameUIRootController.rewardRoot` -> `RewardRootUI`.
- `GameUIRootController.pauseRoot` -> optional pause root.
- `GameUIRootController.loadingRoot` -> optional loading root.

MissionSelectPanel fields:

- `root` -> `MissionSelectRoot`.
- `missionRowRoot` -> `MissionRows`.
- `missionButtonTemplate` -> `MissionButtonTemplate` Button.
- `titleText` -> `TitleText` TMP text.
- `emptyText` -> `EmptyText` TMP text.
- `closeButton` -> `CloseButton`.
- `missionSelectFlow` -> `MissionSelectFlow`.
- `subscribeToMissionSelectFlow`: optional; keep disabled if `OfficeFlowController.OpenMissionSelectPanel()` calls `Show(...)`.
- `forwardSelectionToFlow`: usually disabled when `OfficeFlowController` subscribes to panel selection.

DaySettlementPanel fields:

- `root` -> `DaySettlementRoot`.
- `titleText` -> `SettlementTitleText`.
- `rewardSummaryText` -> `SettlementRewardSummaryText`.
- `rewardRowRoot` -> `SettlementRewardRows`.
- `confirmButton` -> `ConfirmButton`.
- `closeButton` -> `SettlementCloseButton`.
- `daySettlementFlow` -> `DaySettlementFlow`.
- `subscribeToSettlementFlow`: enable only when testing settlement display.
- `completeSettlementOnConfirm`: enable only when testing settlement completion.

RewardUIPanel fields:

- `root` -> `RewardPanelRoot`.
- `titleText` -> TMP title text child.
- `resultText` -> TMP result text child, optional.
- `rewardRowRoot` -> child RectTransform for rows.
- `closeButton` -> reward close button.

Can remain null:

- Legacy `Text` fields can remain null if TMP fields are assigned.
- Button templates can remain null for some panels, but MissionSelectPanel is easier to inspect with a template.
- Shop/Supply panel references do not exist yet.

### PlayerRoot

Checklist:

- [ ] Create or instantiate a test player under `PlayerRoot`.
- [ ] Ensure it has the current active movement/input components used by the scene.
- [ ] Add `PlayerFieldAttackController` if testing field attack combat start.

Inspector:

- `PlayerFieldAttackController.entryPoint` -> `CombatEntryPoint`.
- `PlayerFieldAttackController.attackOrigin` -> player attack origin Transform.
- `PlayerFieldAttackController.targetMask` -> layer containing test enemy.
- `PlayerFieldAttackController.enemyTag` -> `Enemy` if using tag detection.

Can remain null:

- `openingEffectOrNull` can remain null.
- Animation references can remain null for logic-only combat start tests, but expect missing animation behavior.

### FieldRoot

Checklist:

- [ ] Create `TestEnemy`.
- [ ] Add `CombatHpComponent`.
- [ ] Add `CombatSkillLoadoutComponent` or ensure fallback skill setup is valid.
- [ ] Add `CombatKeywordComponent`.
- [ ] Add collider/rigidbody as required by your encounter route.
- [ ] Create `TestEncounterTrigger`.
- [ ] Add `CombatEncounterTrigger2D`.

Inspector:

- `CombatEncounterTrigger2D.entryPoint` -> `CombatEntryPoint`.
- `CombatEncounterTrigger2D.enemyObject` -> `TestEnemy`, or assign `encounterGroup`.
- `CombatEncounterTrigger2D.startReason` -> keep default or select desired start reason.
- `CombatEncounterTrigger2D.initiativeSide` -> `Allies` for first test.
- `CombatEncounterTrigger2D.playerTag` -> `Player`.

Can remain null:

- `openingEffectOrNull` can remain null.
- `encounterGroup` can remain null if `enemyObject` is assigned.

### DebugRoot

Checklist:

- [ ] Keep empty for first pass.
- [ ] Add debug components only after the normal route is wired.

Do not:

- Do not use debug combat start as proof that field encounter wiring works.

## 5. Inspector Fields to Assign

Assign these minimum fields before the first Play Mode run:

- `UIScreenRouter.uiRoot`
- `UIScreenRouter.stateMachine`
- `GameUIRootController.fieldRoot`
- `GameUIRootController.combatRoot`
- `GameUIRootController.rewardRoot`
- `CombatEntryPoint.director`
- `CombatEntryPoint.flowOrchestrator`
- `CombatRewardUIBinder.entryPoint`
- `CombatRewardUIBinder.rewardPanel`
- `CombatRewardUIBinder.rewardService`
- `RewardService.currencyWallet`
- `RewardService.inventoryService`
- `DailyFlowController.calendarService`
- `DaySettlementFlow.dailyFlowController`
- `DaySettlementFlow.calendarService`
- `QuestObjectiveTracker.questRuntime`
- `QuestCompletionFlow.questRuntime`
- `QuestCompletionFlow.rewardService`
- `OfficeFlowController.dailyFlowController`
- `OfficeFlowController.missionSelectFlow`
- `OfficeFlowController.sceneFlowController`
- `MissionSelectPanel.root`
- `MissionSelectPanel.missionRowRoot`
- `MissionSelectPanel.missionSelectFlow`
- `ShopService.currencyWallet`
- `ShopService.inventoryService`
- `PreMissionSupplyFlow.supplyLoadoutService`

## 6. Fields That Can Remain Null

Safe for first integrated scene pass:

- `OfficeFlowController.preMissionSupplyFlow` if `waitForSupplyBeforeFieldLoad` is disabled.
- `OfficeFlowController.missionSelectPanel` if testing event-only mission selection.
- `MissionSelectFlow.missionBoard` if local `missionEntries` are configured.
- `ShopService.shopInventory` if local `localItems` are configured.
- `ShopService.priceRule`.
- `QuestRuntime.missionManager` for pure `QuestDefinitionSO` tests.
- `QuestCompletionFlow.daySettlementFlow` if settlement notification is disabled.
- `DaySettlementPanel.daySettlementFlow` if settlement panel is not tested.
- `DaySettlementPanel.confirmButton` if not testing panel-driven completion.
- `CombatEncounterTrigger2D.openingEffectOrNull`.
- `PlayerFieldAttackController.openingEffectOrNull`.
- Legacy `Text` fields when TMP fields are assigned.
- `SaveLoadService.saveManager` when not testing legacy save/load.

Not safe to leave null for the relevant feature:

- `MissionSelectPanel.root` when displaying mission UI.
- `RewardService.currencyWallet` and `inventoryService` when testing rewards/shop.
- `OfficeFlowController.sceneFlowController` when testing field scene load.
- `PreMissionSupplyFlow.supplyLoadoutService` when testing supply.
- `CombatRewardUIBinder.rewardService` when testing reward grant.
- `CombatRewardUIBinder.rewardPanel` when testing reward display.

## 7. Required ScriptableObject Assets to Create

Create these test assets only if existing assets are not suitable.

### QuestDefinitionSO

Path suggestion:

```text
Assets/GAME/TestData/Integration/Quest_IntegrationTest.asset
```

Values:

- `questId`: `test_mission_001`
- `questTitle`: `Integration Test Mission`
- `description`: `Used for Integration Checkpoint 1 scene validation.`
- `rewardGold`: `50`
- `rewardExp`: `10`
- `objectives`: optional for first mission select test; add one objective if testing QuestRuntime events.

If adding one objective:

- `objectiveId`: `enemy_defeated`
- `eventType`: `Kill`
- `requiredCount`: `1`
- `optional`: false
- `description`: `Defeat one enemy.`

### MissionBoardDefinitionSO

Path suggestion:

```text
Assets/GAME/TestData/Integration/MissionBoard_IntegrationTest.asset
```

Add one mission:

- `missionId`: `test_mission_001`
- `title`: `Integration Test Mission`
- `description`: `Select this to test OfficeFlowController.`
- `targetFieldSceneName`: an existing test field scene name in Build Settings
- `targetSpawnPointId`: leave empty
- `unlocked`: true
- `questDefinition`: `Quest_IntegrationTest`
- `caseFile`: none

### ShopInventorySO

Path suggestion:

```text
Assets/GAME/TestData/Integration/ShopInventory_IntegrationTest.asset
```

Add one item:

- `itemId`: `potion_small`
- `displayName`: `Small Potion`
- `price`: `10`
- `quantity`: `1`
- `unlocked`: true

### SkillDefinitionSO

Use an existing combat skill asset if possible.

Only create a new one if the combat planning test has no available skills. Fill only fields required by the current `SkillDefinitionSO` Inspector and keep it minimal.

## 8. Minimal UI Objects to Create

Under `GameUIRoot/Canvas`, create these minimum UI objects.

Mission Select:

- `MissionSelectRoot`: RectTransform or GameObject root.
- `TitleText`: TextMeshProUGUI.
- `EmptyText`: TextMeshProUGUI.
- `MissionRows`: RectTransform with VerticalLayoutGroup if desired.
- `MissionButtonTemplate`: Button with child TextMeshProUGUI.
- `CloseButton`: Button.

Day Settlement:

- `DaySettlementRoot`: RectTransform or GameObject root.
- `SettlementTitleText`: TextMeshProUGUI.
- `SettlementRewardSummaryText`: TextMeshProUGUI.
- `SettlementRewardRows`: RectTransform with VerticalLayoutGroup if desired.
- `ConfirmButton`: Button.
- `SettlementCloseButton`: Button.

Reward:

- `RewardPanelRoot`: RectTransform or GameObject root.
- Reward title TMP text.
- Reward result TMP text, optional.
- Reward row root RectTransform.
- Reward close Button.

Combat:

- `CombatRootUI`: GameObject root.
- `CombatPlanningPanel`: GameObject root.
- Skill list RectTransform.
- Target list RectTransform.
- Confirm Button.
- Button prefab or scene Button for combat action rows.

Do not create ShopPanel or SupplyPanel in this task. They do not exist yet.

## 9. Minimal Test Data to Enter

Currency:

- Set `CurrencyWallet.gold` to `500`.

Mission:

- Use `test_mission_001`.
- Use a real scene name in `targetFieldSceneName`.
- Leave `targetSpawnPointId` empty until `SceneFlowController` supports spawn routing.

Shop:

- Add `potion_small`, price `10`, quantity `1`.

Supply:

- Use `potion_small` as the first supply loadout item.
- Add it through `PreMissionSupplyFlow.AddSupplyItem("potion_small", 1)` from a temporary debug button or Inspector invocation if available.

Daily:

- `CalendarService.currentDay`: `1`
- `CalendarService.currentWeek`: `1`
- `CalendarService.currentPhase`: `None` before flow begins.

Quest:

- Test quest id should match mission id: `test_mission_001`.

## 10. Play Mode Test Order

Run tests in this order.

### Test 1: Root Boot

- [ ] Press Play.
- [ ] Confirm no duplicate singleton destroy warnings.
- [ ] Confirm `GameStateMachine.Current` is `Exploration`.
- [ ] Confirm `CalendarService.CurrentPhase` is `None` or expected test value.

### Test 2: Office Phase

- [ ] Call `OfficeFlowController.EnterOffice()`.
- [ ] Confirm `CalendarService.CurrentPhase` becomes `Office`.

### Test 3: Mission Select Without Supply

- [ ] Disable `OfficeFlowController.waitForSupplyBeforeFieldLoad`.
- [ ] Call `OfficeFlowController.OpenMissionSelectPanel()`.
- [ ] Confirm `MissionSelectRoot` displays.
- [ ] Click test mission.
- [ ] Confirm selected mission is stored.
- [ ] If `loadMissionSceneOnSelection` is enabled, confirm field scene loading starts.

### Test 4: Mission Select With Supply Delay

- [ ] Enable `OfficeFlowController.waitForSupplyBeforeFieldLoad`.
- [ ] Ensure `preMissionSupplyFlow` is assigned.
- [ ] Call `OfficeFlowController.OpenMissionSelectPanel()`.
- [ ] Click test mission.
- [ ] Confirm field scene does not load immediately.
- [ ] Call `PreMissionSupplyFlow.AddSupplyItem("potion_small", 1)`.
- [ ] Call `PreMissionSupplyFlow.CompleteSupplyStep()`.
- [ ] Confirm `OfficeFlowController.EnterSelectedMissionField()` runs and scene loading starts.

### Test 5: Shop Purchase

- [ ] Confirm `CurrencyWallet.gold` is at least `10`.
- [ ] Call `ShopService.Purchase(...)` for `potion_small`, quantity `1`.
- [ ] Confirm gold decreases by `10`.
- [ ] Confirm `InventoryService.GetCount("potion_small")` increases.

### Test 6: Combat Entry

- [ ] Enter or remain in a field scene with player and test enemy.
- [ ] Trigger `CombatEncounterTrigger2D` or `PlayerFieldAttackController`.
- [ ] Confirm `GameStateMachine.Current` becomes `CombatPlanning`.
- [ ] Confirm combat UI appears.

### Test 7: Reward

- [ ] Complete combat using existing flow or debug finish.
- [ ] Confirm `RewardService` grants reward once.
- [ ] Confirm `RewardUIPanel` displays.

### Test 8: Settlement

- [ ] Enable `QuestCompletionFlow.notifyDaySettlementOnQuestCompletion`.
- [ ] Complete the test quest.
- [ ] Confirm `DaySettlementFlow.OnSettlementReady` fires.
- [ ] If `DaySettlementPanel.subscribeToSettlementFlow` is enabled, confirm panel displays.

### Test 9: Save Snapshot

- [ ] Call `SaveLoadService.CaptureGameSaveDataSnapshot()`.
- [ ] Confirm snapshot contains:
  - gold
  - inventory item counts
  - quest progress
  - daily phase/day/week
  - selected mission id
  - selected mission target scene
  - selected supply item ids/counts
  - completed settlement ids after settlement completion

## 11. Expected Console Logs

Expected or acceptable:

- `[CombatEntryPoint] Combat started...`
- `[Currency] Gold -10 => ...` during shop purchase.
- `[Inventory] potion_small +1 => ...` during shop purchase.
- `[RewardService] EXP ... CharacterProgressionService is not implemented yet.` if EXP is granted.
- `[OfficeFlowController] Selected mission has a spawn point id...` only if a spawn id is set.
- `Duplicate mission id ignored...` only if intentionally testing duplicate mission ids.
- `Duplicate settlement request blocked...` only if intentionally testing duplicate settlement requests.

Unexpected and should be investigated:

- Missing `GameStateMachine`.
- Missing `SceneFlowController`.
- Missing `MissionSelectFlow`.
- Missing `CurrencyWallet` during shop purchase.
- Missing `InventoryService` during shop purchase.
- Supply confirmed with no selected mission.
- Combat start blocked while state is expected to be `Exploration`.
- No save data providers found.

## 12. Failure Symptoms and Likely Cause

| Symptom | Likely cause |
|---|---|
| Mission panel does not open | `MissionSelectPanel.root` missing, panel object disabled, or `OfficeFlowController.missionSelectPanel` not assigned |
| Mission click does nothing | `OfficeFlowController` is not subscribed, `MissionSelectFlow` missing, mission id invalid |
| Mission list is empty | no `MissionBoardDefinitionSO`, no local entries, entries locked, or blank mission ids |
| Field loads immediately despite supply test | `waitForSupplyBeforeFieldLoad` is disabled |
| Field never loads after mission selection | `waitForSupplyBeforeFieldLoad` enabled but `CompleteSupplyStep()` not called |
| Supply completion warns | no selected mission was passed to `PreMissionSupplyFlow` |
| Shop purchase fails | missing wallet/inventory, insufficient gold, item not unlocked or not found |
| Combat start blocked | GameState is not `Exploration`, active combat already exists, duplicate trigger fired |
| Combat starts but no skills | `CombatEntryPoint.skillDefinitions` empty or player/enemy loadout missing |
| Combat UI does not show | `GameUIRootController.combatRoot` not assigned or combat HUD references missing |
| Reward UI does not show | `CombatRewardUIBinder.rewardPanel` missing or reward root hidden |
| Settlement panel does not show | settlement notifications disabled or `DaySettlementPanel.subscribeToSettlementFlow` disabled |
| Save snapshot missing daily data | `CalendarService` not present or inactive in scene |
| Save snapshot missing supply data | `SupplyLoadoutService` not present or no selected supply items |

## 13. What Not to Test Yet

Do not test these in this checkpoint scene:

- Full Title replacement.
- Full Office loop with story CaseFile replacement.
- Shop UI.
- Supply UI.
- Party management.
- Equipment/loadout consumption.
- Calendar UI.
- Save slots or persistent `GameSaveData` disk path.
- Spawn point routing through `SceneFlowController`.
- Full DemoMission removal.
- Legacy battle route cleanup.
- Full reward table/economy balance.
- Character EXP progression.

## 14. Screenshots to Capture if Something Fails

Capture these screenshots before changing the scene:

1. Hierarchy expanded under `RuntimeRoot`.
2. Inspector for the object that owns the failing component.
3. Inspector for `GameStateRoot/GameStateMachine`.
4. Inspector for `OfficeRoot/OfficeFlowController`.
5. Inspector for `OfficeRoot/MissionSelectFlow`.
6. Inspector for `GameUIRoot/Canvas/MissionSelectRoot` and its `MissionSelectPanel`.
7. Inspector for `ShopSupplyRoot/PreMissionSupplyFlow`.
8. Inspector for `ShopSupplyRoot/SupplyLoadoutService`.
9. Inspector for `RewardRoot/RewardService`.
10. Inspector for `RewardRoot/CurrencyWallet` and `InventoryService`.
11. Inspector for `CombatRoot/CombatEntryPoint`.
12. Console window filtered to warnings/errors.
13. Game view showing visible UI state.
14. Scene view showing player/enemy/encounter trigger positions if combat fails.

Include the current Play Mode step number from this checklist when reporting the failure.
