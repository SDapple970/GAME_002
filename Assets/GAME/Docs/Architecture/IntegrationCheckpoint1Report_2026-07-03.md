# Integration Checkpoint 1 Report - 2026-07-03

## 1. Summary

Integration Checkpoint 1 audited the current GAME002 backbone after the combat entry, quest, reward, save/load, daily settlement, office mission select, and shop/supply phases.

No gameplay behavior was changed in this checkpoint. The work is documentation and integration planning only.

The current architecture has clear runtime owners for the main loops, but most new systems are not yet scene-wired or Play Mode validated. The safest next step is to create a dedicated integrated test scene/prefab hierarchy and wire existing optional references explicitly rather than relying on fallback discovery.

## 2. Compile Status

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

This matches the known Unity-generated project behavior recorded in prior reports: the available `dotnet` check reports no C# compile warnings or errors but exits nonzero.

Unity is not available on PATH in this shell, so Unity Play Mode validation was not run.

## 3. Files Inspected

Reports inspected:

- `FinalArchitectureAudit_2026-07-03.md`
- `FieldEnemyCombatEntryRefactorReport_2026-07-03.md`
- `CombatEntryStateInputUIValidationReport_2026-07-03.md`
- `DemoMissionToQuestPhase1Report_2026-07-03.md`
- `DemoMissionToQuestPhase2Report_2026-07-03.md`
- `RewardServiceConsolidationPhase1Report_2026-07-03.md`
- `RewardServiceConsolidationPhase2Report_2026-07-03.md`
- `SaveLoadPhase1Report_2026-07-03.md`
- `DailySettlementPhase1Report_2026-07-03.md`
- `DailySettlementPhase2Report_2026-07-03.md`
- `OfficeMissionSelectPhase1Report_2026-07-03.md`
- `OfficeMissionSelectPhase2Report_2026-07-03.md`
- `ShopSupplyPhase1Report_2026-07-03.md`

Runtime areas sampled:

- `Assets/GAME/Scripts/Core`
- `Assets/GAME/Scripts/Combat`
- `Assets/GAME/Scripts/Quest`
- `Assets/GAME/Scripts/DemoMission`
- `Assets/GAME/Scripts/Reward`
- `Assets/GAME/Scripts/NonCombat/Save`
- `Assets/GAME/Scripts/NonCombat/Inventory`
- `Assets/GAME/Scripts/Daily`
- `Assets/GAME/Scripts/Office`
- `Assets/GAME/Scripts/Shop`
- `Assets/GAME/Scripts/Supply`
- `Assets/GAME/Scripts/UI`

The task referenced `Assets/GAME/Scripts/Inventory`; this checkout stores the active inventory/currency services under `Assets/GAME/Scripts/NonCombat/Inventory`.

## 4. System Owner Map

| Area | Current owner | Status |
|---|---|---|
| GameState | `GameStateMachine` | Clear owner. Persistent singleton. |
| Scene loading | `SceneFlowController` | Clear owner for scene-name loads. `SceneTravelService` remains compatibility owner for spawn-point travel. |
| Combat entry | `CombatEntryPoint` | Clear production owner. Legacy/debug routes still exist by design. |
| Quest progress | `QuestRuntime` | Preferred owner. DemoMission remains compatibility adapter. |
| Reward application | `RewardService` | Clear owner for reward grants. Some older direct reward paths remain documented. |
| Save/load coordination | `SaveLoadService` | Future snapshot owner. Legacy disk save still delegates to `SaveManager`. |
| Calendar data | `CalendarService` | Clear owner for day/week/chapter/phase data. |
| Day phase transitions | `DailyFlowController` | Clear owner for daily phase methods. |
| Settlement preparation | `DaySettlementFlow` | Clear owner for settlement request/result preparation. |
| Office hub flow | `OfficeFlowController` | Clear owner for mission selection handoff and field entry. |
| Mission selection | `MissionSelectFlow` | Clear owner for mission list and selected mission result. |
| Shop purchase validation/application | `ShopService` | Clear owner for simple purchases through currency/inventory. |
| Supply loadout | `SupplyLoadoutService` | Clear owner for selected pre-mission supply item ids/counts. |
| Pre-mission supply step | `PreMissionSupplyFlow` | Clear owner for supply completion event. |
| Root UI routing | `UIScreenRouter` | Clear owner for `GameState` to major UI roots. Daily/Office panels are not fully routed yet. |

## 5. Current Production Flow Map

Combat start:

```text
FieldEnemy / CombatEncounterTrigger2D / PlayerFieldAttackController / TutorialBattleStartInteractionEventSO
-> CombatStartRequest
-> CombatEntryPoint.StartCombat(...)
-> CombatBootstrapper.StartCombat(...)
-> GameStateMachine.SetState(CombatPlanning)
-> CombatEntryPoint.OnCombatStarted
```

Combat reward:

```text
CombatEntryPoint.OnCombatEnded
-> CombatRewardUIBinder
-> RewardService.GrantReward(...)
-> CurrencyWallet / InventoryService / EXP placeholder
-> RewardUIPanel.Show(...)
```

Quest progress:

```text
DemoMissionRuntime / QuestEventChannel / direct quest APIs
-> QuestRuntime.ApplyEvent(...)
-> QuestRuntime.OnObjectiveProgressChanged / OnQuestCompleted
-> QuestCompletionFlow
```

Quest reward and optional settlement:

```text
QuestCompletionFlow
-> RewardService
-> optional DaySettlementFlow.PrepareSettlement(...)
```

Office mission selection:

```text
OfficeFlowController.OpenMissionSelectPanel()
-> MissionSelectFlow.GetAvailableMissions()
-> MissionSelectPanel.Show(...)
-> MissionSelectPanel.OnMissionSelected
-> OfficeFlowController.SelectMission(...)
-> MissionSelectFlow.SelectMission(...)
-> OfficeFlowController.ReceiveSelectedMission(...)
```

Optional supply step:

```text
OfficeFlowController.ReceiveSelectedMission(...)
-> PreMissionSupplyFlow.SetSelectedMission(...)
-> SupplyLoadoutService Add/Remove/Clear
-> PreMissionSupplyFlow.CompleteSupplyStep()
-> OfficeFlowController.EnterSelectedMissionField()
-> SceneFlowController.LoadScene(...)
```

Save snapshot:

```text
SaveLoadService.CaptureGameSaveDataSnapshot()
-> all active/inactive MonoBehaviours implementing ISaveDataProvider
-> GameSaveData
```

## 6. Optional vs Required References

Safe to leave null for existing scenes:

- `DailyFlowController.calendarService` if a `CalendarService` exists in scene.
- `DaySettlementFlow.dailyFlowController` and `calendarService` if discoverable.
- `QuestCompletionFlow.daySettlementFlow` when settlement notifications are disabled.
- `MissionCompletionController.daySettlementFlow` when settlement notifications are disabled.
- `OfficeFlowController.missionSelectPanel` if using event-only mission selection.
- `OfficeFlowController.preMissionSupplyFlow` when `waitForSupplyBeforeFieldLoad` is false.
- `ShopService.currencyWallet` and `inventoryService` if the scene has singleton instances.
- `PreMissionSupplyFlow.supplyLoadoutService` if discoverable.
- UI panel roots for scenes that are not using those panels yet.

Required for actual integrated gameplay:

- One active `GameStateMachine`.
- One active `SceneFlowController` for new scene-name loading paths.
- One `CombatEntryPoint` with combat director/orchestrator/HUD dependencies wired or discoverable.
- `RewardService` with access to `CurrencyWallet` and `InventoryService`.
- `QuestRuntime` with relevant `QuestDefinitionSO` entries for Quest-backed progress.
- `CalendarService`, `DailyFlowController`, and `DaySettlementFlow` for daily/settlement flow.
- `OfficeFlowController`, `MissionSelectFlow`, and mission entries/board for Office mission selection.
- `PreMissionSupplyFlow` and `SupplyLoadoutService` if `waitForSupplyBeforeFieldLoad` is enabled.
- `GameUIRootController` and `UIScreenRouter` for root UI visibility.
- `MissionSelectPanel.root` when the panel is actively used, so hiding visuals does not disable the subscriber GameObject.

## 7. Duplicate Route Check

Scene loading:

- Production new route: `SceneFlowController.LoadScene(sceneName)`.
- Compatibility route: `SceneTravelService.TravelTo(sceneName, spawnPointId)`.
- Risk: `SceneFlowController` does not support spawn points yet; OfficeFlow warns and drops spawn routing.

Mission selection:

- New route: `MissionSelectFlow -> OfficeFlowController`.
- Existing case-file route: `CaseFileDocumentPanel -> SceneTravelService`.
- Risk: both can launch field/dungeon content if both are wired in the same Office UI without clear UX ownership.

Combat start:

- Production route: `CombatStartRequest -> CombatEntryPoint.StartCombat(...)`.
- Legacy/debug routes remain: legacy battle scripts and combat debug tools.
- Risk: scenes with both legacy battle triggers and production encounter triggers can start competing flows.

Reward grant:

- Production owner: `RewardService`.
- Remaining older/direct paths: `ChoiceRunner`, some search/interaction compatibility fallbacks, debug/test paths.
- Risk: content source can still bypass `RewardService` if older route is used.

Quest completion:

- Preferred owner: `QuestRuntime` plus `QuestCompletionFlow`.
- Compatibility owners: DemoMission and legacy Quest/Mission managers.
- Risk: duplicate completion signals are mostly guarded, but scene wiring should confirm only one presentation path fires.

Settlement creation:

- Owner: `DaySettlementFlow`.
- Producers: optional `QuestCompletionFlow` and `MissionCompletionController`.
- Risk: notifications are default disabled; enabling both must rely on id guards and should be Play Mode tested.

Save/load capture:

- Snapshot owner: `SaveLoadService`.
- Legacy disk owner: `SaveManager`.
- Risk: `SaveLoadService.Save()` still delegates to legacy `SaveManager`, while `CaptureGameSaveDataSnapshot()` uses the new provider contract.

## 8. Circular Dependency / Fallback Lookup Risks

Most new systems use optional references with fallback lookup. This preserves scenes but creates ordering and duplicate-root risks.

Notable lookup patterns:

- `GameStateMachine`, `SceneFlowController`, `RewardService`, `CalendarService`, and `DaySettlementFlow` use singleton-style `Instance`.
- `OfficeFlowController` looks up `DailyFlowController`, `MissionSelectFlow`, `MissionSelectPanel`, `PreMissionSupplyFlow`, and `SceneFlowController`.
- `PreMissionSupplyFlow` looks up `SupplyLoadoutService`.
- `ShopService` looks up `CurrencyWallet` and `InventoryService`.
- `UIScreenRouter` looks up `GameUIRootController` and `GameStateMachine`.
- `SaveLoadService` discovers all save providers/consumers in the scene.

Primary risks:

- duplicate singleton objects destroying themselves in `Awake`
- fallback resolving the wrong inactive object when multiple test roots exist
- event subscription order issues if root GameObjects are disabled
- save snapshot capturing debug/test providers in the same scene

No direct hard circular dependency was found in the new backbone, but `OfficeFlowController <-> PreMissionSupplyFlow` is an event loop by design: Office sends selected mission to supply; supply completion calls Office field entry.

## 9. Missing Scene Object Risks

Likely missing in current scenes until manually wired:

- `DailyRoot` containing `CalendarService`, `DailyFlowController`, `DaySettlementFlow`
- `OfficeRoot` containing `OfficeFlowController`, `MissionSelectFlow`
- `ShopSupplyRoot` containing `ShopService`, `SupplyLoadoutService`, `PreMissionSupplyFlow`
- `GameUIRoot` references for `MissionSelectPanel` and `DaySettlementPanel`
- `RewardRoot` with `RewardService`, `CurrencyWallet`, and `InventoryService` in scenes that test shop/reward
- `QuestRoot` with Quest definitions for non-demo mission progress
- `SaveLoadRoot` with `SaveLoadService`

Existing scenes can still run without these because the new systems are passive, but integrated flow testing requires these roots.

## 10. SaveLoad Integration Status

Implemented provider/consumer coverage:

- `CalendarService`: daily calendar data.
- `DaySettlementFlow`: completed settlement ids.
- `QuestRuntime`: quest state/objective progress.
- `CurrencyWallet`: gold.
- `InventoryService`: inventory item counts.
- `DemoMissionRuntime`: DemoMission snapshot provider only.
- `OfficeFlowController`: selected mission id/target scene/spawn.
- `SupplyLoadoutService`: selected supply item ids/counts.

Known gaps:

- `SaveLoadService.Save()` still delegates to legacy `SaveManager`.
- `GameSaveData` snapshot persistence to disk is not fully implemented.
- DemoMission restore is intentionally not implemented.
- No scene restore/active mission session restore exists.
- No save slots, migrations, or UI exist.

## 11. UI Routing Status

Current root routing:

- `UIScreenRouter` maps `GameState` to major roots in `GameUIRootController`.
- Combat, reward, dialogue, choice, pause, loading, title, and field roots can be toggled.

Current panel-local behavior:

- `MissionSelectPanel` is presentation-only and not fully routed by `UIScreenRouter`.
- `DaySettlementPanel` is presentation-only and not fully routed by `UIScreenRouter`.
- `RewardUIPanel` remains presentation-only for reward results.
- Shop/Supply UI panels do not exist yet.

Risk:

- GameState and DayPhase are separate state concepts. Do not route `DayPhase.MissionSelect` or `DayPhase.Settlement` by overloading `GameState.Reward` or `GameState.UIOnly` until a UI ownership decision is made.

## 12. Unity Play Mode Risk List

- Missing root objects prevent new systems from doing anything visible.
- Duplicate singleton roots can self-destroy and leave serialized references stale.
- `MissionSelectPanel` requires a child `root` for safe visual hiding.
- `MissionSelectFlow` requires stable mission ids and target scene names.
- `SceneFlowController` ignores spawn point ids.
- `PreMissionSupplyFlow` has no UI, so supply completion needs a temporary button/debug hook.
- `ShopService` purchase tests require both `CurrencyWallet` and `InventoryService`.
- `RewardService` EXP still has no progression owner.
- `QuestRuntime` definitions must match mission ids for production quest progress.
- `SaveLoadService.CaptureGameSaveDataSnapshot()` may include inactive/debug providers.
- Combat UI visibility may be affected by both `UIScreenRouter` and combat-local UI controllers.
- Existing DemoMission completion UI can still fire alongside QuestRuntime completion events if both are wired without testing.

Script/meta check:

- No missing `.cs.meta` pairs were found under `Assets/GAME/Scripts`.
- `git status --short` was clean before adding this checkpoint documentation.

## 13. Recommended Next Implementation Step

Recommended next task:

```text
Create an IntegrationTest scene or prefab root only, with no new gameplay logic.

Wire:
- RuntimeRoot/CoreRoot
- CombatRoot
- QuestRoot
- RewardRoot
- DailyRoot
- OfficeRoot
- ShopSupplyRoot
- SaveLoadRoot
- GameUIRoot

Then perform Unity Play Mode validation for:
Title/Office entry stub -> MissionSelect -> optional Supply confirm -> Field scene load,
and Field enemy -> CombatPlanning -> RewardService -> optional Quest/DemoMission completion.
```

This should happen before Shop/Supply Phase 2, Party, full UI routing, or save/load persistence work.

## 14. Do-Not-Touch List

Do not change in the next implementation step:

- Script paths, class names, namespaces, or serialized field names.
- Existing Title flow.
- Existing CaseFile and `SceneTravelService` behavior.
- Combat damage, turn resolution, skill data, or combat result rules.
- Reward grant behavior.
- Quest/DemoMission compatibility behavior.
- Settlement request/result behavior.
- Save file path, save slots, or legacy `SaveManager` hotkeys.
- Full Shop/Supply UI.
- Party/equipment management.
- Full Calendar UI.
