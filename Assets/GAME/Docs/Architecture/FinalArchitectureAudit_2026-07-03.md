# Final Architecture Audit - 2026-07-03

Repository: `SDapple970/GAME_002`  
Unity project root: current repository root  
Audit target: full GAME002 architecture, not BIC/demo-only vertical slice  
Scope: `Assets/GAME/Scripts/**/*.cs`, plus scene/prefab/data reference risks noted from `Assets/GAME`.

No runtime files were modified, moved, renamed, deleted, or namespace-changed for this audit.

## 1. Summary

- Current script count under `Assets/GAME/Scripts`: 261 `.cs` files.
- Current top-level script folders: `Camera`, `Combat`, `Common`, `Core`, `Cutscene`, `Debugging`, `Demo`, `DemoMission`, `Dialogue`, `Enemies`, `Enemy`, `Input`, `Interaction`, `Legacy`, `Mission`, `NonCombat`, `Office`, `Player`, `Quest`, `Reward`, `Search`, `Story`, `Systems`, `Title`, `Tutorial`, `UI`.
- Requested final-architecture folders that do not currently exist directly under `Assets/GAME/Scripts`: `World`, `Narrative`, `Inventory`, `Data`, `Daily`, `Exploration`, `Social`, `Shop`, `Party`, `Audio`, `Localization`.
- The current project already has credible production owners for Core, input routing, combat entry/resolution, reward, story/dialogue, quest wrapper, UI router, scene flow, and save/load wrapper.
- The most important refactor target is not deletion or file moves. It is ownership consolidation around one route at a time, starting with `FieldEnemy` and the combat-start path because it mixes field AI, combat entry, legacy battle fallback, and demo mission progress.
- Missing final systems are real architecture gaps: Daily/Calendar/Settlement, Exploration resources/conditions, Social stats, Choice requirement evaluation, Shop/Supply/PreMission supply.

## 2. Compile Status

Command run:

```text
dotnet build Assembly-CSharp.csproj --no-restore --nologo
```

Result:

```text
Exit code: 1
Build failed.
0 warnings
0 errors
```

Interpretation:

- No C# compiler errors were reported by the available `dotnet` check.
- The nonzero exit with `0 warnings / 0 errors` appears to be Unity-generated project/tooling behavior, consistent with earlier repo notes.
- `Unity` was not discoverable on `PATH`, so a real Unity editor/batchmode compile was not confirmed in this environment.
- No compile-error refactor should be attempted from this audit alone. Next validation should be an editor compile or Unity batchmode run.

## 3. File Ownership Map

### Core

- `Assets/GAME/Scripts/Core/GameFlowController.cs`
- `Assets/GAME/Scripts/Core/GameState.cs`
- `Assets/GAME/Scripts/Core/GameStateMachine.cs`
- `Assets/GAME/Scripts/Core/RuntimeBootstrapper.cs`
- `Assets/GAME/Scripts/Core/SceneFlowController.cs`
- `Assets/GAME/Scripts/Story/SceneTravelInteractionEventSO.cs`
- `Assets/GAME/Scripts/Story/SceneTravelService.cs`

Notes: `SceneTravelService` is a compatibility scene-travel owner and should eventually wrap or defer to `SceneFlowController`.

### Input

- `Assets/GAME/Scripts/Input/GameInputInstaller.cs`
- `Assets/GAME/Scripts/Input/inputactions.cs`
- `Assets/GAME/Scripts/Input/InputDeviceWatcher.cs`
- `Assets/GAME/Scripts/Input/InputRouter.cs`
- `Assets/GAME/Scripts/Input/InputService.cs`
- `Assets/GAME/Scripts/Input/OverworldInputAdapter.cs`
- `Assets/GAME/Scripts/Input/RebindSaveLoad.cs`
- `Assets/GAME/Scripts/Player/IPlayerInput.cs`
- `Assets/GAME/Scripts/Player/PlayerInputUnity.cs`
- `Assets/GAME/Scripts/Player/Overworld/OverworldInputBridge.cs`
- `Assets/GAME/Scripts/Player/Runtime/PlayerInputController.cs`

Notes: production route is `GameInputInstaller -> InputService -> InputRouter`. Old direct input routes remain for compatibility.

### World

- `Assets/GAME/Scripts/Camera/CameraFollow2D.cs`
- `Assets/GAME/Scripts/Enemies/Runtime/FieldEnemyAnimator2D.cs`
- `Assets/GAME/Scripts/Enemies/Runtime/FieldEnemyMotor2D.cs`
- `Assets/GAME/Scripts/Enemies/Runtime/FieldEnemyPatrolAI2D.cs`
- `Assets/GAME/Scripts/Enemy/Overworld/EnemyAnimator2D.cs`
- `Assets/GAME/Scripts/Enemy/Overworld/OverworldEnemyAI.cs`
- `Assets/GAME/Scripts/Interaction/IInteractable.cs`
- `Assets/GAME/Scripts/Interaction/InteractableObject.cs`
- `Assets/GAME/Scripts/Interaction/InteractionController.cs`
- `Assets/GAME/Scripts/Interaction/InteractionEventSO.cs`
- `Assets/GAME/Scripts/Interaction/ObjectStateChangeEventSO.cs`
- `Assets/GAME/Scripts/Interaction/InteractionPromptUI.cs`
- `Assets/GAME/Scripts/NonCombat/Interaction/INonCombatInteractable.cs`
- `Assets/GAME/Scripts/NonCombat/Interaction/Interactable2D.cs`
- `Assets/GAME/Scripts/NonCombat/Interaction/InteractionDetector2D.cs`
- `Assets/GAME/Scripts/Office/OfficeHotspot2D.cs`
- `Assets/GAME/Scripts/Story/LocalTeleportInteractionEventSO.cs`
- `Assets/GAME/Scripts/Story/Runtime/World/StoryDialogueTrigger2D.cs`
- `Assets/GAME/Scripts/Story/SceneSpawnPoint.cs`

Notes: `Assets/GAME/Scripts/Combat/FieldEnemy.cs` is listed under Combat and Mixed Responsibility because it owns world behavior too.

### Combat

- `Assets/GAME/Scripts/Camera/CombatCameraController.cs`
- `Assets/GAME/Scripts/Combat/CombatLogHUD.cs`
- `Assets/GAME/Scripts/Combat/FieldEnemy.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Actions/SkillRunner.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Actions/SoSkill.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Adapters/CombatHpComponent.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Adapters/CombatKeywordComponent.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Adapters/CombatSkillLoadoutComponent.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Adapters/CombatStartRequest.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Adapters/DummyCombatant.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Adapters/DummyCombatantFactory.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Adapters/FieldCombatantAdapter.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Adapters/FieldCombatantFactory.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Adapters/HpAccessor.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Adapters/ICombatantFactory.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Adapters/OpeningEffectApplier.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Animation/CombatantAnimationDriver.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatBootstrapper.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatEndEvaluator.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatEntryPoint.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatFlowOrchestrator.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatPlanValidator.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatResultBuilder.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatStateMachine.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatTimeline.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatTurnResolver.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/InspirationPool.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/KnowledgeBook.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/SkillBook.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/StaggerSystem.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Effects/CombatDirector.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Environment/CombatEnvironment.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatDemoFlowController.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatEncounterGroup.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatEncounterTrigger2D.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatFieldLock.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatFormationManager.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatStateSyncer.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/EncounterAdvantageApplier.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Model/ActionPlan.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Model/CombatEndReason.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Model/CombatEnums.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Model/CombatIds.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Model/CombatPlanDraft.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Model/CombatPlaybook.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Model/CombatResult.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Model/CombatSession.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Model/CombatTurn.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Model/ICombatant.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Model/ISkill.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Model/KeywordMask.cs`
- `Assets/GAME/Scripts/Common/Damage/IDamageable.cs`
- `Assets/GAME/Scripts/Common/Damage/SimpleDamageable.cs`
- `Assets/GAME/Scripts/Player/Runtime/PlayerFieldAttackController.cs`
- `Assets/GAME/Scripts/Tutorial/TutorialBattleStartInteractionEventSO.cs`

Notes: `DummyCombatant*` and `SimpleDamageable` are support/test flavored, but compile with runtime code.

### Narrative

- `Assets/GAME/Scripts/Dialogue/TimedChoiceDialogueEventSO.cs`
- `Assets/GAME/Scripts/Dialogue/TimedChoiceDialoguePanel.cs`
- `Assets/GAME/Scripts/Dialogue/TimedChoiceOption.cs`
- `Assets/GAME/Scripts/Interaction/DialogueInteractionEventSO.cs`
- `Assets/GAME/Scripts/NonCombat/Chapter/ChapterDefinitionSO.cs`
- `Assets/GAME/Scripts/NonCombat/Chapter/NonCombatChapterProgressManager.cs`
- `Assets/GAME/Scripts/NonCombat/Choice/ChoiceOutcome.cs`
- `Assets/GAME/Scripts/NonCombat/Choice/ChoiceRunner.cs`
- `Assets/GAME/Scripts/NonCombat/Choice/NonCombatChoiceCondition.cs`
- `Assets/GAME/Scripts/NonCombat/Dialogue/DialogueChoice.cs`
- `Assets/GAME/Scripts/NonCombat/Dialogue/DialogueController.cs`
- `Assets/GAME/Scripts/NonCombat/Dialogue/DialogueNodeSO.cs`
- `Assets/GAME/Scripts/NonCombat/Progress/StoryFlagDatabase.cs`
- `Assets/GAME/Scripts/Story/CaseBoard.cs`
- `Assets/GAME/Scripts/Story/ChapterId.cs`
- `Assets/GAME/Scripts/Story/ChapterProgress.cs`
- `Assets/GAME/Scripts/Story/ChapterProgressManager.cs`
- `Assets/GAME/Scripts/Story/ChapterStartInteractionEventSO.cs`
- `Assets/GAME/Scripts/Story/Runtime/Core/DialogueRunner.cs`
- `Assets/GAME/Scripts/Story/Runtime/Core/StoryFlagCondition.cs`
- `Assets/GAME/Scripts/Story/Runtime/Core/StoryFlagManager.cs`
- `Assets/GAME/Scripts/Story/Runtime/Data/ChoiceCondition.cs`
- `Assets/GAME/Scripts/Story/Runtime/Data/ChoiceDefinition.cs`
- `Assets/GAME/Scripts/Story/Runtime/Data/ChoiceResult.cs`
- `Assets/GAME/Scripts/Story/Runtime/Data/DialogueLine.cs`
- `Assets/GAME/Scripts/Story/Runtime/Data/StoryChoice.cs`
- `Assets/GAME/Scripts/Story/Runtime/Data/StoryCondition.cs`
- `Assets/GAME/Scripts/Story/Runtime/Data/StoryEffect.cs`
- `Assets/GAME/Scripts/Story/Runtime/Data/StoryNode.cs`
- `Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionController.cs`
- `Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionKind.cs`
- `Assets/GAME/Scripts/Story/Runtime/StoryEventRunner.cs`
- `Assets/GAME/Scripts/Story/Runtime/StoryEventTrigger2D.cs`
- `Assets/GAME/Scripts/Story/Runtime/StoryInteractable2D.cs`
- `Assets/GAME/Scripts/Story/Runtime/StoryProgressManager.cs`
- `Assets/GAME/Scripts/Story/Runtime/StorySpeakerAnchor.cs`

Notes: there are at least three narrative generations: `Dialogue`, `NonCombat`, and `Story/Runtime`.

### Quest

- `Assets/GAME/Scripts/Demo/RescueNpcObjectiveEventSO.cs`
- `Assets/GAME/Scripts/Demo/DungeonObjectiveManager.cs`
- `Assets/GAME/Scripts/Demo/ObjectiveEnemyMarker.cs`
- `Assets/GAME/Scripts/DemoMission/Data/DemoMissionDefinitionSO.cs`
- `Assets/GAME/Scripts/DemoMission/Data/MonsterBriefingEntry.cs`
- `Assets/GAME/Scripts/DemoMission/Data/RescueNpcDefinitionSO.cs`
- `Assets/GAME/Scripts/DemoMission/Runtime/DemoMissionRuntime.cs`
- `Assets/GAME/Scripts/DemoMission/Runtime/Ending/DemoRescueNpcEndFlow.cs`
- `Assets/GAME/Scripts/DemoMission/Runtime/MissionCompletionController.cs`
- `Assets/GAME/Scripts/DemoMission/Runtime/MissionObjectiveTracker.cs`
- `Assets/GAME/Scripts/DemoMission/Runtime/RescueNpcActor.cs`
- `Assets/GAME/Scripts/Mission/Runtime/Data/MissionDefinitionSO.cs`
- `Assets/GAME/Scripts/Mission/Runtime/Data/MissionObjective.cs`
- `Assets/GAME/Scripts/Mission/Runtime/MissionManager.cs`
- `Assets/GAME/Scripts/Quest/QuestAdvanceInteractionEventSO.cs`
- `Assets/GAME/Scripts/Quest/QuestCompleteInteractionEventSO.cs`
- `Assets/GAME/Scripts/Quest/QuestCompletionFlow.cs`
- `Assets/GAME/Scripts/Quest/QuestDataSO.cs`
- `Assets/GAME/Scripts/Quest/QuestId.cs`
- `Assets/GAME/Scripts/Quest/QuestManager.cs`
- `Assets/GAME/Scripts/Quest/QuestObjectiveTracker.cs`
- `Assets/GAME/Scripts/Quest/QuestProgress.cs`
- `Assets/GAME/Scripts/Quest/QuestRuntime.cs`
- `Assets/GAME/Scripts/Quest/QuestStartInteractionEventSO.cs`
- `Assets/GAME/Scripts/Quest/QuestStepData.cs`
- `Assets/GAME/Scripts/Tutorial/TutorialQuestCombatBridge.cs`
- `Assets/GAME/Scripts/Tutorial/TutorialReturnToOfficeInteractionEventSO.cs`
- `Assets/GAME/Scripts/Tutorial/TutorialSceneInstaller.cs`

Notes: `QuestRuntime` exists, but DemoMission and Mission still hold active scene/content responsibilities.

### Reward

- `Assets/GAME/Scripts/Combat/Runtime/UI/CombatRewardUIBinder.cs`
- `Assets/GAME/Scripts/Interaction/RandomLootEntry.cs`
- `Assets/GAME/Scripts/Interaction/RandomLootInteractionEventSO.cs`
- `Assets/GAME/Scripts/Interaction/RewardInteractionEventSO.cs`
- `Assets/GAME/Scripts/NonCombat/Reward/RewardApplier.cs`
- `Assets/GAME/Scripts/Reward/RewardResult.cs`
- `Assets/GAME/Scripts/Reward/RewardService.cs`
- `Assets/GAME/Scripts/Search/Runtime/SearchRewardManager.cs`
- `Assets/GAME/Scripts/Search/Runtime/SearchRewardProposal.cs`

Notes: production owner is `RewardService`; several older/event-specific reward paths still exist.

### Inventory

- `Assets/GAME/Scripts/NonCombat/Inventory/CurrencyWallet.cs`
- `Assets/GAME/Scripts/NonCombat/Inventory/InventoryService.cs`
- `Assets/GAME/Scripts/NonCombat/Inventory/ItemDefinitionSO.cs`
- `Assets/GAME/Scripts/UI/Runtime/CurrencyHUD.cs`

Notes: inventory and currency are under `NonCombat`, not final `Inventory`.

### Progression

- `Assets/GAME/Scripts/NonCombat/Progress/GameProgressState.cs`
- `Assets/GAME/Scripts/Systems/Persona/PersonaStat.cs`
- `Assets/GAME/Scripts/Systems/Persona/PersonaStatusManager.cs`

Notes: no final `Progression` folder or `CharacterProgressionService` exists. Persona is closest to social/character stat progression.

### UI

- `Assets/GAME/Scripts/Combat/Runtime/UI/CombatantWidget.cs`
- `Assets/GAME/Scripts/Combat/Runtime/UI/CombatInspirationHUD.cs`
- `Assets/GAME/Scripts/Combat/Runtime/UI/CombatPlanningHUD.cs`
- `Assets/GAME/Scripts/Combat/Runtime/UI/CombatUIRootController.cs`
- `Assets/GAME/Scripts/Combat/Runtime/UI/CombatWidgetManager.cs`
- `Assets/GAME/Scripts/Demo/ContractDocumentPanel.cs`
- `Assets/GAME/Scripts/Demo/TitleSceneController.cs`
- `Assets/GAME/Scripts/DemoMission/Runtime/Ending/DemoEndPanelController.cs`
- `Assets/GAME/Scripts/DemoMission/UI/CaseFileAcceptController.cs`
- `Assets/GAME/Scripts/DemoMission/UI/CaseFilePanel.cs`
- `Assets/GAME/Scripts/DemoMission/UI/MissionCompletePanel.cs`
- `Assets/GAME/Scripts/Mission/Runtime/UI/MissionHUD.cs`
- `Assets/GAME/Scripts/NonCombat/Dialogue/NonCombatDialogueUIPanel.cs`
- `Assets/GAME/Scripts/Office/CaseFileDocumentPanel.cs`
- `Assets/GAME/Scripts/Office/OfficeMenuController.cs`
- `Assets/GAME/Scripts/Quest/QuestTrackerUI.cs`
- `Assets/GAME/Scripts/Search/Runtime/UI/ItemAcquisitionHUD.cs`
- `Assets/GAME/Scripts/Search/Runtime/UI/SearchDecisionHUD.cs`
- `Assets/GAME/Scripts/Search/Runtime/UI/SearchResultHUD.cs`
- `Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionAutoUIBootstrapper.cs`
- `Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionConfirmUI.cs`
- `Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionPromptUI.cs`
- `Assets/GAME/Scripts/Story/Runtime/UI/ChoiceButtonUI.cs`
- `Assets/GAME/Scripts/Story/Runtime/UI/ChoiceUIPanel.cs`
- `Assets/GAME/Scripts/Story/Runtime/UI/DialoguePanel.cs`
- `Assets/GAME/Scripts/Story/Runtime/UI/DialogueUIPanel.cs`
- `Assets/GAME/Scripts/Story/Runtime/UI/StoryDialogueHUD.cs`
- `Assets/GAME/Scripts/Story/Runtime/UI/TimedChoicePanel.cs`
- `Assets/GAME/Scripts/Story/Runtime/UI/WorldDialogueBubble.cs`
- `Assets/GAME/Scripts/Title/Runtime/TitleSceneAnimator.cs`
- `Assets/GAME/Scripts/Title/Runtime/TitleSceneController.cs`
- `Assets/GAME/Scripts/UI/BattleTransitionController.cs`
- `Assets/GAME/Scripts/UI/DemoEndController.cs`
- `Assets/GAME/Scripts/UI/GameUIRootController.cs`
- `Assets/GAME/Scripts/UI/RewardItemUI.cs`
- `Assets/GAME/Scripts/UI/RewardUIPanel.cs`
- `Assets/GAME/Scripts/UI/Runtime/BagButtonHUD.cs`
- `Assets/GAME/Scripts/UI/Runtime/CombatUIDialogueBlocker.cs`
- `Assets/GAME/Scripts/UI/Runtime/DialogueLogHUD.cs`
- `Assets/GAME/Scripts/UI/Runtime/MapHUD.cs`
- `Assets/GAME/Scripts/UI/Runtime/MissionTrackerHUD.cs`
- `Assets/GAME/Scripts/UI/Runtime/OverworldHUDRoot.cs`
- `Assets/GAME/Scripts/UI/Runtime/StatusHUD.cs`
- `Assets/GAME/Scripts/UI/ScreenFader.cs`
- `Assets/GAME/Scripts/UI/UIScreenRouter.cs`

Notes: production owner exists as `UIScreenRouter` + `GameUIRootController`, but many panels still own direct `SetActive`, `Show`, and `Hide`.

### Data

- `Assets/GAME/Scripts/Combat/Data/OpeningEffectSO.cs`
- `Assets/GAME/Scripts/Combat/Data/SkillDefinitionSO.cs`
- `Assets/GAME/Scripts/Combat/Data/SkillMovementMode.cs`
- `Assets/GAME/Scripts/Demo/ContractDataSO.cs`
- `Assets/GAME/Scripts/Search/Runtime/Data/SearchableObjectDefinitionSO.cs`
- `Assets/GAME/Scripts/Search/Runtime/Data/SearchEffect.cs`
- `Assets/GAME/Scripts/Search/Runtime/Data/SearchOutcome.cs`
- `Assets/GAME/Scripts/Story/CaseFileDataSO.cs`
- `Assets/GAME/Scripts/Story/Runtime/Data/DialogueDefinitionSO.cs`
- `Assets/GAME/Scripts/Story/Runtime/Data/StoryEventDefinitionSO.cs`
- `Assets/GAME/Scripts/Title/Data/TitleMissionDefinitionSO.cs`
- `Assets/GAME/Scripts/UI/DemoEndInteractionEventSO.cs`

Notes: Data is distributed by feature folder. This is acceptable short-term because Unity asset GUIDs make SO moves/renames risky.

### SaveLoad

- `Assets/GAME/Scripts/Core/SaveLoadService.cs`
- `Assets/GAME/Scripts/NonCombat/Save/SaveData.cs`
- `Assets/GAME/Scripts/NonCombat/Save/SaveManager.cs`
- `Assets/GAME/Scripts/NonCombat/Save/SaveSerializer.cs`

Notes: `SaveLoadService` is the production wrapper; `SaveManager` remains compatibility and has debug F5/F6 input.

### Movement

- `Assets/GAME/Scripts/Player/FieldSkillCaster.cs`
- `Assets/GAME/Scripts/Player/Overworld/OverworldAttack2D.cs`
- `Assets/GAME/Scripts/Player/Overworld/OverworldPlayerDriver.cs`
- `Assets/GAME/Scripts/Player/Overworld/OverworldPlayerGlue.cs`
- `Assets/GAME/Scripts/Player/OverworldPlayerController.cs`
- `Assets/GAME/Scripts/Player/PlayerAnimator2D.cs`
- `Assets/GAME/Scripts/Player/PlayerController2D.cs`
- `Assets/GAME/Scripts/Player/PlayerMotor2D.cs`
- `Assets/GAME/Scripts/Player/Runtime/PlayerAnimationController.cs`
- `Assets/GAME/Scripts/Player/Runtime/PlayerMotor2D_New.cs`

Notes: preferred route appears to be `Player/Runtime`, but old movement stack is still scene-referenced.

### Party

- No dedicated party runtime files found.

Notes: `PersonaStatusManager` exists under Progression/Social-adjacent ownership, but it is not a party/bond system.

### Daily

- No Daily, Calendar, or Settlement runtime files found.

### Exploration

- `Assets/GAME/Scripts/Search/Runtime/SearchableInteractable2D.cs`
- `Assets/GAME/Scripts/Search/Runtime/SearchObjectAnchor.cs`
- `Assets/GAME/Scripts/Search/Runtime/SearchObjectVisualState2D.cs`
- `Assets/GAME/Scripts/Search/Runtime/SearchResultRunner.cs`

Notes: current exploration is object search/interaction, not final exploration resource/condition runtime.

### Social

- `Assets/GAME/Scripts/Systems/Persona/PersonaStat.cs`
- `Assets/GAME/Scripts/Systems/Persona/PersonaStatusManager.cs`

Notes: closest existing social-stat equivalent, but no final `SocialStatService` exists.

### Shop

- No shop, supply loadout, or pre-mission supply files found.

### Audio

- No dedicated audio service files found. Audio appears only as localized fields/usages such as skill SFX and cutscene/video audio.

### Cutscene

- `Assets/GAME/Scripts/Cutscene/Runtime/CutscenePlaybackRequest.cs`
- `Assets/GAME/Scripts/Cutscene/Runtime/MissionCompleteCutsceneController.cs`

Notes: mission-complete cutscene support exists, but no general cutscene/timeline service exists.

### Localization

- No localization runtime or data files found.

### Debugging

- `Assets/GAME/Scripts/Debugging/Combat/CombatAutoPlanner.cs`
- `Assets/GAME/Scripts/Debugging/Combat/CombatFieldCallDebug.cs`
- `Assets/GAME/Scripts/Debugging/Combat/CombatSkillDebugInvoker.cs`
- `Assets/GAME/Scripts/Debugging/Combat/CombatStartSmokeTest.cs`
- `Assets/GAME/Scripts/Debugging/Combat/CombatTestRunner.cs`
- `Assets/GAME/Scripts/Debugging/Combat/InspirationDebugHotkey.cs`
- `Assets/GAME/Scripts/Debugging/StoryInteractionDebugHotkey.cs`
- `Assets/GAME/Scripts/Debugging/VerticalSliceSceneValidator.cs`

Notes: several debug files are still scene/test-scene referenced and should not be deleted.

### Legacy

- `Assets/GAME/Scripts/Legacy/Battle/BattleTransitionRequest.cs`
- `Assets/GAME/Scripts/Legacy/Battle/BattleTrigger2D.cs`
- `Assets/GAME/Scripts/Legacy/Battle/SeamlessBattleManager.cs`
- `Assets/GAME/Scripts/Legacy/StoryDeprecated/StoryFlagManagerDeprecated.cs`
- `Assets/GAME/Scripts/UI/BattleTransitionController.cs`

Notes: `Assets/GAME/Scripts/Battle` no longer exists as a top-level folder; old battle code is under `Legacy/Battle`, with `BattleTransitionController` still in `UI`.

### Unknown / Needs Review

- None by path. The files are classifiable by current responsibility, but several have mixed ownership and are listed below.

## 4. Production Owner Map

| Ownership Area | Current Production Owner | Support / Compatibility Files | Audit Result |
|---|---|---|---|
| GameState | `Core/GameStateMachine.cs`, `Core/GameState.cs` | `RuntimeBootstrapper.cs`, direct state calls in UI/combat/story | Production owner exists. |
| Scene flow | `Core/SceneFlowController.cs` | `Story/SceneTravelService.cs`, direct scene/event scripts | Production owner exists, but compatibility route remains. |
| Input routing | `Input/GameInputInstaller.cs`, `Input/InputService.cs`, `Input/InputRouter.cs` | `OverworldInputAdapter.cs`, `OverworldInputBridge.cs`, `PlayerInputUnity.cs`, debug hotkeys | Production owner exists; old routes remain. |
| Combat entry | `Combat/Runtime/Core/CombatEntryPoint.cs` | `CombatEncounterTrigger2D.cs`, `PlayerFieldAttackController.cs`, `FieldEnemy.cs`, legacy `BattleTransitionController.cs` | Production owner exists; duplicate start routes remain. |
| Combat turn resolution | `CombatTurnResolver.cs`, `CombatStateMachine.cs`, `CombatDirector.cs` | `CombatFlowOrchestrator.cs`, HUD components | Production owner exists. |
| Combat UI | `CombatPlanningHUD.cs`, `CombatUIRootController.cs`, `CombatWidgetManager.cs`, `CombatRewardUIBinder.cs` | `UIScreenRouter.cs`, `GameUIRootController.cs`, `RewardUIPanel.cs` | Functional, but visibility ownership is split. |
| Dialogue / Choice | `Story/Runtime/Core/DialogueRunner.cs`, `Story/Runtime/StoryEventRunner.cs` | `Dialogue/*`, `NonCombat/Dialogue/*`, `TimedChoicePanel.cs` | Production owner exists; older narrative paths remain. |
| Quest / Mission progress | `QuestRuntime.cs`, `QuestObjectiveTracker.cs`, `QuestCompletionFlow.cs` | `MissionManager.cs`, `QuestManager.cs`, `DemoMissionRuntime.cs` | Production wrappers exist, but active content still uses demo/mission managers. |
| Reward | `RewardService.cs`, `RewardResult.cs` | `RewardApplier.cs`, `CombatRewardUIBinder.cs`, random/search reward paths | Production owner exists; grant paths not fully centralized. |
| Inventory / Currency | `InventoryService.cs`, `CurrencyWallet.cs` | `ItemDefinitionSO.cs`, `CurrencyHUD.cs` | Owner exists under `NonCombat/Inventory`; final folder missing. |
| Save / Load | `SaveLoadService.cs` | `SaveManager.cs`, `SaveData.cs`, `SaveSerializer.cs` | Wrapper exists; final schema/versioning missing. |

## 5. Mixed Responsibility Files

| File | Mixed Responsibilities | Refactor Direction |
|---|---|---|
| `Assets/GAME/Scripts/Combat/FieldEnemy.cs` | Field enemy behavior, damage, combat start, legacy battle event fallback, demo mission defeat progress. | First refactor target. Split into field enemy runtime, combat encounter trigger/group, and quest objective event bridge. |
| `Assets/GAME/Scripts/Combat/Runtime/UI/CombatRewardUIBinder.cs` | Combat-end event handling, reward grant, demo mission progress, state transition, reward UI. | Keep short-term; later move demo progress out and let RewardService/GameFlow own grants/state. |
| `Assets/GAME/Scripts/Cutscene/Runtime/MissionCompleteCutsceneController.cs` | Mission completion subscription, video playback, input skip, reward panel activation, game state transitions. | Future CutsceneService/DaySettlementFlow boundary needed. |
| `Assets/GAME/Scripts/DemoMission/Runtime/Ending/DemoRescueNpcEndFlow.cs` | Rescue flow, UI sequence, mission completion, ending flow. | Promote behavior into Quest/Mission flow plus Cutscene/Settlement later. |
| `Assets/GAME/Scripts/Search/Runtime/SearchableInteractable2D.cs` | Proximity detection, fallback input, prompt UI, decision UI, search result execution. | Split after input/UI owners are stable. |
| `Assets/GAME/Scripts/Search/Runtime/SearchResultRunner.cs` | Search outcome execution and reward proposal handling. | Later integrate with `RewardService` and exploration resources. |
| `Assets/GAME/Scripts/Story/Runtime/StoryEventRunner.cs` | Story event flow, dialogue UI, choice effects, story progress marking, state transitions. | Acceptable for now; future split into runner, effect executor, choice requirement evaluator. |
| `Assets/GAME/Scripts/Dialogue/TimedChoiceDialoguePanel.cs` | UI, timer, time scale pause, state transition, choice resolution. | Legacy/merge candidate under final narrative UI and input route. |
| `Assets/GAME/Scripts/NonCombat/Save/SaveManager.cs` | Save/load runtime and debug F5/F6 input. | Keep behind `SaveLoadService`; move hotkeys to Debugging later. |
| `Assets/GAME/Scripts/Office/OfficeMenuController.cs` | Office interaction/menu UI and content navigation. | Future UI/Shop/Supply hub boundary. |

## 6. Duplicate Entry Points

### Combat Start Routes

- `CombatEntryPoint.StartCombatFromField(...)` is called by:
  - `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatEncounterTrigger2D.cs`
  - `Assets/GAME/Scripts/Player/Runtime/PlayerFieldAttackController.cs`
  - `Assets/GAME/Scripts/Combat/FieldEnemy.cs`
  - debug tooling such as `CombatFieldCallDebug.cs`
- Legacy/event route still exists:
  - `Assets/GAME/Scripts/Combat/FieldEnemy.cs` publishes `OnBattleRequested` fallback.
  - `Assets/GAME/Scripts/Legacy/Battle/BattleTrigger2D.cs`
  - `Assets/GAME/Scripts/Legacy/Battle/SeamlessBattleManager.cs`
  - `Assets/GAME/Scripts/UI/BattleTransitionController.cs`
- Refactor decision: standardize production on `CombatEncounterTrigger2D`/`CombatEncounterGroup` + `CombatEntryPoint`; keep legacy scene refs untouched until Inspector validation.

### Input Handling Routes

- Production route: `GameInputInstaller -> InputService -> InputRouter`.
- Preferred player consumer: `Player/Runtime/PlayerInputController.cs`.
- Old/parallel routes:
  - `Input/OverworldInputAdapter.cs`
  - `Player/Overworld/OverworldInputBridge.cs`
  - `Player/PlayerInputUnity.cs`
  - `Player/IPlayerInput.cs`
  - direct `UnityEngine.Input` calls in cutscene, search, timed choice, save debug, story debug, combat debug.
- Refactor decision: do not delete old input routes until the active player prefab and all scene references are confirmed.

### UI Show / Hide Routes

- Production route: `UIScreenRouter -> GameUIRootController`.
- Direct show/hide still exists in:
  - Combat UI: `CombatUIRootController`, `CombatPlanningHUD`, `CombatDemoFlowController`, `CombatRewardUIBinder`
  - Story UI: `DialoguePanel`, `DialogueUIPanel`, `ChoiceUIPanel`, `TimedChoicePanel`, `WorldDialogueBubble`, `StoryDialogueHUD`
  - Search UI: `SearchDecisionHUD`, `SearchResultHUD`, `ItemAcquisitionHUD`
  - Demo/UI: `RewardUIPanel`, `DemoEndController`, `ContractDocumentPanel`, DemoMission panels, Office UI
- Refactor decision: route root-level visibility first; leave panel-local show/hide until its parent root is under router control.

### Mission / Quest Completion Routes

- Production quest wrappers:
  - `QuestRuntime.cs`
  - `QuestObjectiveTracker.cs`
  - `QuestCompletionFlow.cs`
- Other completion paths:
  - `MissionManager.cs`
  - `QuestManager.cs`
  - `DemoMissionRuntime.cs`
  - `MissionCompletionController.cs`
  - `DungeonObjectiveManager.cs`
  - `RescueNpcObjectiveEventSO.cs`
  - `StoryEffect.cs` can complete mission/objective
  - `TutorialQuestCombatBridge.cs`
- Refactor decision: migrate DemoMission/Mission content into production quest runtime gradually; do not rename SO classes yet.

### Reward Granting Routes

- Production owner: `RewardService.cs`.
- Other grant/proposal paths:
  - `RewardApplier.cs`
  - `CombatRewardUIBinder.cs`
  - `RewardInteractionEventSO.cs`
  - `RandomLootInteractionEventSO.cs`
  - `SearchRewardManager.cs`
  - `SearchResultRunner.cs`
  - `SearchEffect.cs`
  - `ItemAcquisitionHUD.cs`
- Refactor decision: introduce `RewardRequest` later and convert one source at a time.

## 7. Legacy / Debug / Demo Candidates

### Keep Legacy Temporarily

- `Assets/GAME/Scripts/Legacy/Battle/BattleTransitionRequest.cs`
- `Assets/GAME/Scripts/Legacy/Battle/BattleTrigger2D.cs`
- `Assets/GAME/Scripts/Legacy/Battle/SeamlessBattleManager.cs`
- `Assets/GAME/Scripts/Legacy/StoryDeprecated/StoryFlagManagerDeprecated.cs`
- `Assets/GAME/Scripts/UI/BattleTransitionController.cs`
- `Assets/GAME/Scripts/Input/OverworldInputAdapter.cs`
- `Assets/GAME/Scripts/Player/Overworld/OverworldInputBridge.cs`
- `Assets/GAME/Scripts/Player/PlayerInputUnity.cs`
- `Assets/GAME/Scripts/Player/PlayerController2D.cs`
- `Assets/GAME/Scripts/Player/PlayerMotor2D.cs`
- `Assets/GAME/Scripts/Player/OverworldPlayerController.cs`

### Keep Debugging Temporarily

- `Assets/GAME/Scripts/Debugging/Combat/CombatFieldCallDebug.cs`
- `Assets/GAME/Scripts/Debugging/Combat/CombatSkillDebugInvoker.cs`
- `Assets/GAME/Scripts/Debugging/Combat/CombatStartSmokeTest.cs`
- `Assets/GAME/Scripts/Debugging/Combat/CombatTestRunner.cs`
- `Assets/GAME/Scripts/Debugging/Combat/CombatAutoPlanner.cs`
- `Assets/GAME/Scripts/Debugging/Combat/InspirationDebugHotkey.cs`
- `Assets/GAME/Scripts/Debugging/StoryInteractionDebugHotkey.cs`
- `Assets/GAME/Scripts/Debugging/VerticalSliceSceneValidator.cs`

### Demo / Vertical-Slice Candidates

- `Assets/GAME/Scripts/Demo/**`
- `Assets/GAME/Scripts/DemoMission/**`
- `Assets/GAME/Scripts/UI/DemoEndController.cs`
- `Assets/GAME/Scripts/UI/DemoEndInteractionEventSO.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatDemoFlowController.cs`
- `Assets/GAME/Scripts/Tutorial/**`

Audit stance: these are not delete candidates. They are source material for production quest/content pipeline migration, and some are scene/asset referenced.

## 8. Missing Final Systems

| Required Final System | Current Status | Closest Existing Files | Audit Recommendation |
|---|---|---|---|
| `DailyFlowController` | Missing | `GameFlowController.cs` | Create later after quest/reward flow ownership is stable. |
| `CalendarService` | Missing | none | Create with Daily system, not during cleanup. |
| `DaySettlementFlow` | Missing | `GameFlowController.cs`, `RewardUIPanel.cs`, `MissionCompleteCutsceneController.cs` | Create after combat reward and mission-complete flow are centralized. |
| `ExplorationResourceRuntime` | Missing | `SearchRewardProposal.cs`, `SearchEffect.cs`, `GameProgressState.cs` | Create after search/exploration rewards are mapped to resources. |
| `ExplorationConditionService` | Missing | `StoryCondition.cs`, `NonCombatChoiceCondition.cs`, `SearchOutcome.cs` | Create with condition unification. |
| `SocialStatService` | Missing | `PersonaStatusManager.cs`, `PersonaStat.cs` | Promote Persona into final Social service later. |
| `ChoiceRequirementEvaluator` | Missing | `ChoiceCondition.cs`, `StoryCondition.cs`, `NonCombatChoiceCondition.cs` | Create when narrative conditions are consolidated. |
| `ShopService` | Missing | none | Future system; do not stub until shop UX/data is defined. |
| `SupplyLoadoutService` | Missing | inventory/currency files only | Future system; should integrate with Inventory and PreMission flow. |
| `PreMissionSupplyFlow` | Missing | `OfficeMenuController.cs`, case file UI, mission data | Future office/pre-mission loop owner. |

Additional final gaps:

- Party/Bond runtime is missing.
- Audio settings/service is missing.
- General Cutscene/Timeline service is missing.
- Localization service/data is missing.
- Versioned save schema, save slots, stable world IDs, and migrations are missing.

## 9. Scene / Prefab Reference Risks

Do not move, rename, or delete these without Unity Inspector checks and `.meta` preservation:

- `CombatEntryPoint.cs`, `CombatDirector.cs`, `CombatStateSyncer.cs`, `CombatFormationManager.cs`, combat HUDs, and encounter components are scene-referenced.
- `FieldEnemy.cs` is scene-referenced in `Test.unity`.
- Debug combat tools are referenced by `Test.unity` and/or `Assets/GAME/Combat/Tests/Scenes/CombatTest.unity`.
- DemoMission runtime/data files are referenced by `Dungeon 1.unity` and tutorial data assets.
- `BattleTransitionController.cs` is scene-referenced in `Test.unity` and `InGame.unity`.
- Old player movement/input files are scene-referenced in `Demo.unity` and/or `Test.unity`.
- `RewardItemUI.cs` is prefab-referenced by `Assets/GAME/Prefabs/RewardItem.prefab` and `Assets/GAME/Prefabs/UI/Combat/RewarItemUI.prefab`.
- `RewardUIPanel.cs`, `GameUIRootController.cs`, and `UIScreenRouter.cs` are scene-referenced.
- Story event/data SO scripts are referenced by `Assets/GAME/Story/**` assets.
- Search data/runtime/UI scripts are referenced by `Test.unity` and `Assets/GAME/Search/**` assets.

Reference-risk rule for the next refactor: behavior changes can happen inside a file when requested, but structural changes to script path, class name, namespace, or serialized fields must be avoided unless the task explicitly includes Unity reference migration.

## 10. Recommended Refactor Order

1. Compile/Unity validation first: run Unity editor or batchmode compile and record real compile status.
2. Combat start consolidation: make `FieldEnemy` stop being an owner of multiple systems by extracting decisions into existing `CombatEncounterTrigger2D`/`CombatEncounterGroup` and quest objective bridge behavior. Do not move/rename files in this step.
3. Reward route consolidation: convert search/random/interaction reward sources to call `RewardService` through a small request model, one source at a time.
4. Quest/Mission/DemoMission consolidation: map DemoMission runtime progress to `QuestRuntime`/`QuestObjectiveTracker` while preserving existing SO class names.
5. Input route cleanup: confirm active player prefab route, then disable or wrap old direct input paths without removing scripts.
6. UI root consolidation: wire `UIScreenRouter` ownership over major roots, then reduce duplicate root `SetActive` calls.
7. Narrative condition consolidation: add `ChoiceRequirementEvaluator` and route Story/NonCombat choice conditions through it.
8. Save/load schema upgrade: add versioned save data, slots, migrations, and stable IDs.
9. Add final-loop systems: Daily, Calendar, DaySettlement, ExplorationResource, SocialStat, Shop/Supply, PreMissionSupply.
10. Only after all reference risks are cleared, move/archive legacy/debug/demo scripts.

First refactor recommendation: `FieldEnemy` and combat-start ownership. It has the highest cross-system risk and blocks clean Quest, Reward, and Legacy cleanup.

## 11. Do-Not-Touch List

For the next refactor task, do not move, rename, delete, or change serialized fields on:

- Any `ScriptableObject` class with existing `.asset` references: combat skill data, story event data, quest/demo/mission data, search data, interaction data, title/demo end data.
- Any scene-referenced runtime owner: core bootstrap/state/flow, input installer, combat entry/director/state syncer, UI router/root, reward service, old player controllers, DemoMission runtime, search runtime/UI.
- Legacy battle files and `BattleTransitionController.cs` until scene references are validated.
- Debugging combat files while `Test.unity` and combat test scenes still reference them.
- Generated Input System file `Assets/GAME/Scripts/Input/inputactions.cs`; regenerate through Unity if casing/name changes are needed.
- `PlayerMotor2D_New.cs`; do not rename until the old `PlayerMotor2D.cs` scene references are removed.

Allowed safe next-step style: internal behavior changes in one subsystem with no path/class/namespace/serialized-field changes and a focused Unity compile/test pass.

## 12. Next Codex Task Suggestion

Recommended next task:

```text
Refactor combat start ownership without moving or renaming files.

Goal:
- Keep CombatEntryPoint as the only production combat entry.
- Preserve scene/prefab references.
- Do not delete Legacy or DemoMission files.
- Reduce FieldEnemy mixed responsibility by routing combat start through the existing encounter path or a thin internal adapter.
- Do not change serialized field names unless strictly necessary.

Required output:
- Code changes limited to combat/world integration files.
- A short report listing behavior preserved, files changed, and Unity compile status.
```
