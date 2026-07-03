# Tab 18. 실제 파일 기준표 / GitHub 매핑

Date: 2026-07-03  
Repository: `SDapple970/GAME_002`  
Unity project root: current repository root  
Main script folder: `Assets/GAME/Scripts`

## Audit Scope

- `Assets/GAME/Scripts/**/*.cs`
- `Assets/GAME/Data` assets where serialized ScriptableObject references matter
- `Assets/GAME/Prefabs` where UI prefab references matter
- `Assets/GAME/Scenes` where scene component references affect Legacy/Inspector decisions

This report is a file mapping audit only. It does not mark missing planned files as immediate implementation tasks, and it does not recommend deleting uncertain or scene-referenced files without Unity Inspector checks.

## Status and Action Legend

| Status | Meaning |
|---|---|
| Exists | File exists and matches the planned responsibility closely enough. |
| Missing | Planned target does not exist yet. |
| Name Diff | Existing file is the same or similar responsibility under a different name. |
| Duplicate | Parallel file path or class responsibility exists. |
| Merge Candidate | Responsibility should later be folded into a clearer owner. |
| Legacy Compat | Old compatibility path that should stay until scene/prefab references are resolved. |
| Debug Only | Debug/test helper, not production runtime architecture. |
| Archive Candidate | Likely should move out of runtime production folders later. |
| Delete Candidate | Likely unnecessary, but only after reference checks. |
| Needs Inspector Check | Serialized scene/prefab/asset usage should be verified in Unity before action. |

| Action | Meaning |
|---|---|
| Keep | Keep as current production or support file. |
| Create | Create later when the production phase needs it. |
| Rename | Rename later with Unity/meta safety. |
| Move | Move later with `.meta` preservation. |
| Merge | Merge responsibility into planned owner later. |
| Promote | Promote demo/prototype behavior into production system. |
| Wrap | Keep as adapter/wrapper around production owner. |
| Legacy | Keep temporarily for compatibility. |
| Debugging | Keep under debug/test ownership. |
| Archive | Archive after references are cleared. |
| Delete Later | Delete only after Unity reference checks. |
| Inspector Check | Check scene/prefab/asset references before any change. |

## Summary By System

| System | Current Actual State | Main Mapping Result |
|---|---|---|
| Core | `GameStateMachine`, `RuntimeBootstrapper`, `GameFlowController`, `SceneFlowController`, and `SaveLoadService` exist. | Production owners exist. Save schema/slot files are future targets. |
| Input | `GameInputInstaller`, `InputService`, and `InputRouter` exist. Old adapters also remain. | Planned route exists; `OverworldInputAdapter`, `OverworldInputBridge`, `PlayerInputUnity`, and old player controllers are compatibility/merge candidates. |
| World / Field / Stage | Player, enemy, scene travel, camera, interaction, and search field components exist. | Field runtime exists, but `FieldEnemy` mixes AI, combat entry, and demo progress responsibilities. |
| Combat | `CombatEntryPoint` path exists with runtime core/model/UI/integration files. | Production combat path exists. Legacy battle route remains for compatibility and reference checks. |
| Narrative / Dialogue / Choice | Story runtime and older NonCombat/Dialogue paths coexist. | Production `StoryEventRunner`/`DialogueRunner` path exists, with older dialogue/timed-choice panels to merge later. |
| Quest | Production `QuestRuntime`, `QuestObjectiveTracker`, and `QuestCompletionFlow` exist; DemoMission and Mission paths also exist. | Quest wrappers exist, but DemoMission files are still scene/asset-backed and should be promoted gradually. |
| Reward / Inventory / Progression | `RewardService`, `RewardResult`, inventory, currency, persona, and save/progress files exist. | Reward owner exists; `RewardApplier`, search rewards, random loot, and UI display paths should wrap/merge into service. |
| UI | `UIScreenRouter` and `GameUIRootController` exist, plus many direct HUD/panel controllers. | Planned router exists; direct panel controllers require inspector wiring and gradual migration. |
| Data / ScriptableObject | Combat skill, story event, quest, mission, search, interaction, demo, and title SOs exist. | Data is present but split across demo/production folders and naming generations. |
| Debugging / Legacy | `Debugging` and `Legacy` folders exist. | Keep temporarily; some debug scripts still have scene/test references. |
| Movement / Motion | New `Player/Runtime` movement stack and older player stack coexist. | New runtime stack is preferred; old stack is legacy/inspector-check territory. |
| Party / Character Runtime | Persona files exist; party/character services are missing. | Future architecture target. |
| Audio / Settings / Rebind | Skill SFX fields and `RebindSaveLoad` exist; no audio/settings service owners. | Future architecture target, except rebind compatibility. |
| Cutscene / Timeline | `CutscenePlaybackRequest` and mission-complete cutscene controller exist. | Thin cutscene support exists; timeline director/service is future target. |
| Localization | No localization runtime files found. | Future architecture target. |
| Content Production Pipeline | Demo, tutorial, title, mission, story/search data folders exist. | Pipeline exists as content-specific scripts and assets, but needs production ownership rules. |

## Scene / Data / Prefab Reference Notes

- Scene refs found for core owners: `GameStateMachine`, `RuntimeBootstrapper`, `GameFlowController`, `SceneFlowController`, `SaveLoadService`, `RewardService`, `GameUIRootController`, and `UIScreenRouter` are present in major scenes.
- `CombatEntryPoint`, `CombatDirector`, `CombatStateSyncer`, `CombatFormationManager`, combat HUDs, and encounter components are scene-referenced.
- `FieldEnemy` is scene-referenced in `Test.unity`; do not delete or move without inspector validation.
- Debugging combat tools are still referenced in `Test.unity` and `Assets/GAME/Combat/Tests/Scenes/CombatTest.unity`.
- DemoMission assets and runtime components are referenced in `Assets/GAME/Data/Tutorial` and `Dungeon 1.unity`.
- `BattleTransitionController` is scene-referenced in `Test.unity` and `InGame.unity`; it remains Legacy Compat.
- UI prefab refs include `RewardItemUI` in `Assets/GAME/Prefabs/RewardItem.prefab` and `Assets/GAME/Prefabs/UI/Combat/RewarItemUI.prefab`.
- Data folders contain typo/legacy asset names such as `Evnet_RescueNpc.asset`, `Dialogue_RescueN{pc.asset`, and `RewarItemUI.prefab`; these need content/reference checks, not script edits.

## Planned Missing Files Required Later

| System | Planned File | Actual File Path | Status | Recommended Action | Notes |
|---|---|---|---|---|---|
| Core | `GameSaveData.cs` | - | Missing | Create | Future save schema; not an immediate task. |
| Core | `SaveSlotService.cs` | - | Missing | Create | Needed when multi-slot save UX is planned. |
| Core | `SaveMigrationService.cs` | - | Missing | Create | Needed after versioned save data exists. |
| Input | `CombatInputAdapter.cs` | - | Missing | Create | Future adapter if combat input separates from global router. |
| Input | `DialogueInputAdapter.cs` | - | Missing | Create | Future adapter for narrative advance/cancel routing. |
| Input | `UIInputAdapter.cs` | - | Missing | Create | Future owner for menu navigation/cancel. |
| World / Field / Stage | `EncounterDefinitionSO.cs` | - | Missing | Create | Future data owner for encounter composition. |
| World / Field / Stage | `FieldEnemyController.cs` | `Assets/GAME/Scripts/Combat/FieldEnemy.cs` | Name Diff | Rename | Future split target after responsibilities are separated. |
| Quest | `QuestDefinitionSO.cs` | `Assets/GAME/Scripts/Quest/QuestDataSO.cs`, `Assets/GAME/Scripts/DemoMission/Data/DemoMissionDefinitionSO.cs` | Name Diff | Promote | Current quest/demo SOs cover part of the planned role. |
| Reward / Inventory / Progression | `RewardDefinitionSO.cs` | - | Missing | Create | Future data model for reward requests/tables. |
| Reward / Inventory / Progression | `RewardTableSO.cs` | - | Missing | Create | Future loot/reward table asset. |
| Reward / Inventory / Progression | `RewardRequest.cs` | - | Missing | Create | Future request DTO into `RewardService`. |
| Reward / Inventory / Progression | `CharacterProgressionService.cs` | - | Missing | Create | `RewardService` already logs that combat EXP has no target service yet. |
| UI | `HUDRootController.cs` | `Assets/GAME/Scripts/UI/GameUIRootController.cs` | Name Diff | Keep | Existing name is broader than HUD only. |
| UI | `ModalRouter.cs` | - | Missing | Create | Future if modal stacking becomes complex. |
| Party / Character Runtime | `PartyRuntime.cs` | - | Missing | Create | Future party composition owner. |
| Party / Character Runtime | `CharacterRuntime.cs` | - | Missing | Create | Future character state owner. |
| Party / Character Runtime | `CharacterDefinitionSO.cs` | - | Missing | Create | Future character data asset. |
| Audio / Settings / Rebind | `AudioService.cs` | - | Missing | Create | Future central audio owner. |
| Audio / Settings / Rebind | `AudioSettingsService.cs` | - | Missing | Create | Future volume/mixer settings owner. |
| Audio / Settings / Rebind | `SettingsService.cs` | - | Missing | Create | Future settings persistence owner. |
| Audio / Settings / Rebind | `RebindService.cs` | `Assets/GAME/Scripts/Input/RebindSaveLoad.cs` | Name Diff | Wrap | Existing file is compatibility persistence. |
| Cutscene / Timeline | `CutsceneService.cs` | `Assets/GAME/Scripts/Cutscene/Runtime/MissionCompleteCutsceneController.cs` | Name Diff | Promote | Existing mission-specific controller can inform the service. |
| Cutscene / Timeline | `TimelinePlaybackController.cs` | - | Missing | Create | Future Timeline integration owner. |
| Localization | `LocalizationService.cs` | - | Missing | Create | No localization runtime found. |
| Localization | `LocalizedTextTableSO.cs` | - | Missing | Create | No localization data asset found. |
| Content Production Pipeline | `ContentValidationRunner.cs` | `Assets/GAME/Scripts/Debugging/VerticalSliceSceneValidator.cs` | Name Diff | Promote | Existing validator can become production content validation later. |

## Full Actual Script Mapping

This table covers all 261 `.cs` files currently under `Assets/GAME/Scripts`.

| System | Planned File | Actual File Path | Status | Recommended Action | Notes |
|---|---|---|---|---|---|
| World / Field / Stage | `CameraFollow2D.cs` | `Assets/GAME/Scripts/Camera/CameraFollow2D.cs` | Exists | Keep | Scene-referenced follow camera. |
| Combat | `CombatCameraController.cs` | `Assets/GAME/Scripts/Camera/CombatCameraController.cs` | Exists | Keep | Combat presentation camera support. |
| Combat | `CombatLogHUD.cs` | `Assets/GAME/Scripts/Combat/CombatLogHUD.cs` | Exists | Keep | Combat UI support. |
| Data / ScriptableObject | `OpeningEffectSO.cs` | `Assets/GAME/Scripts/Combat/Data/OpeningEffectSO.cs` | Exists | Keep | Combat opening effect data. |
| Data / ScriptableObject | `SkillDefinitionSO.cs` | `Assets/GAME/Scripts/Combat/Data/SkillDefinitionSO.cs` | Exists | Keep | Skill assets reference this file. |
| Data / ScriptableObject | `SkillMovementMode.cs` | `Assets/GAME/Scripts/Combat/Data/SkillMovementMode.cs` | Exists | Keep | Skill enum/model support. |
| World / Field / Stage | `FieldEnemyController.cs` | `Assets/GAME/Scripts/Combat/FieldEnemy.cs` | Merge Candidate | Merge | Mixed field AI, combat entry, and demo progress; scene-referenced. |
| Combat | `SkillRunner.cs` | `Assets/GAME/Scripts/Combat/Runtime/Actions/SkillRunner.cs` | Exists | Keep | Combat action execution. |
| Combat | `SoSkill.cs` | `Assets/GAME/Scripts/Combat/Runtime/Actions/SoSkill.cs` | Exists | Keep | Adapter from skill SO to combat skill model. |
| Combat | `CombatHpComponent.cs` | `Assets/GAME/Scripts/Combat/Runtime/Adapters/CombatHpComponent.cs` | Exists | Keep | Scene-referenced combatant adapter. |
| Combat | `CombatKeywordComponent.cs` | `Assets/GAME/Scripts/Combat/Runtime/Adapters/CombatKeywordComponent.cs` | Exists | Keep | Scene-referenced combatant adapter. |
| Combat | `CombatSkillLoadoutComponent.cs` | `Assets/GAME/Scripts/Combat/Runtime/Adapters/CombatSkillLoadoutComponent.cs` | Exists | Keep | Scene-referenced skill loadout. |
| Combat | `CombatStartRequest.cs` | `Assets/GAME/Scripts/Combat/Runtime/Adapters/CombatStartRequest.cs` | Exists | Keep | Start request DTO. |
| Combat | `DummyCombatant.cs` | `Assets/GAME/Scripts/Combat/Runtime/Adapters/DummyCombatant.cs` | Debug Only | Debugging | Useful for smoke/test combat. |
| Combat | `DummyCombatantFactory.cs` | `Assets/GAME/Scripts/Combat/Runtime/Adapters/DummyCombatantFactory.cs` | Debug Only | Debugging | Test/dummy combatant factory. |
| Combat | `FieldCombatantAdapter.cs` | `Assets/GAME/Scripts/Combat/Runtime/Adapters/FieldCombatantAdapter.cs` | Exists | Keep | Production adapter from field object to combatant. |
| Combat | `FieldCombatantFactory.cs` | `Assets/GAME/Scripts/Combat/Runtime/Adapters/FieldCombatantFactory.cs` | Exists | Keep | Factory used by combat bootstrap path. |
| Combat | `HpAccessor.cs` | `Assets/GAME/Scripts/Combat/Runtime/Adapters/HpAccessor.cs` | Exists | Keep | Adapter helper. |
| Combat | `ICombatantFactory.cs` | `Assets/GAME/Scripts/Combat/Runtime/Adapters/ICombatantFactory.cs` | Exists | Keep | Combat factory interface. |
| Combat | `OpeningEffectApplier.cs` | `Assets/GAME/Scripts/Combat/Runtime/Adapters/OpeningEffectApplier.cs` | Exists | Keep | Applies encounter advantage/opening effect. |
| Combat | `CombatantAnimationDriver.cs` | `Assets/GAME/Scripts/Combat/Runtime/Animation/CombatantAnimationDriver.cs` | Exists | Keep | Scene-referenced animation bridge. |
| Combat | `CombatBootstrapper.cs` | `Assets/GAME/Scripts/Combat/Runtime/Core/CombatBootstrapper.cs` | Exists | Keep | Production combat session builder. |
| Combat | `CombatEndEvaluator.cs` | `Assets/GAME/Scripts/Combat/Runtime/Core/CombatEndEvaluator.cs` | Exists | Keep | Combat end rules. |
| Combat | `CombatEntryPoint.cs` | `Assets/GAME/Scripts/Combat/Runtime/Core/CombatEntryPoint.cs` | Exists | Keep | Official combat start path; scene-referenced. |
| Combat | `CombatFlowOrchestrator.cs` | `Assets/GAME/Scripts/Combat/Runtime/Core/CombatFlowOrchestrator.cs` | Exists | Keep | Scene-referenced planning/flow bridge. |
| Combat | `CombatPlanValidator.cs` | `Assets/GAME/Scripts/Combat/Runtime/Core/CombatPlanValidator.cs` | Exists | Keep | Combat planning validation. |
| Combat | `CombatResultBuilder.cs` | `Assets/GAME/Scripts/Combat/Runtime/Core/CombatResultBuilder.cs` | Exists | Keep | Combat result creation. |
| Combat | `CombatStateMachine.cs` | `Assets/GAME/Scripts/Combat/Runtime/Core/CombatStateMachine.cs` | Exists | Keep | Combat phase state owner. |
| Combat | `CombatTimeline.cs` | `Assets/GAME/Scripts/Combat/Runtime/Core/CombatTimeline.cs` | Exists | Keep | Combat turn timeline model. |
| Combat | `CombatTurnResolver.cs` | `Assets/GAME/Scripts/Combat/Runtime/Core/CombatTurnResolver.cs` | Exists | Keep | Combat resolution rules. |
| Combat | `InspirationPool.cs` | `Assets/GAME/Scripts/Combat/Runtime/Core/InspirationPool.cs` | Exists | Keep | Combat resource model. |
| Combat | `KnowledgeBook.cs` | `Assets/GAME/Scripts/Combat/Runtime/Core/KnowledgeBook.cs` | Exists | Keep | Combat knowledge model. |
| Combat | `SkillBook.cs` | `Assets/GAME/Scripts/Combat/Runtime/Core/SkillBook.cs` | Exists | Keep | Combat skill model. |
| Combat | `StaggerSystem.cs` | `Assets/GAME/Scripts/Combat/Runtime/Core/StaggerSystem.cs` | Exists | Keep | Combat stagger rules. |
| Combat | `CombatDirector.cs` | `Assets/GAME/Scripts/Combat/Runtime/Effects/CombatDirector.cs` | Exists | Keep | Production combat presentation owner; scene-referenced. |
| Combat | `CombatEnvironment.cs` | `Assets/GAME/Scripts/Combat/Runtime/Environment/CombatEnvironment.cs` | Exists | Keep | Combat environment data. |
| Combat | `CombatDemoFlowController.cs` | `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatDemoFlowController.cs` | Merge Candidate | Merge | Demo flow should eventually be content/quest driven. |
| Combat | `CombatEncounterGroup.cs` | `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatEncounterGroup.cs` | Exists | Keep | Production encounter composition helper. |
| Combat | `CombatEncounterTrigger2D.cs` | `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatEncounterTrigger2D.cs` | Exists | Keep | Preferred field-to-combat trigger path. |
| Combat | `CombatFieldLock.cs` | `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatFieldLock.cs` | Exists | Keep | Field locking during combat. |
| Combat | `CombatFormationManager.cs` | `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatFormationManager.cs` | Exists | Keep | Scene-referenced formation support. |
| Combat | `CombatStateSyncer.cs` | `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatStateSyncer.cs` | Exists | Keep | Syncs combat start to game state. |
| Combat | `EncounterAdvantageApplier.cs` | `Assets/GAME/Scripts/Combat/Runtime/Integration/EncounterAdvantageApplier.cs` | Legacy Compat | Wrap | Still references legacy encounter request data. |
| Combat | `ActionPlan.cs` | `Assets/GAME/Scripts/Combat/Runtime/Model/ActionPlan.cs` | Exists | Keep | Combat model. |
| Combat | `CombatEndReason.cs` | `Assets/GAME/Scripts/Combat/Runtime/Model/CombatEndReason.cs` | Exists | Keep | Combat model enum. |
| Combat | `CombatEnums.cs` | `Assets/GAME/Scripts/Combat/Runtime/Model/CombatEnums.cs` | Exists | Keep | Combat model enums. |
| Combat | `CombatIds.cs` | `Assets/GAME/Scripts/Combat/Runtime/Model/CombatIds.cs` | Exists | Keep | Combat ID model. |
| Combat | `CombatPlanDraft.cs` | `Assets/GAME/Scripts/Combat/Runtime/Model/CombatPlanDraft.cs` | Exists | Keep | Combat planning model. |
| Combat | `CombatPlaybook.cs` | `Assets/GAME/Scripts/Combat/Runtime/Model/CombatPlaybook.cs` | Exists | Keep | Combat model. |
| Combat | `CombatResult.cs` | `Assets/GAME/Scripts/Combat/Runtime/Model/CombatResult.cs` | Exists | Keep | Combat result model. |
| Combat | `CombatSession.cs` | `Assets/GAME/Scripts/Combat/Runtime/Model/CombatSession.cs` | Exists | Keep | Combat session model. |
| Combat | `CombatTurn.cs` | `Assets/GAME/Scripts/Combat/Runtime/Model/CombatTurn.cs` | Exists | Keep | Combat turn model. |
| Combat | `ICombatant.cs` | `Assets/GAME/Scripts/Combat/Runtime/Model/ICombatant.cs` | Exists | Keep | Combat interface. |
| Combat | `ISkill.cs` | `Assets/GAME/Scripts/Combat/Runtime/Model/ISkill.cs` | Exists | Keep | Combat skill interface. |
| Combat | `KeywordMask.cs` | `Assets/GAME/Scripts/Combat/Runtime/Model/KeywordMask.cs` | Exists | Keep | Combat keyword model. |
| UI | `CombatantWidget.cs` | `Assets/GAME/Scripts/Combat/Runtime/UI/CombatantWidget.cs` | Exists | Keep | Combat UI component. |
| UI | `CombatInspirationHUD.cs` | `Assets/GAME/Scripts/Combat/Runtime/UI/CombatInspirationHUD.cs` | Exists | Keep | Combat HUD; scene-referenced. |
| UI | `CombatPlanningHUD.cs` | `Assets/GAME/Scripts/Combat/Runtime/UI/CombatPlanningHUD.cs` | Exists | Keep | Combat planning HUD; scene-referenced. |
| Reward / Inventory / Progression | `CombatRewardUIBinder.cs` | `Assets/GAME/Scripts/Combat/Runtime/UI/CombatRewardUIBinder.cs` | Exists | Keep | Binds combat result to RewardService and reward UI. |
| UI | `CombatUIRootController.cs` | `Assets/GAME/Scripts/Combat/Runtime/UI/CombatUIRootController.cs` | Merge Candidate | Merge | Later fold visibility into `UIScreenRouter` ownership. |
| UI | `CombatWidgetManager.cs` | `Assets/GAME/Scripts/Combat/Runtime/UI/CombatWidgetManager.cs` | Exists | Keep | Combat widget orchestration. |
| Combat | `IDamageable.cs` | `Assets/GAME/Scripts/Common/Damage/IDamageable.cs` | Exists | Keep | Shared damage interface. |
| Combat | `SimpleDamageable.cs` | `Assets/GAME/Scripts/Common/Damage/SimpleDamageable.cs` | Archive Candidate | Archive | Useful test target, not clear production owner. |
| Core | `GameFlowController.cs` | `Assets/GAME/Scripts/Core/GameFlowController.cs` | Exists | Keep | Production game flow owner. |
| Core | `GameState.cs` | `Assets/GAME/Scripts/Core/GameState.cs` | Exists | Keep | Production state enum. |
| Core | `GameStateMachine.cs` | `Assets/GAME/Scripts/Core/GameStateMachine.cs` | Exists | Keep | Production global state machine. |
| Core | `RuntimeBootstrapper.cs` | `Assets/GAME/Scripts/Core/RuntimeBootstrapper.cs` | Exists | Keep | Production bootstrapping owner. |
| Core | `SaveLoadService.cs` | `Assets/GAME/Scripts/Core/SaveLoadService.cs` | Exists | Keep | Production wrapper over old save path. |
| Core | `SceneFlowController.cs` | `Assets/GAME/Scripts/Core/SceneFlowController.cs` | Exists | Keep | Production scene flow owner. |
| Cutscene / Timeline | `CutscenePlaybackRequest.cs` | `Assets/GAME/Scripts/Cutscene/Runtime/CutscenePlaybackRequest.cs` | Exists | Keep | Cutscene request data. |
| Cutscene / Timeline | `CutsceneService.cs` | `Assets/GAME/Scripts/Cutscene/Runtime/MissionCompleteCutsceneController.cs` | Name Diff | Promote | Mission-specific cutscene controller; future service target. |
| Debugging / Legacy | `CombatAutoPlanner.cs` | `Assets/GAME/Scripts/Debugging/Combat/CombatAutoPlanner.cs` | Debug Only | Debugging | Debug automation helper. |
| Debugging / Legacy | `CombatFieldCallDebug.cs` | `Assets/GAME/Scripts/Debugging/Combat/CombatFieldCallDebug.cs` | Debug Only | Debugging | Test scene reference; do not delete before check. |
| Debugging / Legacy | `CombatSkillDebugInvoker.cs` | `Assets/GAME/Scripts/Debugging/Combat/CombatSkillDebugInvoker.cs` | Debug Only | Debugging | Test scene reference. |
| Debugging / Legacy | `CombatStartSmokeTest.cs` | `Assets/GAME/Scripts/Debugging/Combat/CombatStartSmokeTest.cs` | Debug Only | Debugging | Test scene reference. |
| Debugging / Legacy | `CombatTestRunner.cs` | `Assets/GAME/Scripts/Debugging/Combat/CombatTestRunner.cs` | Debug Only | Debugging | Combat test scene reference. |
| Debugging / Legacy | `InspirationDebugHotkey.cs` | `Assets/GAME/Scripts/Debugging/Combat/InspirationDebugHotkey.cs` | Debug Only | Debugging | Debug hotkey. |
| Debugging / Legacy | `StoryInteractionDebugHotkey.cs` | `Assets/GAME/Scripts/Debugging/StoryInteractionDebugHotkey.cs` | Debug Only | Debugging | Debug hotkey. |
| Content Production Pipeline | `ContentValidationRunner.cs` | `Assets/GAME/Scripts/Debugging/VerticalSliceSceneValidator.cs` | Name Diff | Promote | Scene validator can become content validation later. |
| Content Production Pipeline | `ContractDataSO.cs` | `Assets/GAME/Scripts/Demo/ContractDataSO.cs` | Archive Candidate | Archive | Demo contract data; superseded by mission/case data later. |
| UI | `CaseFileDocumentPanel.cs` | `Assets/GAME/Scripts/Demo/ContractDocumentPanel.cs` | Name Diff | Promote | Demo document panel overlaps Office/DemoMission case file UI. |
| Quest | `MissionObjectiveTracker.cs` | `Assets/GAME/Scripts/Demo/DungeonObjectiveManager.cs` | Name Diff | Promote | Demo objective manager; Dungeon 1 scene-referenced. |
| Quest | `ObjectiveMarker.cs` | `Assets/GAME/Scripts/Demo/ObjectiveEnemyMarker.cs` | Merge Candidate | Merge | Demo objective marker should fold into quest/field objective flow. |
| Quest | `RescueNpcObjectiveEventSO.cs` | `Assets/GAME/Scripts/Demo/RescueNpcObjectiveEventSO.cs` | Name Diff | Promote | Demo SO referenced by tutorial/interaction assets. |
| UI | `TitleSceneController.cs` | `Assets/GAME/Scripts/Demo/TitleSceneController.cs` | Duplicate | Merge | Duplicate responsibility with `Title/Runtime/TitleSceneController.cs`. |
| Quest | `QuestDefinitionSO.cs` | `Assets/GAME/Scripts/DemoMission/Data/DemoMissionDefinitionSO.cs` | Name Diff | Promote | DemoMission asset-backed definition; migrate carefully. |
| Quest | `MonsterBriefingEntry.cs` | `Assets/GAME/Scripts/DemoMission/Data/MonsterBriefingEntry.cs` | Exists | Keep | Demo mission briefing data; can map into quest content. |
| Quest | `RescueNpcDefinitionSO.cs` | `Assets/GAME/Scripts/DemoMission/Data/RescueNpcDefinitionSO.cs` | Name Diff | Promote | Demo rescue data asset; migrate carefully. |
| Quest | `QuestRuntime.cs` | `Assets/GAME/Scripts/DemoMission/Runtime/DemoMissionRuntime.cs` | Name Diff | Promote | Demo runtime still active in Dungeon 1. |
| UI | `MissionCompletePanel.cs` | `Assets/GAME/Scripts/DemoMission/Runtime/Ending/DemoEndPanelController.cs` | Name Diff | Promote | Demo end UI controller. |
| Cutscene / Timeline | `MissionCompleteCutsceneController.cs` | `Assets/GAME/Scripts/DemoMission/Runtime/Ending/DemoRescueNpcEndFlow.cs` | Merge Candidate | Merge | End flow mixes mission completion, UI, and cutscene-like behavior. |
| Quest | `QuestCompletionFlow.cs` | `Assets/GAME/Scripts/DemoMission/Runtime/MissionCompletionController.cs` | Name Diff | Promote | Demo completion flow overlaps production quest completion. |
| Quest | `QuestObjectiveTracker.cs` | `Assets/GAME/Scripts/DemoMission/Runtime/MissionObjectiveTracker.cs` | Name Diff | Promote | Demo tracker overlaps production objective tracker. |
| Quest | `QuestInteractableActor.cs` | `Assets/GAME/Scripts/DemoMission/Runtime/RescueNpcActor.cs` | Name Diff | Promote | Demo NPC actor subscribes to routed input and mission runtime. |
| UI | `CaseFileAcceptController.cs` | `Assets/GAME/Scripts/DemoMission/UI/CaseFileAcceptController.cs` | Merge Candidate | Merge | DemoMission UI should route through production UI/quest accept flow later. |
| UI | `CaseFilePanel.cs` | `Assets/GAME/Scripts/DemoMission/UI/CaseFilePanel.cs` | Merge Candidate | Merge | DemoMission UI. |
| UI | `MissionCompletePanel.cs` | `Assets/GAME/Scripts/DemoMission/UI/MissionCompletePanel.cs` | Merge Candidate | Merge | DemoMission UI. |
| Narrative / Dialogue / Choice | `TimedChoiceEventSO.cs` | `Assets/GAME/Scripts/Dialogue/TimedChoiceDialogueEventSO.cs` | Name Diff | Merge | Older timed-choice data path. |
| UI | `TimedChoicePanel.cs` | `Assets/GAME/Scripts/Dialogue/TimedChoiceDialoguePanel.cs` | Duplicate | Merge | Older timed-choice panel overlaps Story runtime UI. |
| Narrative / Dialogue / Choice | `TimedChoiceOption.cs` | `Assets/GAME/Scripts/Dialogue/TimedChoiceOption.cs` | Exists | Keep | Older timed-choice model. |
| World / Field / Stage | `FieldEnemyAnimator2D.cs` | `Assets/GAME/Scripts/Enemies/Runtime/FieldEnemyAnimator2D.cs` | Exists | Keep | Dungeon 1 scene-referenced enemy runtime. |
| World / Field / Stage | `FieldEnemyMotor2D.cs` | `Assets/GAME/Scripts/Enemies/Runtime/FieldEnemyMotor2D.cs` | Exists | Keep | Dungeon 1 scene-referenced enemy runtime. |
| World / Field / Stage | `FieldEnemyPatrolAI2D.cs` | `Assets/GAME/Scripts/Enemies/Runtime/FieldEnemyPatrolAI2D.cs` | Exists | Keep | Dungeon 1 scene-referenced enemy runtime. |
| World / Field / Stage | `FieldEnemyAnimator2D.cs` | `Assets/GAME/Scripts/Enemy/Overworld/EnemyAnimator2D.cs` | Name Diff | Merge | Older enemy path in Test scene. |
| World / Field / Stage | `FieldEnemyPatrolAI2D.cs` | `Assets/GAME/Scripts/Enemy/Overworld/OverworldEnemyAI.cs` | Name Diff | Merge | Older enemy AI path in Test scene. |
| Input | `GameInputInstaller.cs` | `Assets/GAME/Scripts/Input/GameInputInstaller.cs` | Exists | Keep | Production input root; scene-referenced. |
| Input | `InputActions.cs` | `Assets/GAME/Scripts/Input/inputactions.cs` | Name Diff | Rename | Generated file name casing should be normalized only through Input System workflow. |
| Input | `InputDeviceWatcher.cs` | `Assets/GAME/Scripts/Input/InputDeviceWatcher.cs` | Exists | Keep | Device state support. |
| Input | `InputRouter.cs` | `Assets/GAME/Scripts/Input/InputRouter.cs` | Exists | Keep | Planned route owner exists. |
| Input | `InputService.cs` | `Assets/GAME/Scripts/Input/InputService.cs` | Exists | Keep | Input event service. |
| Input | `PlayerInputAdapter.cs` | `Assets/GAME/Scripts/Input/OverworldInputAdapter.cs` | Legacy Compat | Legacy | Older direct InputActionReference route. |
| Audio / Settings / Rebind | `RebindService.cs` | `Assets/GAME/Scripts/Input/RebindSaveLoad.cs` | Name Diff | Wrap | Existing rebind persistence wrapper. |
| Narrative / Dialogue / Choice | `DialogueInteractionEventSO.cs` | `Assets/GAME/Scripts/Interaction/DialogueInteractionEventSO.cs` | Exists | Keep | Interaction data asset path. |
| World / Field / Stage | `IInteractable.cs` | `Assets/GAME/Scripts/Interaction/IInteractable.cs` | Exists | Keep | Interaction interface. |
| World / Field / Stage | `InteractableObject.cs` | `Assets/GAME/Scripts/Interaction/InteractableObject.cs` | Exists | Keep | Scene-referenced interaction object. |
| World / Field / Stage | `InteractionController.cs` | `Assets/GAME/Scripts/Interaction/InteractionController.cs` | Exists | Keep | Scene-referenced interaction controller. |
| World / Field / Stage | `InteractionEventSO.cs` | `Assets/GAME/Scripts/Interaction/InteractionEventSO.cs` | Exists | Keep | Interaction event data base. |
| UI | `InteractionPromptUI.cs` | `Assets/GAME/Scripts/Interaction/InteractionPromptUI.cs` | Exists | Keep | Scene-referenced prompt UI. |
| Narrative / Dialogue / Choice | `ObjectStateChangeEventSO.cs` | `Assets/GAME/Scripts/Interaction/ObjectStateChangeEventSO.cs` | Exists | Keep | Interaction outcome data. |
| Reward / Inventory / Progression | `LootEntry.cs` | `Assets/GAME/Scripts/Interaction/RandomLootEntry.cs` | Name Diff | Merge | Should later route through RewardRequest/RewardService. |
| Reward / Inventory / Progression | `LootInteractionEventSO.cs` | `Assets/GAME/Scripts/Interaction/RandomLootInteractionEventSO.cs` | Name Diff | Merge | Random loot should later use RewardService. |
| Reward / Inventory / Progression | `RewardInteractionEventSO.cs` | `Assets/GAME/Scripts/Interaction/RewardInteractionEventSO.cs` | Exists | Wrap | Should wrap RewardService rather than direct grants. |
| Debugging / Legacy | `BattleTransitionRequest.cs` | `Assets/GAME/Scripts/Legacy/Battle/BattleTransitionRequest.cs` | Legacy Compat | Legacy | Old battle data path. |
| Debugging / Legacy | `BattleTrigger2D.cs` | `Assets/GAME/Scripts/Legacy/Battle/BattleTrigger2D.cs` | Legacy Compat | Legacy | Old battle trigger path; do not revive. |
| Debugging / Legacy | `SeamlessBattleManager.cs` | `Assets/GAME/Scripts/Legacy/Battle/SeamlessBattleManager.cs` | Legacy Compat | Legacy | Old battle manager. |
| Debugging / Legacy | `StoryFlagManagerDeprecated.cs` | `Assets/GAME/Scripts/Legacy/StoryDeprecated/StoryFlagManagerDeprecated.cs` | Legacy Compat | Legacy | Deprecated story flag path. |
| Quest | `MissionDefinitionSO.cs` | `Assets/GAME/Scripts/Mission/Runtime/Data/MissionDefinitionSO.cs` | Exists | Keep | Production mission data asset exists. |
| Quest | `MissionObjective.cs` | `Assets/GAME/Scripts/Mission/Runtime/Data/MissionObjective.cs` | Exists | Keep | Mission objective model. |
| Quest | `QuestManager.cs` | `Assets/GAME/Scripts/Mission/Runtime/MissionManager.cs` | Name Diff | Wrap | Mission manager compatibility with quest architecture. |
| UI | `MissionHUD.cs` | `Assets/GAME/Scripts/Mission/Runtime/UI/MissionHUD.cs` | Exists | Keep | Mission UI support. |
| Narrative / Dialogue / Choice | `ChapterDefinitionSO.cs` | `Assets/GAME/Scripts/NonCombat/Chapter/ChapterDefinitionSO.cs` | Exists | Keep | NonCombat chapter data. |
| Narrative / Dialogue / Choice | `ChapterProgressManager.cs` | `Assets/GAME/Scripts/NonCombat/Chapter/NonCombatChapterProgressManager.cs` | Name Diff | Merge | Older chapter progress path. |
| Narrative / Dialogue / Choice | `ChoiceOutcome.cs` | `Assets/GAME/Scripts/NonCombat/Choice/ChoiceOutcome.cs` | Exists | Keep | NonCombat choice model. |
| Narrative / Dialogue / Choice | `ChoiceRunner.cs` | `Assets/GAME/Scripts/NonCombat/Choice/ChoiceRunner.cs` | Exists | Keep | NonCombat choice runner; later unify with Story choice runner. |
| Narrative / Dialogue / Choice | `ChoiceCondition.cs` | `Assets/GAME/Scripts/NonCombat/Choice/NonCombatChoiceCondition.cs` | Name Diff | Merge | Older choice condition path. |
| Narrative / Dialogue / Choice | `DialogueChoice.cs` | `Assets/GAME/Scripts/NonCombat/Dialogue/DialogueChoice.cs` | Exists | Keep | NonCombat dialogue model. |
| Narrative / Dialogue / Choice | `DialogueController.cs` | `Assets/GAME/Scripts/NonCombat/Dialogue/DialogueController.cs` | Merge Candidate | Merge | Older dialogue controller. |
| Data / ScriptableObject | `DialogueNodeSO.cs` | `Assets/GAME/Scripts/NonCombat/Dialogue/DialogueNodeSO.cs` | Exists | Keep | NonCombat dialogue SO. |
| UI | `DialoguePanel.cs` | `Assets/GAME/Scripts/NonCombat/Dialogue/NonCombatDialogueUIPanel.cs` | Name Diff | Merge | Older dialogue UI. |
| World / Field / Stage | `INonCombatInteractable.cs` | `Assets/GAME/Scripts/NonCombat/Interaction/INonCombatInteractable.cs` | Exists | Keep | NonCombat interaction interface. |
| World / Field / Stage | `Interactable2D.cs` | `Assets/GAME/Scripts/NonCombat/Interaction/Interactable2D.cs` | Exists | Keep | NonCombat field interaction. |
| World / Field / Stage | `InteractionDetector2D.cs` | `Assets/GAME/Scripts/NonCombat/Interaction/InteractionDetector2D.cs` | Exists | Keep | NonCombat interaction detector. |
| Reward / Inventory / Progression | `CurrencyWallet.cs` | `Assets/GAME/Scripts/NonCombat/Inventory/CurrencyWallet.cs` | Exists | Keep | Currency runtime state. |
| Reward / Inventory / Progression | `InventoryService.cs` | `Assets/GAME/Scripts/NonCombat/Inventory/InventoryService.cs` | Exists | Keep | Inventory owner. |
| Data / ScriptableObject | `ItemDefinitionSO.cs` | `Assets/GAME/Scripts/NonCombat/Inventory/ItemDefinitionSO.cs` | Exists | Keep | Item data asset. |
| Reward / Inventory / Progression | `GameProgressState.cs` | `Assets/GAME/Scripts/NonCombat/Progress/GameProgressState.cs` | Exists | Keep | Progress state model. |
| Narrative / Dialogue / Choice | `StoryFlagDatabase.cs` | `Assets/GAME/Scripts/NonCombat/Progress/StoryFlagDatabase.cs` | Exists | Keep | Story flag data. |
| Reward / Inventory / Progression | `RewardApplier.cs` | `Assets/GAME/Scripts/NonCombat/Reward/RewardApplier.cs` | Legacy Compat | Wrap | Compatibility wrapper around RewardService. |
| Core | `SaveData.cs` | `Assets/GAME/Scripts/NonCombat/Save/SaveData.cs` | Exists | Keep | Existing save model; future versioned schema needed. |
| Core | `SaveManager.cs` | `Assets/GAME/Scripts/NonCombat/Save/SaveManager.cs` | Legacy Compat | Wrap | Keep behind `SaveLoadService`. |
| Core | `SaveSerializer.cs` | `Assets/GAME/Scripts/NonCombat/Save/SaveSerializer.cs` | Exists | Keep | Save serialization helper. |
| Content Production Pipeline | `CaseFileDocumentPanel.cs` | `Assets/GAME/Scripts/Office/CaseFileDocumentPanel.cs` | Exists | Keep | Office/case file content UI. |
| World / Field / Stage | `OfficeHotspot2D.cs` | `Assets/GAME/Scripts/Office/OfficeHotspot2D.cs` | Exists | Keep | Office hotspot interaction. |
| UI | `OfficeMenuController.cs` | `Assets/GAME/Scripts/Office/OfficeMenuController.cs` | Merge Candidate | Merge | Direct office UI controller; route later through UI owner. |
| Movement / Motion | `FieldSkillCaster.cs` | `Assets/GAME/Scripts/Player/FieldSkillCaster.cs` | Exists | Keep | Player field skill support. |
| Input | `IPlayerInput.cs` | `Assets/GAME/Scripts/Player/IPlayerInput.cs` | Legacy Compat | Legacy | Old input abstraction; verify before retiring. |
| Movement / Motion | `OverworldAttack2D.cs` | `Assets/GAME/Scripts/Player/Overworld/OverworldAttack2D.cs` | Legacy Compat | Legacy | Old overworld attack path; scene-referenced. |
| Input | `PlayerInputController.cs` | `Assets/GAME/Scripts/Player/Overworld/OverworldInputBridge.cs` | Legacy Compat | Legacy | Send Messages bridge; keep until no prefab/scene dependency. |
| Movement / Motion | `OverworldPlayerDriver.cs` | `Assets/GAME/Scripts/Player/Overworld/OverworldPlayerDriver.cs` | Delete Candidate | Delete Later | Empty compile placeholder; still check Inspector first. |
| Movement / Motion | `PlayerGlue.cs` | `Assets/GAME/Scripts/Player/Overworld/OverworldPlayerGlue.cs` | Legacy Compat | Legacy | Reflection glue; legacy input/movement bridge. |
| Movement / Motion | `OverworldPlayerController.cs` | `Assets/GAME/Scripts/Player/OverworldPlayerController.cs` | Legacy Compat | Legacy | Old player controller; scene-referenced. |
| Movement / Motion | `PlayerAnimator2D.cs` | `Assets/GAME/Scripts/Player/PlayerAnimator2D.cs` | Legacy Compat | Legacy | Old animation controller; scene-referenced. |
| Movement / Motion | `PlayerController2D.cs` | `Assets/GAME/Scripts/Player/PlayerController2D.cs` | Archive Candidate | Archive | Old IPlayerInput controller path. |
| Input | `PlayerInputUnity.cs` | `Assets/GAME/Scripts/Player/PlayerInputUnity.cs` | Legacy Compat | Legacy | Old `UnityEngine.Input` route. |
| Movement / Motion | `PlayerMotor2D.cs` | `Assets/GAME/Scripts/Player/PlayerMotor2D.cs` | Legacy Compat | Legacy | Old motor; scene-referenced. |
| Movement / Motion | `PlayerAnimationController.cs` | `Assets/GAME/Scripts/Player/Runtime/PlayerAnimationController.cs` | Exists | Keep | Preferred runtime animation controller. |
| Combat | `PlayerFieldAttackController.cs` | `Assets/GAME/Scripts/Player/Runtime/PlayerFieldAttackController.cs` | Exists | Keep | Preferred field attack to combat entry bridge. |
| Input | `PlayerInputController.cs` | `Assets/GAME/Scripts/Player/Runtime/PlayerInputController.cs` | Exists | Keep | Preferred routed player input consumer. |
| Movement / Motion | `PlayerMotor2D.cs` | `Assets/GAME/Scripts/Player/Runtime/PlayerMotor2D_New.cs` | Name Diff | Rename | Preferred motor, but name should be normalized later with Unity safety. |
| Quest | `QuestAdvanceInteractionEventSO.cs` | `Assets/GAME/Scripts/Quest/QuestAdvanceInteractionEventSO.cs` | Exists | Keep | Quest interaction event data. |
| Quest | `QuestCompleteInteractionEventSO.cs` | `Assets/GAME/Scripts/Quest/QuestCompleteInteractionEventSO.cs` | Exists | Keep | Quest event data. |
| Quest | `QuestCompletionFlow.cs` | `Assets/GAME/Scripts/Quest/QuestCompletionFlow.cs` | Exists | Keep | Production quest completion wrapper. |
| Data / ScriptableObject | `QuestDefinitionSO.cs` | `Assets/GAME/Scripts/Quest/QuestDataSO.cs` | Name Diff | Rename | Existing quest data SO; rename only after asset migration plan. |
| Quest | `QuestId.cs` | `Assets/GAME/Scripts/Quest/QuestId.cs` | Exists | Keep | Quest ID value object. |
| Quest | `QuestManager.cs` | `Assets/GAME/Scripts/Quest/QuestManager.cs` | Legacy Compat | Wrap | Existing manager still scene-referenced. |
| Quest | `QuestObjectiveTracker.cs` | `Assets/GAME/Scripts/Quest/QuestObjectiveTracker.cs` | Exists | Keep | Production quest objective wrapper. |
| Quest | `QuestProgress.cs` | `Assets/GAME/Scripts/Quest/QuestProgress.cs` | Exists | Keep | Quest progress model. |
| Quest | `QuestRuntime.cs` | `Assets/GAME/Scripts/Quest/QuestRuntime.cs` | Exists | Keep | Production quest runtime wrapper. |
| Quest | `QuestStartInteractionEventSO.cs` | `Assets/GAME/Scripts/Quest/QuestStartInteractionEventSO.cs` | Exists | Keep | Quest event data. |
| Quest | `QuestStepData.cs` | `Assets/GAME/Scripts/Quest/QuestStepData.cs` | Exists | Keep | Quest step data. |
| UI | `QuestTrackerUI.cs` | `Assets/GAME/Scripts/Quest/QuestTrackerUI.cs` | Legacy Compat | Wrap | Scene-referenced quest UI; later consolidate with HUD router. |
| Reward / Inventory / Progression | `RewardResult.cs` | `Assets/GAME/Scripts/Reward/RewardResult.cs` | Exists | Keep | Reward result DTO. |
| Reward / Inventory / Progression | `RewardService.cs` | `Assets/GAME/Scripts/Reward/RewardService.cs` | Exists | Keep | Production reward owner. |
| Data / ScriptableObject | `SearchableObjectDefinitionSO.cs` | `Assets/GAME/Scripts/Search/Runtime/Data/SearchableObjectDefinitionSO.cs` | Exists | Keep | Search content assets reference this file. |
| Data / ScriptableObject | `SearchEffect.cs` | `Assets/GAME/Scripts/Search/Runtime/Data/SearchEffect.cs` | Exists | Keep | Search effect data model. |
| Data / ScriptableObject | `SearchOutcome.cs` | `Assets/GAME/Scripts/Search/Runtime/Data/SearchOutcome.cs` | Exists | Keep | Search outcome model. |
| World / Field / Stage | `SearchableInteractable2D.cs` | `Assets/GAME/Scripts/Search/Runtime/SearchableInteractable2D.cs` | Exists | Keep | Test scene search interaction. |
| World / Field / Stage | `SearchObjectAnchor.cs` | `Assets/GAME/Scripts/Search/Runtime/SearchObjectAnchor.cs` | Exists | Keep | Search scene anchor. |
| World / Field / Stage | `SearchObjectVisualState2D.cs` | `Assets/GAME/Scripts/Search/Runtime/SearchObjectVisualState2D.cs` | Exists | Keep | Search visual state. |
| World / Field / Stage | `SearchResultRunner.cs` | `Assets/GAME/Scripts/Search/Runtime/SearchResultRunner.cs` | Exists | Keep | Search result executor. |
| Reward / Inventory / Progression | `SearchRewardManager.cs` | `Assets/GAME/Scripts/Search/Runtime/SearchRewardManager.cs` | Merge Candidate | Merge | Later route search rewards through RewardService. |
| Reward / Inventory / Progression | `SearchRewardProposal.cs` | `Assets/GAME/Scripts/Search/Runtime/SearchRewardProposal.cs` | Exists | Keep | Search reward DTO. |
| UI | `ItemAcquisitionHUD.cs` | `Assets/GAME/Scripts/Search/Runtime/UI/ItemAcquisitionHUD.cs` | Exists | Keep | Search UI; Test scene referenced. |
| UI | `SearchDecisionHUD.cs` | `Assets/GAME/Scripts/Search/Runtime/UI/SearchDecisionHUD.cs` | Exists | Keep | Search UI; Test scene referenced. |
| UI | `SearchResultHUD.cs` | `Assets/GAME/Scripts/Search/Runtime/UI/SearchResultHUD.cs` | Exists | Keep | Search UI; Test scene referenced. |
| Narrative / Dialogue / Choice | `CaseBoard.cs` | `Assets/GAME/Scripts/Story/CaseBoard.cs` | Exists | Keep | Case/story runtime support. |
| Data / ScriptableObject | `CaseFileDataSO.cs` | `Assets/GAME/Scripts/Story/CaseFileDataSO.cs` | Exists | Keep | Tutorial case asset references this file. |
| Narrative / Dialogue / Choice | `ChapterId.cs` | `Assets/GAME/Scripts/Story/ChapterId.cs` | Exists | Keep | Story chapter ID. |
| Narrative / Dialogue / Choice | `ChapterProgress.cs` | `Assets/GAME/Scripts/Story/ChapterProgress.cs` | Exists | Keep | Story chapter progress model. |
| Narrative / Dialogue / Choice | `ChapterProgressManager.cs` | `Assets/GAME/Scripts/Story/ChapterProgressManager.cs` | Exists | Keep | Scene-referenced story progress owner. |
| Narrative / Dialogue / Choice | `ChapterStartInteractionEventSO.cs` | `Assets/GAME/Scripts/Story/ChapterStartInteractionEventSO.cs` | Exists | Keep | Story interaction event data. |
| World / Field / Stage | `LocalTeleportInteractionEventSO.cs` | `Assets/GAME/Scripts/Story/LocalTeleportInteractionEventSO.cs` | Exists | Keep | Local travel event data. |
| Narrative / Dialogue / Choice | `DialogueRunner.cs` | `Assets/GAME/Scripts/Story/Runtime/Core/DialogueRunner.cs` | Exists | Keep | Production dialogue runner. |
| Narrative / Dialogue / Choice | `StoryFlagCondition.cs` | `Assets/GAME/Scripts/Story/Runtime/Core/StoryFlagCondition.cs` | Duplicate | Merge | Type overlaps story data condition enum; namespace-separated. |
| Narrative / Dialogue / Choice | `StoryFlagManager.cs` | `Assets/GAME/Scripts/Story/Runtime/Core/StoryFlagManager.cs` | Exists | Keep | Scene-referenced story flag owner. |
| Narrative / Dialogue / Choice | `ChoiceCondition.cs` | `Assets/GAME/Scripts/Story/Runtime/Data/ChoiceCondition.cs` | Exists | Keep | Story choice data. |
| Narrative / Dialogue / Choice | `ChoiceDefinition.cs` | `Assets/GAME/Scripts/Story/Runtime/Data/ChoiceDefinition.cs` | Exists | Keep | Story choice data. |
| Narrative / Dialogue / Choice | `ChoiceResult.cs` | `Assets/GAME/Scripts/Story/Runtime/Data/ChoiceResult.cs` | Exists | Keep | Story choice result data. |
| Data / ScriptableObject | `DialogueDefinitionSO.cs` | `Assets/GAME/Scripts/Story/Runtime/Data/DialogueDefinitionSO.cs` | Exists | Keep | Story dialogue data asset. |
| Narrative / Dialogue / Choice | `DialogueLine.cs` | `Assets/GAME/Scripts/Story/Runtime/Data/DialogueLine.cs` | Exists | Keep | Story dialogue line model. |
| Narrative / Dialogue / Choice | `StoryChoice.cs` | `Assets/GAME/Scripts/Story/Runtime/Data/StoryChoice.cs` | Exists | Keep | Story choice model. |
| Narrative / Dialogue / Choice | `StoryCondition.cs` | `Assets/GAME/Scripts/Story/Runtime/Data/StoryCondition.cs` | Duplicate | Merge | Type overlaps core condition enum; namespace-separated. |
| Narrative / Dialogue / Choice | `StoryEffect.cs` | `Assets/GAME/Scripts/Story/Runtime/Data/StoryEffect.cs` | Exists | Keep | Story effect data. |
| Data / ScriptableObject | `StoryEventDefinitionSO.cs` | `Assets/GAME/Scripts/Story/Runtime/Data/StoryEventDefinitionSO.cs` | Exists | Keep | Many Story assets reference this file. |
| Narrative / Dialogue / Choice | `StoryNode.cs` | `Assets/GAME/Scripts/Story/Runtime/Data/StoryNode.cs` | Exists | Keep | Story node model. |
| UI | `StoryInteractionAutoUIBootstrapper.cs` | `Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionAutoUIBootstrapper.cs` | Merge Candidate | Merge | UI bootstrap helper; later route through UI root policy. |
| UI | `StoryInteractionConfirmUI.cs` | `Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionConfirmUI.cs` | Exists | Keep | Story confirm UI. |
| Narrative / Dialogue / Choice | `StoryInteractionController.cs` | `Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionController.cs` | Exists | Keep | Routed interaction owner; scene-referenced. |
| Narrative / Dialogue / Choice | `StoryInteractionKind.cs` | `Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionKind.cs` | Exists | Keep | Story interaction enum. |
| UI | `StoryInteractionPromptUI.cs` | `Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionPromptUI.cs` | Exists | Keep | Scene-referenced prompt UI. |
| Narrative / Dialogue / Choice | `StoryEventRunner.cs` | `Assets/GAME/Scripts/Story/Runtime/StoryEventRunner.cs` | Exists | Keep | Production story event runner. |
| Narrative / Dialogue / Choice | `StoryEventTrigger2D.cs` | `Assets/GAME/Scripts/Story/Runtime/StoryEventTrigger2D.cs` | Exists | Keep | Story trigger. |
| Narrative / Dialogue / Choice | `StoryInteractable2D.cs` | `Assets/GAME/Scripts/Story/Runtime/StoryInteractable2D.cs` | Exists | Keep | Story interactable. |
| Narrative / Dialogue / Choice | `StoryProgressManager.cs` | `Assets/GAME/Scripts/Story/Runtime/StoryProgressManager.cs` | Exists | Keep | Story progress manager. |
| Narrative / Dialogue / Choice | `StorySpeakerAnchor.cs` | `Assets/GAME/Scripts/Story/Runtime/StorySpeakerAnchor.cs` | Exists | Keep | Dialogue speaker anchor. |
| UI | `ChoiceButtonUI.cs` | `Assets/GAME/Scripts/Story/Runtime/UI/ChoiceButtonUI.cs` | Exists | Keep | Story choice UI component. |
| UI | `ChoiceUIPanel.cs` | `Assets/GAME/Scripts/Story/Runtime/UI/ChoiceUIPanel.cs` | Exists | Keep | Story choice panel. |
| UI | `DialoguePanel.cs` | `Assets/GAME/Scripts/Story/Runtime/UI/DialoguePanel.cs` | Exists | Keep | Story dialogue panel; scene-referenced. |
| UI | `DialogueUIPanel.cs` | `Assets/GAME/Scripts/Story/Runtime/UI/DialogueUIPanel.cs` | Duplicate | Merge | Another dialogue UI panel; consolidate later. |
| UI | `StoryDialogueHUD.cs` | `Assets/GAME/Scripts/Story/Runtime/UI/StoryDialogueHUD.cs` | Exists | Keep | Story dialogue HUD. |
| UI | `TimedChoicePanel.cs` | `Assets/GAME/Scripts/Story/Runtime/UI/TimedChoicePanel.cs` | Exists | Keep | Story timed choice UI. |
| UI | `WorldDialogueBubble.cs` | `Assets/GAME/Scripts/Story/Runtime/UI/WorldDialogueBubble.cs` | Exists | Keep | World dialogue UI. |
| World / Field / Stage | `StoryDialogueTrigger2D.cs` | `Assets/GAME/Scripts/Story/Runtime/World/StoryDialogueTrigger2D.cs` | Exists | Keep | World story trigger. |
| World / Field / Stage | `SceneSpawnPoint.cs` | `Assets/GAME/Scripts/Story/SceneSpawnPoint.cs` | Exists | Keep | Scene spawn point; scene-referenced. |
| Core | `SceneTravelInteractionEventSO.cs` | `Assets/GAME/Scripts/Story/SceneTravelInteractionEventSO.cs` | Exists | Keep | Scene travel interaction data. |
| Core | `SceneTravelService.cs` | `Assets/GAME/Scripts/Story/SceneTravelService.cs` | Legacy Compat | Wrap | Existing scene travel owner should wrap/route through SceneFlowController. |
| Party / Character Runtime | `PersonaStat.cs` | `Assets/GAME/Scripts/Systems/Persona/PersonaStat.cs` | Exists | Keep | Character/persona stat enum. |
| Party / Character Runtime | `PersonaStatusManager.cs` | `Assets/GAME/Scripts/Systems/Persona/PersonaStatusManager.cs` | Exists | Keep | Persona status runtime; future character service may wrap it. |
| Data / ScriptableObject | `TitleMissionDefinitionSO.cs` | `Assets/GAME/Scripts/Title/Data/TitleMissionDefinitionSO.cs` | Exists | Keep | Title mission data. |
| UI | `TitleSceneAnimator.cs` | `Assets/GAME/Scripts/Title/Runtime/TitleSceneAnimator.cs` | Exists | Keep | Title scene animation. |
| UI | `TitleSceneController.cs` | `Assets/GAME/Scripts/Title/Runtime/TitleSceneController.cs` | Exists | Keep | Production title controller; duplicate demo controller exists. |
| Combat | `TutorialBattleStartInteractionEventSO.cs` | `Assets/GAME/Scripts/Tutorial/TutorialBattleStartInteractionEventSO.cs` | Exists | Keep | Tutorial combat event data. |
| Quest | `TutorialQuestCombatBridge.cs` | `Assets/GAME/Scripts/Tutorial/TutorialQuestCombatBridge.cs` | Exists | Keep | Tutorial bridge between quest and combat. |
| Content Production Pipeline | `TutorialReturnToOfficeInteractionEventSO.cs` | `Assets/GAME/Scripts/Tutorial/TutorialReturnToOfficeInteractionEventSO.cs` | Exists | Keep | Tutorial content event data. |
| Content Production Pipeline | `TutorialSceneInstaller.cs` | `Assets/GAME/Scripts/Tutorial/TutorialSceneInstaller.cs` | Exists | Keep | Tutorial scene setup helper. |
| Debugging / Legacy | `BattleTransitionController.cs` | `Assets/GAME/Scripts/UI/BattleTransitionController.cs` | Legacy Compat | Legacy | Old battle UI/scene bridge; scene-referenced. |
| UI | `DemoEndController.cs` | `Assets/GAME/Scripts/UI/DemoEndController.cs` | Archive Candidate | Archive | Demo-specific end UI; scene-referenced. |
| UI | `DemoEndInteractionEventSO.cs` | `Assets/GAME/Scripts/UI/DemoEndInteractionEventSO.cs` | Archive Candidate | Archive | Demo end data asset; asset-referenced. |
| UI | `GameUIRootController.cs` | `Assets/GAME/Scripts/UI/GameUIRootController.cs` | Exists | Keep | Planned UI root owner exists; scene-referenced. |
| UI | `RewardItemUI.cs` | `Assets/GAME/Scripts/UI/RewardItemUI.cs` | Exists | Keep | Reward item prefab references this script. |
| UI | `RewardUIPanel.cs` | `Assets/GAME/Scripts/UI/RewardUIPanel.cs` | Exists | Keep | Display-only reward panel; scene-referenced. |
| UI | `BagButtonHUD.cs` | `Assets/GAME/Scripts/UI/Runtime/BagButtonHUD.cs` | Exists | Keep | Runtime HUD component. |
| UI | `CombatUIDialogueBlocker.cs` | `Assets/GAME/Scripts/UI/Runtime/CombatUIDialogueBlocker.cs` | Merge Candidate | Merge | UI state policy should later live under router/root ownership. |
| UI | `CurrencyHUD.cs` | `Assets/GAME/Scripts/UI/Runtime/CurrencyHUD.cs` | Exists | Keep | Runtime currency HUD. |
| UI | `DialogueLogHUD.cs` | `Assets/GAME/Scripts/UI/Runtime/DialogueLogHUD.cs` | Exists | Keep | Runtime dialogue log HUD. |
| UI | `MapHUD.cs` | `Assets/GAME/Scripts/UI/Runtime/MapHUD.cs` | Exists | Keep | Runtime map HUD. |
| UI | `MissionTrackerHUD.cs` | `Assets/GAME/Scripts/UI/Runtime/MissionTrackerHUD.cs` | Exists | Keep | Runtime mission tracker HUD. |
| UI | `OverworldHUDRoot.cs` | `Assets/GAME/Scripts/UI/Runtime/OverworldHUDRoot.cs` | Exists | Keep | Runtime field HUD root. |
| UI | `StatusHUD.cs` | `Assets/GAME/Scripts/UI/Runtime/StatusHUD.cs` | Exists | Keep | Runtime status HUD. |
| UI | `ScreenFader.cs` | `Assets/GAME/Scripts/UI/ScreenFader.cs` | Exists | Keep | Scene transition UI utility. |
| UI | `UIScreenRouter.cs` | `Assets/GAME/Scripts/UI/UIScreenRouter.cs` | Exists | Keep | Planned UI router exists; scene-referenced. |

## Existing Production-Ready Files

- Core owners: `GameState.cs`, `GameStateMachine.cs`, `RuntimeBootstrapper.cs`, `GameFlowController.cs`, `SceneFlowController.cs`, `SaveLoadService.cs`.
- Input owners: `GameInputInstaller.cs`, `InputService.cs`, `InputRouter.cs`, `Player/Runtime/PlayerInputController.cs`.
- Combat owners: `CombatEntryPoint.cs`, `CombatBootstrapper.cs`, `CombatStateMachine.cs`, `CombatTurnResolver.cs`, `CombatDirector.cs`, `CombatEncounterTrigger2D.cs`, `CombatEncounterGroup.cs`, `FieldCombatantFactory.cs`.
- Reward owners: `RewardService.cs`, `RewardResult.cs`, with `CombatRewardUIBinder.cs` as current integration.
- Quest wrappers: `QuestRuntime.cs`, `QuestObjectiveTracker.cs`, `QuestCompletionFlow.cs`.
- Story owners: `StoryEventRunner.cs`, `DialogueRunner.cs`, `StoryInteractionController.cs`, `StoryFlagManager.cs`.
- UI owners: `GameUIRootController.cs`, `UIScreenRouter.cs`, `RewardUIPanel.cs`, combat/story HUD files.

## Files That Should Be Renamed Later

| Current File | Suggested Planned Name | Reason |
|---|---|---|
| `Assets/GAME/Scripts/Input/inputactions.cs` | `InputActions.cs` | Generated class/file casing is nonstandard. Regenerate through Unity Input System rather than manual rename. |
| `Assets/GAME/Scripts/Player/Runtime/PlayerMotor2D_New.cs` | `PlayerMotor2D.cs` | Preferred runtime motor should eventually own the canonical name after old motor is retired. |
| `Assets/GAME/Scripts/Quest/QuestDataSO.cs` | `QuestDefinitionSO.cs` | Name should match production architecture after asset migration plan. |
| `Assets/GAME/Scripts/DemoMission/Data/DemoMissionDefinitionSO.cs` | `MissionDefinitionSO.cs` or `QuestDefinitionSO.cs` | Demo asset-backed SO; rename only after content migration. |
| `Assets/GAME/Scripts/Demo/TitleSceneController.cs` | Archive or demo-specific name | Duplicates `Title/Runtime/TitleSceneController.cs`. |

## Files That Should Be Moved Later

| Current File | Suggested Destination | Reason |
|---|---|---|
| `Assets/GAME/Scripts/Player/PlayerController2D.cs` | `Assets/GAME/Scripts/Legacy/Player/` | Old input/movement route. |
| `Assets/GAME/Scripts/Player/PlayerInputUnity.cs` | `Assets/GAME/Scripts/Legacy/Player/` | Old `UnityEngine.Input` route. |
| `Assets/GAME/Scripts/Input/OverworldInputAdapter.cs` | `Assets/GAME/Scripts/Legacy/Input/` or merge into planned adapter | Parallel direct input route. |
| `Assets/GAME/Scripts/Player/Overworld/OverworldInputBridge.cs` | `Assets/GAME/Scripts/Legacy/Input/` | Parallel Send Messages route. |
| `Assets/GAME/Scripts/Demo/**` | `Assets/GAME/Scripts/Legacy/Demo/` or content pipeline folder | Demo-specific code should not sit beside production owners forever. |
| `Assets/GAME/Scripts/UI/DemoEndController.cs` | `Assets/GAME/Scripts/Legacy/Demo/` | Demo-specific end UI. |
| `Assets/GAME/Scripts/UI/DemoEndInteractionEventSO.cs` | `Assets/GAME/Scripts/Legacy/Demo/` | Demo-specific event asset class. |

## Files That Should Be Merged Later

| Merge Candidate | Planned Owner | Notes |
|---|---|---|
| `FieldEnemy.cs` | `CombatEncounterTrigger2D`, `FieldEnemyPatrolAI2D`, quest objective bridge | Split AI, combat entry, and mission progress responsibilities. |
| DemoMission runtime files | `QuestRuntime`, `QuestObjectiveTracker`, `QuestCompletionFlow` | Promote behavior after asset and scene references are mapped. |
| DemoMission UI files | `MissionHUD`, `QuestTrackerUI`, UI router/root | Keep until demo flow migrates. |
| `RewardApplier`, `SearchRewardManager`, random loot interaction events | `RewardService` + future `RewardRequest` | Centralize reward grants. |
| Direct UI controllers and blockers | `UIScreenRouter` + `GameUIRootController` | Keep scene behavior stable while routing is wired. |
| NonCombat dialogue/choice paths | Story runtime dialogue/choice owners | Avoid parallel narrative implementations long term. |
| `StoryFlagCondition.cs` and `StoryCondition.cs` enum overlap | One story condition model | Namespaces prevent compile conflict, but concept is duplicated. |

## Files That Should Remain Legacy Temporarily

- `Assets/GAME/Scripts/Legacy/Battle/BattleTransitionRequest.cs`
- `Assets/GAME/Scripts/Legacy/Battle/BattleTrigger2D.cs`
- `Assets/GAME/Scripts/Legacy/Battle/SeamlessBattleManager.cs`
- `Assets/GAME/Scripts/Legacy/StoryDeprecated/StoryFlagManagerDeprecated.cs`
- `Assets/GAME/Scripts/UI/BattleTransitionController.cs`
- `Assets/GAME/Scripts/Player/PlayerController2D.cs`
- `Assets/GAME/Scripts/Player/PlayerInputUnity.cs`
- `Assets/GAME/Scripts/Input/OverworldInputAdapter.cs`
- `Assets/GAME/Scripts/Player/Overworld/OverworldInputBridge.cs`
- DemoMission files until Dungeon 1 and tutorial assets are migrated.

## Files Likely Unnecessary But Needing Reference Checks

| File | Current Recommendation | Reason |
|---|---|---|
| `Assets/GAME/Scripts/Player/Overworld/OverworldPlayerDriver.cs` | Delete Later | Appears to be a placeholder, but check Inspector first. |
| `Assets/GAME/Scripts/Common/Damage/SimpleDamageable.cs` | Archive | Useful test damage component, unclear production use. |
| `Assets/GAME/Scripts/Demo/TitleSceneController.cs` | Archive | Duplicate title controller responsibility. |
| `Assets/GAME/Scripts/UI/DemoEndController.cs` | Archive | Demo-specific and scene-referenced. |
| `Assets/GAME/Scripts/UI/DemoEndInteractionEventSO.cs` | Archive | Demo-specific and asset-referenced. |
| `Assets/GAME/Scripts/Debugging/Combat/*.cs` | Debugging | Keep for test scenes or wrap in editor/test ownership. |

## Recommended Next Implementation Order

1. Open Unity and run missing-script/reference checks for `Demo`, `Dungeon 1`, `InGame`, `Test`, `TitleScene`, and `TutorialScene`.
2. Confirm `CombatEntryPoint` is the only production combat start route in active scenes; leave Legacy battle files intact until references are cleared.
3. Split `FieldEnemy` responsibilities into field AI, encounter trigger/group, and quest objective bridge.
4. Promote DemoMission data/runtime behavior into `MissionDefinitionSO`/`QuestRuntime`/`QuestObjectiveTracker` without renaming assets first.
5. Route search, random loot, and interaction rewards through `RewardService` and a future `RewardRequest`.
6. Finish scene wiring for `GameUIRootController` and `UIScreenRouter`; then merge direct visibility blockers/controllers.
7. Retire old input routes after confirming the preferred `GameInputInstaller -> InputService -> InputRouter -> PlayerInputController` path is active.
8. Add save/progression schema files only after runtime data ownership is stable.
9. Add missing Party, Audio, Settings/Rebind, Cutscene/Timeline, and Localization owners as feature work, not cleanup work.
10. Convert content validators and tutorial/demo setup scripts into a formal content production pipeline after runtime architecture is stable.
