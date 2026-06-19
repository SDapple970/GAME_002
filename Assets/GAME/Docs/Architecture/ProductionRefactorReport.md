# Production Refactor Report

Date: 2026-06-20
Unity target: 6000.2.6f2

## Safety Check

Git status before this pass:

`	ext
D Assets/.tmp.driveupload/* (18 pre-existing deleted temp upload files)
`

Git status after this pass:

`	ext
D Assets/.tmp.driveupload/4595
 D Assets/.tmp.driveupload/5352
 D Assets/.tmp.driveupload/5354
 D Assets/.tmp.driveupload/5423
 D Assets/.tmp.driveupload/5586
 D Assets/.tmp.driveupload/5588
 D Assets/.tmp.driveupload/5618
 D Assets/.tmp.driveupload/5620
 D Assets/.tmp.driveupload/5622
 D Assets/.tmp.driveupload/5624
 D Assets/.tmp.driveupload/5626
 D Assets/.tmp.driveupload/5628
 D Assets/.tmp.driveupload/5630
 D Assets/.tmp.driveupload/5632
 D Assets/.tmp.driveupload/5642
 D Assets/.tmp.driveupload/5644
 D Assets/.tmp.driveupload/5646
 D Assets/.tmp.driveupload/5648
 D Assets/GAME/Scripts/Battle.meta
 D Assets/GAME/Scripts/Battle/BattleTransitionRequest.cs
 D Assets/GAME/Scripts/Battle/BattleTransitionRequest.cs.meta
 D Assets/GAME/Scripts/Battle/BattleTrigger2D.cs
 D Assets/GAME/Scripts/Battle/BattleTrigger2D.cs.meta
 D Assets/GAME/Scripts/Battle/SeamlessBattleManager.cs
 D Assets/GAME/Scripts/Battle/SeamlessBattleManager.cs.meta
 D Assets/GAME/Scripts/Combat/CombatSkillDebugInvoker.cs
 D Assets/GAME/Scripts/Combat/CombatSkillDebugInvoker.cs.meta
 D Assets/GAME/Scripts/Combat/Debugging.meta
 D Assets/GAME/Scripts/Combat/Debugging/CombatAutoPlanner.cs
 D Assets/GAME/Scripts/Combat/Debugging/CombatAutoPlanner.cs.meta
 D Assets/GAME/Scripts/Combat/Debugging/CombatFieldCallDebug.cs
 D Assets/GAME/Scripts/Combat/Debugging/CombatFieldCallDebug.cs.meta
 D Assets/GAME/Scripts/Combat/Debugging/CombatStartSmokeTest.cs
 D Assets/GAME/Scripts/Combat/Debugging/CombatStartSmokeTest.cs.meta
 D Assets/GAME/Scripts/Combat/Debugging/CombatTestRunner.cs
 D Assets/GAME/Scripts/Combat/Debugging/CombatTestRunner.cs.meta
 D Assets/GAME/Scripts/Combat/Debugging/InspirationDebugHotkey.cs
 D Assets/GAME/Scripts/Combat/Debugging/InspirationDebugHotkey.cs.meta
 M Assets/GAME/Scripts/Combat/Runtime/Integration/CombatDemoFlowController.cs
 M Assets/GAME/Scripts/Combat/Runtime/Integration/CombatStateSyncer.cs
 M Assets/GAME/Scripts/Combat/Runtime/UI/CombatRewardUIBinder.cs
 M Assets/GAME/Scripts/Core/GameState.cs
 M Assets/GAME/Scripts/Core/GameStateMachine.cs
 M Assets/GAME/Scripts/DemoMission/Runtime/MissionCompletionController.cs
 M Assets/GAME/Scripts/DemoMission/Runtime/RescueNpcActor.cs
 M Assets/GAME/Scripts/Dialogue/TimedChoiceDialogueEventSO.cs
 M Assets/GAME/Scripts/Dialogue/TimedChoiceDialoguePanel.cs
 M Assets/GAME/Scripts/Input/GameInputInstaller.cs
 M Assets/GAME/Scripts/NonCombat/Dialogue/DialogueController.cs
 M Assets/GAME/Scripts/NonCombat/Reward/RewardApplier.cs
 M Assets/GAME/Scripts/Player/Runtime/PlayerFieldAttackController.cs
 M Assets/GAME/Scripts/Player/Runtime/PlayerInputController.cs
 D Assets/GAME/Scripts/Story/Deprecated.meta
 D Assets/GAME/Scripts/Story/Deprecated/StoryFlagManagerDeprecated.cs
 D Assets/GAME/Scripts/Story/Deprecated/StoryFlagManagerDeprecated.cs.meta
 M Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionController.cs
 D Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionDebugHotkey.cs
 D Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionDebugHotkey.cs.meta
 M Assets/GAME/Scripts/Story/Runtime/StoryEventRunner.cs
 M Assets/GAME/Scripts/UI/BattleTransitionController.cs
 M Assets/GAME/Scripts/UI/RewardUIPanel.cs
?? Assets/GAME/Scripts/Core/GameFlowController.cs
?? Assets/GAME/Scripts/Core/RuntimeBootstrapper.cs
?? Assets/GAME/Scripts/Core/SaveLoadService.cs
?? Assets/GAME/Scripts/Core/SceneFlowController.cs
?? Assets/GAME/Scripts/Debugging/Combat.meta
?? Assets/GAME/Scripts/Debugging/Combat/
?? Assets/GAME/Scripts/Debugging/README.md
?? Assets/GAME/Scripts/Debugging/StoryInteractionDebugHotkey.cs
?? Assets/GAME/Scripts/Debugging/StoryInteractionDebugHotkey.cs.meta
?? Assets/GAME/Scripts/Input/InputRouter.cs
?? Assets/GAME/Scripts/Input/InputService.cs
?? Assets/GAME/Scripts/Legacy/
?? Assets/GAME/Scripts/Quest/QuestCompletionFlow.cs
?? Assets/GAME/Scripts/Quest/QuestObjectiveTracker.cs
?? Assets/GAME/Scripts/Quest/QuestRuntime.cs
?? Assets/GAME/Scripts/Reward/
?? Assets/GAME/Scripts/UI/GameUIRootController.cs
?? Assets/GAME/Scripts/UI/UIScreenRouter.cs
`

Reference scan method:
- Listed every .cs file under Assets/GAME/Scripts.
- Parsed class/namespace/kind from source text.
- Checked .meta GUIDs against .unity, .prefab, and .asset files under Assets/GAME.
- Treated any scene/prefab/asset GUID hit as a serialized-reference risk.

## Files Modified

- Assets/GAME/Scripts/Core/GameState.cs: added production GameState values and kept Combat as a compatibility alias.
- Assets/GAME/Scripts/Core/GameStateMachine.cs: added shared state permission helpers.
- Assets/GAME/Scripts/Input/GameInputInstaller.cs: routed generated Input System events through InputService and InputRouter.
- Assets/GAME/Scripts/Player/Runtime/PlayerInputController.cs: uses GameStateMachine exploration-input helper.
- Assets/GAME/Scripts/Player/Runtime/PlayerFieldAttackController.cs: uses GameStateMachine exploration-input helper.
- Assets/GAME/Scripts/Combat/Runtime/Integration/CombatStateSyncer.cs: sets CombatPlanning on combat start.
- Assets/GAME/Scripts/Combat/Runtime/Integration/CombatDemoFlowController.cs: sets CombatPlanning instead of old Combat alias.
- Assets/GAME/Scripts/Combat/Runtime/UI/CombatRewardUIBinder.cs: grants rewards through RewardService and routes reward state through GameFlowController when present.
- Assets/GAME/Scripts/UI/BattleTransitionController.cs: legacy path now enters CombatPlanning.
- Assets/GAME/Scripts/UI/RewardUIPanel.cs: no longer grants rewards on close; displays only and emits close intent.
- Assets/GAME/Scripts/NonCombat/Reward/RewardApplier.cs: compatibility wrapper delegates to RewardService.
- Assets/GAME/Scripts/Story/Runtime/StoryEventRunner.cs: no longer polls physical input for advance; subscribes to routed Interact.
- Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionController.cs: routed Interact is primary, legacy key only when installer is absent.
- Assets/GAME/Scripts/DemoMission/Runtime/RescueNpcActor.cs: no longer polls physical interact key; subscribes to routed Interact.
- Assets/GAME/Scripts/DemoMission/Runtime/MissionCompletionController.cs: waits on any combat state, not only legacy Combat.
- Assets/GAME/Scripts/NonCombat/Dialogue/DialogueController.cs: checks any combat state.
- Assets/GAME/Scripts/Dialogue/TimedChoiceDialoguePanel.cs: checks any combat state.
- Assets/GAME/Scripts/Dialogue/TimedChoiceDialogueEventSO.cs: checks any combat state.

## Files Created

- Assets/GAME/Scripts/Core/GameFlowController.cs
- Assets/GAME/Scripts/Core/RuntimeBootstrapper.cs
- Assets/GAME/Scripts/Core/SceneFlowController.cs
- Assets/GAME/Scripts/Core/SaveLoadService.cs
- Assets/GAME/Scripts/Input/InputService.cs
- Assets/GAME/Scripts/Input/InputRouter.cs
- Assets/GAME/Scripts/UI/GameUIRootController.cs
- Assets/GAME/Scripts/UI/UIScreenRouter.cs
- Assets/GAME/Scripts/Reward/RewardService.cs
- Assets/GAME/Scripts/Reward/RewardResult.cs
- Assets/GAME/Scripts/Quest/QuestRuntime.cs
- Assets/GAME/Scripts/Quest/QuestObjectiveTracker.cs
- Assets/GAME/Scripts/Quest/QuestCompletionFlow.cs
- Assets/GAME/Scripts/Legacy/README.md
- Assets/GAME/Scripts/Debugging/README.md
- Assets/GAME/Docs/Architecture/ProductionRefactorReport.md
- Assets/GAME/Docs/Architecture/SystemOwnershipMap.md
- Assets/GAME/Docs/Architecture/RefactorRoadmap.md
- Assets/GAME/Docs/Architecture/FileMinimizationRules.md

## Files Moved To Debugging

- Assets/GAME/Scripts/Combat/Debugging/* -> Assets/GAME/Scripts/Debugging/Combat/*
- Assets/GAME/Scripts/Combat/CombatSkillDebugInvoker.cs -> Assets/GAME/Scripts/Debugging/Combat/CombatSkillDebugInvoker.cs
- Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionDebugHotkey.cs -> Assets/GAME/Scripts/Debugging/StoryInteractionDebugHotkey.cs

Risk: low to medium. .meta files were moved with scripts, so scene GUID references should remain valid. Namespaces were intentionally not changed.

## Files Moved To Legacy

- Assets/GAME/Scripts/Battle/* -> Assets/GAME/Scripts/Legacy/Battle/*
- Assets/GAME/Scripts/Story/Deprecated/* -> Assets/GAME/Scripts/Legacy/StoryDeprecated/*

Risk: low for compile, medium for scene organization. Namespaces remain Game.Battle and previous story namespace for compatibility.

## Files Renamed, Merged, Archived, Deleted

- Renamed: none.
- Merged: none. No tiny wrapper was merged because most candidates have scene/prefab reference risk.
- Archived: none.
- Deleted: none. Only moves were performed; pre-existing .tmp.driveupload deleted files were not touched.

## Combat Entry Consolidation

Official owner is now documented as CombatEntryPoint. Existing production paths already call StartCombatFromField:

- CombatEncounterTrigger2D -> CombatEntryPoint.StartCombatFromField.
- PlayerFieldAttackController -> CombatEntryPoint.StartCombatFromField.
- FieldEnemy -> CombatEntryPoint.StartCombatFromField.

The old BattleTrigger2D / SeamlessBattleManager path was moved to Legacy. BattleTransitionController remains as a compatibility scene component because it has scene references.

Remaining risk: FieldEnemy still mixes field AI, combat start, and demo mission progress. It should become a thin world component or be replaced by EncounterTrigger2D + EncounterGroup + quest objective events.

## Input Consolidation

Added the official route:

Unity Input System -> GameInputInstaller -> InputService -> InputRouter -> existing adapters/controllers

Current behavior:
- Move/Jump/Attack/Parry are emitted only when GameState allows Exploration input.
- Interact is routed through the installer and used by story interaction, story runner, and RescueNpcActor compatibility.
- Pause is blocked during Loading.

Remaining direct physical input is documented as either Debugging, Legacy fallback, or a future migration item: cutscene skip, search item confirmation, timed choice hotkeys, office mouse click fallback, old PlayerInputUnity, SaveManager F5/F6 debug hotkeys.

## UI Visibility Consolidation

Added GameUIRootController and UIScreenRouter as the official state-driven visibility route:

GameStateMachine -> UIScreenRouter -> GameUIRootController -> panel roots

Existing panels still own local show/hide behavior where scene references are already wired. Manual scene migration is required to attach UIScreenRouter and assign root references.

## DemoMission / Quest

Full ScriptableObject class renames were not performed because DemoMissionDefinitionSO, RescueNpcDefinitionSO, and DemoMission UI/runtime components have asset and scene risk. Added production Quest wrappers:

- QuestRuntime
- QuestObjectiveTracker
- QuestCompletionFlow

DemoMission remains a compatibility system for this pass. Manual migration should map DemoMissionDefinitionSO assets to production MissionDefinitionSO or a future QuestDefinitionSO, then replace DemoMissionRuntime references in FieldEnemy, CombatRewardUIBinder, and RescueNpcActor.

## Reward / Inventory

Reward granting now flows through RewardService. RewardUIPanel displays data and emits close intent only. RewardApplier remains as a compatibility wrapper.

Remaining risk: search rewards, random loot, and choice outcome rewards still touch inventory/currency paths directly in places. These should become RewardRequest -> RewardService later.

## Save / Load

Added SaveLoadService as official wrapper over existing SaveManager and enforced allowed save states. Existing SaveData still uses serializable data; however, it stores Vector3 playerPosition and lacks scene/spawn IDs, object stable IDs, migration/version fields, quest runtime data, narrative data, and progression data.

## Static Validation

- dotnet build Assembly-CSharp.csproj --no-restore was attempted. It exited nonzero but reported 0 warnings and 0 C# errors; this appears to be Unity-generated project/tooling behavior outside the editor, not a script error report.
- Unity executable was not available on PATH, so Unity batchmode compile validation could not be run here.
- Duplicate class-name scan found only namespace-separated duplicates: TitleSceneController (Game.Demo, GAME.Title) and StoryConditionType (Game.Story.Core, Game.Story.Data).
- Runtime references to Game.Debugging and Game.Legacy namespaces were not detected. Legacy namespace names were intentionally preserved inside moved files.

## Manual Unity Inspector Checks

- SceneRoot: confirm one RuntimeBootstrapper or equivalent bootstrap root per boot scene.
- InputRoot: confirm one GameInputInstaller and no duplicate PlayerInput action sources on the same player.
- UIRoot: add/assign UIScreenRouter and GameUIRootController roots for Field, Dialogue, Choice, Combat, Reward, Pause, Loading.
- CombatRoot: confirm CombatEntryPoint, CombatFlowOrchestrator, CombatDirector, CombatStateSyncer, and RewardService are assigned or discoverable.
- WorldRoot: verify player uses the intended PlayerInputController/PlayerMotor2D_New path, not both old and new controller stacks.
- EncounterGroup: verify CombatEncounterGroup child collection and enemy active states.
- FieldEnemy: verify demo mission flags and CombatEntryPoint reference; plan migration to EncounterTrigger2D/EncounterGroup.
- FieldCombatantAdapter dependencies: verify CombatHpComponent, CombatSkillLoadoutComponent, CombatKeywordComponent on player/enemies.
- Dialogue NPC references: verify StoryInteractable2D points to StoryEventRunner or DialogueRunner and no NPC directly enables dialogue panels.
- Quest/mission references: verify DemoMission assets still load, then plan migration to MissionDefinitionSO/QuestRuntime.
- RewardUIPanel: verify close button still fires OnClosed and no reward is expected from the panel itself.
- UIScreenRouter panel references: verify no panel root is assigned twice or hidden by another root controller.
- GameStateMachine event subscriptions: enter play mode and check no duplicate OnStateChanged subscriptions after scene reload.
- EventSystem: verify exactly one EventSystem per scene.
- Missing serialized fields: open moved Legacy/Debug components and confirm Unity reports no missing scripts after meta-preserving moves.

## Full Script Audit

| File | Class | Namespace | Kind | Layer | Reference status | Overlap | Category |
|---|---|---|---|---|---|---|---|
| Assets\GAME\Scripts\Camera\CameraFollow2D.cs | CameraFollow2D | Game.CameraSys | MonoBehaviour | Other | asset/scene refs: 3 | - | Production Keep |
| Assets\GAME\Scripts\Camera\CombatCameraController.cs | CombatCameraController | Game.Combat.Integration | MonoBehaviour | Other | asset/scene refs: 3 | - | Production Keep |
| Assets\GAME\Scripts\Combat\CombatLogHUD.cs | CombatLogHUD | Game.Combat.UI | MonoBehaviour | Other | asset/scene refs: 2 | - | Production Keep |
| Assets\GAME\Scripts\Combat\Data\OpeningEffectSO.cs | OpeningEffectSO | Game.Combat.Adapters | ScriptableObject | Other | asset/scene refs: 1 | - | Production Keep |
| Assets\GAME\Scripts\Combat\Data\SkillDefinitionSO.cs | SkillDefinitionSO | Game.Combat.Data | ScriptableObject | Other | asset/scene refs: 7 | - | Production Keep |
| Assets\GAME\Scripts\Combat\Data\SkillMovementMode.cs | SkillMovementMode | Game.Combat.Data | Utility | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\FieldEnemy.cs | FieldEnemy | Game.Battle | MonoBehaviour | Other | asset/scene refs: 1 | combat entry overlap | Refactor Required |
| Assets\GAME\Scripts\Combat\Runtime\Actions\SkillRunner.cs | SkillRunner | Game.Combat.Actions | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Actions\SoSkill.cs | SoSkill | Game.Combat.Actions | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Adapters\CombatHpComponent.cs | CombatHpComponent | Game.Combat.Adapters | MonoBehaviour | Other | asset/scene refs: 3 | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Adapters\CombatKeywordComponent.cs | CombatKeywordComponent | Game.Combat.Adapters | MonoBehaviour | Other | asset/scene refs: 3 | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Adapters\CombatSkillLoadoutComponent.cs | CombatSkillLoadoutComponent | Game.Combat.Adapters | MonoBehaviour | Other | asset/scene refs: 3 | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Adapters\CombatStartRequest.cs | CombatStartRequest | Game.Combat.Adapters | Pure Model | Other | code only / meta only | quest/mission overlap | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Adapters\DummyCombatant.cs | DummyCombatant | Game.Combat.Adapters | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Adapters\DummyCombatantFactory.cs | DummyCombatantFactory | Game.Combat.Adapters | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Adapters\FieldCombatantAdapter.cs | FieldCombatantAdapter | Game.Combat.Adapters | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Adapters\FieldCombatantFactory.cs | FieldCombatantFactory | Game.Combat.Adapters | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Adapters\HpAccessor.cs | HpAccessor | Game.Combat.Adapters | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Adapters\ICombatantFactory.cs | ICombatantFactory | Game.Combat.Adapters | Interface | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Adapters\OpeningEffectApplier.cs | OpeningEffectApplier | Game.Combat.Adapters | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Animation\CombatantAnimationDriver.cs | CombatantAnimationDriver | Game.Combat.Animation | MonoBehaviour | Other | asset/scene refs: 1 | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Core\CombatBootstrapper.cs | CombatBootstrapper | Game.Combat.Core | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Core\CombatEndEvaluator.cs | CombatEndEvaluator | Game.Combat.Core | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Core\CombatEntryPoint.cs | CombatEntryPoint | Game.Combat.Core | MonoBehaviour | Other | asset/scene refs: 4 | - | Refactor Required |
| Assets\GAME\Scripts\Combat\Runtime\Core\CombatFlowOrchestrator.cs | CombatFlowOrchestrator | Game.Combat.Core | MonoBehaviour | Other | asset/scene refs: 1 | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Core\CombatPlanValidator.cs | CombatPlanValidator | Game.Combat.Core | Pure Model | Other | code only / meta only | - | Move To Debugging |
| Assets\GAME\Scripts\Combat\Runtime\Core\CombatResultBuilder.cs | CombatResultBuilder | Game.Combat.Core | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Core\CombatStateMachine.cs | CombatStateMachine | Game.Combat.Core | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Core\CombatTimeline.cs | CombatTimeline | Game.Combat.Core | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Core\CombatTurnResolver.cs | CombatTurnResolver | Game.Combat.Core | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Core\InspirationPool.cs | InspirationPool | Game.Combat.Core | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Core\KnowledgeBook.cs | KnowledgeBook | Game.Combat.Core | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Core\SkillBook.cs | SkillBook | Game.Combat.Core | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Core\StaggerSystem.cs | StaggerSystem | Game.Combat.Core | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Effects\CombatDirector.cs | CombatDirector | Game.Combat.Effects | MonoBehaviour | Other | asset/scene refs: 4 | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Environment\CombatEnvironment.cs | CombatEnvironment | Game.Combat.Environment | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Integration\CombatDemoFlowController.cs | CombatDemoFlowController | Game.Combat.Integration | MonoBehaviour | Other | code only / meta only | - | Refactor Required |
| Assets\GAME\Scripts\Combat\Runtime\Integration\CombatEncounterGroup.cs | CombatEncounterGroup | Game.Combat.Integration | MonoBehaviour | Other | code only / meta only | combat entry overlap | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Integration\CombatEncounterTrigger2D.cs | CombatEncounterTrigger2D | Game.Combat.Integration | MonoBehaviour | Other | asset/scene refs: 2 | combat entry overlap | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Integration\CombatFieldLock.cs | CombatFieldLock | Game.Combat.Integration | MonoBehaviour | Other | asset/scene refs: 1 | - | Refactor Required |
| Assets\GAME\Scripts\Combat\Runtime\Integration\CombatFormationManager.cs | CombatFormationManager | Game.Combat.Integration | MonoBehaviour | Other | asset/scene refs: 4 | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Integration\CombatStateSyncer.cs | CombatStateSyncer | Game.Integration | MonoBehaviour | Other | asset/scene refs: 4 | - | Refactor Required |
| Assets\GAME\Scripts\Combat\Runtime\Integration\EncounterAdvantageApplier.cs | EncounterAdvantageApplier | Game.Combat.Integration | MonoBehaviour | Other | asset/scene refs: 3 | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Model\ActionPlan.cs | ActionPlan | Game.Combat.Model | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Model\CombatEndReason.cs | CombatEndReason | Game.Combat.Model | Utility | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Model\CombatEnums.cs | Side | Game.Combat.Model | Utility | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Model\CombatIds.cs | CombatantId | Game.Combat.Model | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Model\CombatPlanDraft.cs | CombatPlanDraft | Game.Combat.Model | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Model\CombatPlaybook.cs | PlaybookEvent | Game.Combat.Data | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Model\CombatResult.cs | CombatResult | Game.Combat.Model | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Model\CombatSession.cs | CombatSession | Game.Combat.Model | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Model\CombatTurn.cs | CombatTurn | Game.Combat.Model | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Model\ICombatant.cs | ICombatant | Game.Combat.Model | Interface | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Model\ISkill.cs | ISkill | Game.Combat.Model | Interface | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\Model\KeywordMask.cs | KeywordMask | Game.Combat.Model | Utility | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\UI\CombatantWidget.cs | CombatantWidget | Game.Combat.UI | MonoBehaviour | Other | asset/scene refs: 1 | - | Refactor Required |
| Assets\GAME\Scripts\Combat\Runtime\UI\CombatInspirationHUD.cs | CombatInspirationHUD | Game.Combat.UI | MonoBehaviour | Other | asset/scene refs: 2 | - | Production Keep |
| Assets\GAME\Scripts\Combat\Runtime\UI\CombatPlanningHUD.cs | CombatPlanningHUD | Game.Combat.UI | MonoBehaviour | Other | asset/scene refs: 3 | - | Refactor Required |
| Assets\GAME\Scripts\Combat\Runtime\UI\CombatRewardUIBinder.cs | CombatRewardUIBinder | Game.Combat.UI | MonoBehaviour | Other | asset/scene refs: 1 | reward/inventory ownership | Refactor Required |
| Assets\GAME\Scripts\Combat\Runtime\UI\CombatUIRootController.cs | CombatUIRootController | Game.Combat.UI | MonoBehaviour | Other | asset/scene refs: 2 | - | Refactor Required |
| Assets\GAME\Scripts\Combat\Runtime\UI\CombatWidgetManager.cs | CombatWidgetManager | Game.Combat.UI | MonoBehaviour | Other | asset/scene refs: 3 | - | Production Keep |
| Assets\GAME\Scripts\Common\Damage\IDamageable.cs | IDamageable | Game.Common | Interface | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Common\Damage\SimpleDamageable.cs | SimpleDamageable | Game.Common | MonoBehaviour | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Core\GameFlowController.cs | GameFlowController | Game.Core | MonoBehaviour | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Core\GameState.cs | GameState | Game.Core | Utility | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Core\GameStateMachine.cs | GameStateMachine | Game.Core | MonoBehaviour | Other | asset/scene refs: 4 | - | Refactor Required |
| Assets\GAME\Scripts\Core\RuntimeBootstrapper.cs | RuntimeBootstrapper | Game.Core | MonoBehaviour | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Core\SaveLoadService.cs | SaveLoadService | Game.Core | MonoBehaviour | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Core\SceneFlowController.cs | SceneFlowController | Game.Core | MonoBehaviour | Other | code only / meta only | - | Refactor Required |
| Assets\GAME\Scripts\Cutscene\Runtime\CutscenePlaybackRequest.cs | CutscenePlaybackRequest | Game.Cutscene | Pure Model | Other | code only / meta only | quest/mission overlap | Production Keep |
| Assets\GAME\Scripts\Cutscene\Runtime\MissionCompleteCutsceneController.cs | MissionCompleteCutsceneController | Game.Cutscene | MonoBehaviour | Other | code only / meta only | quest/mission overlap | Refactor Required |
| Assets\GAME\Scripts\Debugging\Combat\CombatAutoPlanner.cs | CombatAutoPlanner | Game.Combat.Debugging | MonoBehaviour | Other | code only / meta only | - | Move To Debugging |
| Assets\GAME\Scripts\Debugging\Combat\CombatFieldCallDebug.cs | CombatFieldCallDebug | Game.Combat.Core | MonoBehaviour | Other | asset/scene refs: 1 | - | Move To Debugging |
| Assets\GAME\Scripts\Debugging\Combat\CombatSkillDebugInvoker.cs | CombatSkillDebugInvoker | Game.Combat.Debugging | MonoBehaviour | Other | asset/scene refs: 1 | - | Move To Debugging |
| Assets\GAME\Scripts\Debugging\Combat\CombatStartSmokeTest.cs | CombatStartSmokeTest | Game.Combat.Core | MonoBehaviour | Other | asset/scene refs: 1 | - | Move To Debugging |
| Assets\GAME\Scripts\Debugging\Combat\CombatTestRunner.cs | CombatTestRunner | Game.Combat.Core | MonoBehaviour | Other | asset/scene refs: 1 | - | Move To Debugging |
| Assets\GAME\Scripts\Debugging\Combat\InspirationDebugHotkey.cs | InspirationDebugHotkey | Game.Combat.Core | MonoBehaviour | Other | code only / meta only | - | Move To Debugging |
| Assets\GAME\Scripts\Debugging\StoryInteractionDebugHotkey.cs | StoryInteractionDebugHotkey | Game.Story.Interaction | MonoBehaviour | Other | code only / meta only | narrative/choice overlap | Move To Debugging |
| Assets\GAME\Scripts\Debugging\VerticalSliceSceneValidator.cs | VerticalSliceSceneValidator | Game.Debugging | MonoBehaviour | Other | asset/scene refs: 2 | - | Move To Debugging |
| Assets\GAME\Scripts\Demo\ContractDataSO.cs | ContractDataSO | Game.Demo | ScriptableObject | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Demo\ContractDocumentPanel.cs | ContractDocumentPanel | Game.Demo | MonoBehaviour | Other | code only / meta only | - | Refactor Required |
| Assets\GAME\Scripts\Demo\DungeonObjectiveManager.cs | DungeonObjectiveManager | Game.Demo | MonoBehaviour | Other | asset/scene refs: 1 | - | Production Keep |
| Assets\GAME\Scripts\Demo\ObjectiveEnemyMarker.cs | ObjectiveEnemyMarker | Game.Demo | MonoBehaviour | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Demo\RescueNpcObjectiveEventSO.cs | RescueNpcObjectiveEventSO | Game.Demo | Pure Model | Other | asset/scene refs: 2 | - | Production Keep |
| Assets\GAME\Scripts\Demo\TitleSceneController.cs | TitleSceneController | Game.Demo | MonoBehaviour | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\DemoMission\Data\DemoMissionDefinitionSO.cs | DemoMissionDefinitionSO | Game.DemoMission.Data | ScriptableObject | Other | asset/scene refs: 2 | quest/mission overlap | Production Keep |
| Assets\GAME\Scripts\DemoMission\Data\MonsterBriefingEntry.cs | MonsterBriefingEntry | Game.DemoMission.Data | Pure Model | Other | code only / meta only | quest/mission overlap | Production Keep |
| Assets\GAME\Scripts\DemoMission\Data\RescueNpcDefinitionSO.cs | RescueNpcDefinitionSO | Game.DemoMission.Data | ScriptableObject | Other | asset/scene refs: 1 | quest/mission overlap | Production Keep |
| Assets\GAME\Scripts\DemoMission\Runtime\DemoMissionRuntime.cs | DemoMissionRuntime | Game.DemoMission.Runtime | MonoBehaviour | Other | asset/scene refs: 1 | quest/mission overlap | Refactor Required |
| Assets\GAME\Scripts\DemoMission\Runtime\Ending\DemoEndPanelController.cs | DemoEndPanelController | Game.DemoMission | MonoBehaviour | Other | asset/scene refs: 1 | quest/mission overlap | Move To Legacy |
| Assets\GAME\Scripts\DemoMission\Runtime\Ending\DemoRescueNpcEndFlow.cs | DemoRescueNpcEndFlow | Game.DemoMission | MonoBehaviour | Other | asset/scene refs: 1 | quest/mission overlap | Refactor Required |
| Assets\GAME\Scripts\DemoMission\Runtime\MissionCompletionController.cs | MissionCompletionController | Game.DemoMission.Runtime | MonoBehaviour | Other | asset/scene refs: 1 | quest/mission overlap | Refactor Required |
| Assets\GAME\Scripts\DemoMission\Runtime\MissionObjectiveTracker.cs | MissionObjectiveTracker | Game.DemoMission.Runtime | MonoBehaviour | Other | asset/scene refs: 1 | quest/mission overlap | Refactor Required |
| Assets\GAME\Scripts\DemoMission\Runtime\RescueNpcActor.cs | RescueNpcActor | Game.DemoMission.Runtime | MonoBehaviour | Other | asset/scene refs: 1 | quest/mission overlap | Refactor Required |
| Assets\GAME\Scripts\DemoMission\UI\CaseFileAcceptController.cs | CaseFileAcceptController | Game.DemoMission.UI | MonoBehaviour | Other | code only / meta only | quest/mission overlap | Refactor Required |
| Assets\GAME\Scripts\DemoMission\UI\CaseFilePanel.cs | CaseFilePanel | Game.DemoMission.UI | MonoBehaviour | Other | code only / meta only | quest/mission overlap | Refactor Required |
| Assets\GAME\Scripts\DemoMission\UI\MissionCompletePanel.cs | MissionCompletePanel | Game.DemoMission.UI | MonoBehaviour | Other | code only / meta only | quest/mission overlap | Refactor Required |
| Assets\GAME\Scripts\Dialogue\TimedChoiceDialogueEventSO.cs | TimedChoiceDialogueEventSO | Game.Dialogue | Pure Model | Other | asset/scene refs: 1 | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Dialogue\TimedChoiceDialoguePanel.cs | TimedChoiceDialoguePanel | Game.Dialogue | MonoBehaviour | Other | asset/scene refs: 1 | narrative/choice overlap | Refactor Required |
| Assets\GAME\Scripts\Dialogue\TimedChoiceOption.cs | TimedChoiceOption | Game.Dialogue | Pure Model | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Enemies\Runtime\FieldEnemyAnimator2D.cs | FieldEnemyAnimator2D | Game.Enemies | MonoBehaviour | Other | asset/scene refs: 1 | combat entry overlap | Production Keep |
| Assets\GAME\Scripts\Enemies\Runtime\FieldEnemyMotor2D.cs | FieldEnemyMotor2D | Game.Enemies | MonoBehaviour | Other | asset/scene refs: 1 | combat entry overlap | Production Keep |
| Assets\GAME\Scripts\Enemies\Runtime\FieldEnemyPatrolAI2D.cs | FieldEnemyPatrolAI2D | Game.Enemies | MonoBehaviour | Other | asset/scene refs: 1 | combat entry overlap | Production Keep |
| Assets\GAME\Scripts\Enemy\Overworld\EnemyAnimator2D.cs | EnemyAnimator2D | Game.Enemy.Overworld | MonoBehaviour | Other | asset/scene refs: 1 | input path overlap | Production Keep |
| Assets\GAME\Scripts\Enemy\Overworld\OverworldEnemyAI.cs | OverworldEnemyAI | Game.Enemy.Overworld | MonoBehaviour | Other | asset/scene refs: 1 | input path overlap | Production Keep |
| Assets\GAME\Scripts\Input\GameInputInstaller.cs | GameInputInstaller | <global> | MonoBehaviour | Other | asset/scene refs: 4 | input path overlap | Production Keep |
| Assets\GAME\Scripts\Input\inputactions.cs | is | <global> | MonoBehaviour | Other | code only / meta only | input path overlap | Production Keep |
| Assets\GAME\Scripts\Input\InputDeviceWatcher.cs | InputDeviceWatcher | <global> | MonoBehaviour | Other | code only / meta only | input path overlap | Production Keep |
| Assets\GAME\Scripts\Input\InputRouter.cs | InputRouter | Game.Input | Pure Model | Other | code only / meta only | input path overlap | Production Keep |
| Assets\GAME\Scripts\Input\InputService.cs | InputService | Game.Input | Pure Model | Other | code only / meta only | input path overlap | Production Keep |
| Assets\GAME\Scripts\Input\OverworldInputAdapter.cs | OverworldInputAdapter | <global> | MonoBehaviour | Other | code only / meta only | input path overlap | Refactor Required |
| Assets\GAME\Scripts\Input\RebindSaveLoad.cs | RebindSaveLoad | <global> | MonoBehaviour | Other | code only / meta only | input path overlap | Production Keep |
| Assets\GAME\Scripts\Interaction\DialogueInteractionEventSO.cs | DialogueInteractionEventSO | Game.Interaction | Pure Model | Other | asset/scene refs: 2 | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Interaction\IInteractable.cs | IInteractable | Game.Interaction | Interface | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Interaction\InteractableObject.cs | InteractableObject | Game.Interaction | MonoBehaviour | Other | asset/scene refs: 2 | - | Refactor Required |
| Assets\GAME\Scripts\Interaction\InteractionController.cs | InteractionController | Game.Interaction | MonoBehaviour | Other | asset/scene refs: 2 | - | Production Keep |
| Assets\GAME\Scripts\Interaction\InteractionEventSO.cs | InteractionContext | Game.Interaction | ScriptableObject | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Interaction\InteractionPromptUI.cs | InteractionPromptUI | Game.Interaction | MonoBehaviour | Other | asset/scene refs: 2 | - | Refactor Required |
| Assets\GAME\Scripts\Interaction\ObjectStateChangeEventSO.cs | ObjectStateChangeEventSO | Game.Interaction | Pure Model | Other | code only / meta only | - | Refactor Required |
| Assets\GAME\Scripts\Interaction\RandomLootEntry.cs | RandomLootEntry | Game.Interaction | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Interaction\RandomLootInteractionEventSO.cs | RandomLootInteractionEventSO | Game.Interaction | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Interaction\RewardInteractionEventSO.cs | RewardInteractionEventSO | Game.Interaction | Pure Model | Other | code only / meta only | reward/inventory ownership | Production Keep |
| Assets\GAME\Scripts\Legacy\Battle\BattleTransitionRequest.cs | EncounterAdvantage | Game.Battle | Pure Model | Other | code only / meta only | combat entry overlap | Move To Legacy |
| Assets\GAME\Scripts\Legacy\Battle\BattleTrigger2D.cs | BattleTrigger2D | Game.Battle | MonoBehaviour | Other | code only / meta only | combat entry overlap | Move To Legacy |
| Assets\GAME\Scripts\Legacy\Battle\SeamlessBattleManager.cs | SeamlessBattleManager | Game.Battle | MonoBehaviour | Other | code only / meta only | combat entry overlap | Move To Legacy |
| Assets\GAME\Scripts\Legacy\StoryDeprecated\StoryFlagManagerDeprecated.cs | StoryFlagManagerDeprecated | Game.Story | MonoBehaviour | Other | code only / meta only | narrative/choice overlap | Move To Legacy |
| Assets\GAME\Scripts\Mission\Runtime\Data\MissionDefinitionSO.cs | MissionDefinitionSO | Game.Mission.Data | ScriptableObject | Other | asset/scene refs: 2 | quest/mission overlap | Production Keep |
| Assets\GAME\Scripts\Mission\Runtime\Data\MissionObjective.cs | MissionObjective | Game.Mission.Data | Pure Model | Other | code only / meta only | quest/mission overlap | Production Keep |
| Assets\GAME\Scripts\Mission\Runtime\MissionManager.cs | MissionManager | Game.Mission | MonoBehaviour | Other | code only / meta only | quest/mission overlap | Production Keep |
| Assets\GAME\Scripts\Mission\Runtime\UI\MissionHUD.cs | MissionHUD | Game.Mission.UI | MonoBehaviour | Other | code only / meta only | quest/mission overlap | Refactor Required |
| Assets\GAME\Scripts\NonCombat\Chapter\ChapterDefinitionSO.cs | ChapterDefinitionSO | Game.NonCombat.Chapter | ScriptableObject | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\NonCombat\Chapter\NonCombatChapterProgressManager.cs | NonCombatChapterProgressManager | Game.NonCombat.Chapter | MonoBehaviour | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\NonCombat\Choice\ChoiceOutcome.cs | ChoiceOutcomeType | Game.NonCombat.Choice | Pure Model | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\NonCombat\Choice\ChoiceRunner.cs | ChoiceRunner | Game.NonCombat.Choice | MonoBehaviour | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\NonCombat\Choice\NonCombatChoiceCondition.cs | ChoiceConditionType | Game.NonCombat.Choice | Pure Model | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\NonCombat\Dialogue\DialogueChoice.cs | DialogueChoice | Game.NonCombat.Dialogue | Pure Model | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\NonCombat\Dialogue\DialogueController.cs | DialogueController | Game.NonCombat.Dialogue | MonoBehaviour | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\NonCombat\Dialogue\DialogueNodeSO.cs | DialogueNodeSO | Game.NonCombat.Dialogue | ScriptableObject | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\NonCombat\Dialogue\NonCombatDialogueUIPanel.cs | NonCombatDialogueUIPanel | Game.NonCombat.Dialogue | MonoBehaviour | Other | code only / meta only | narrative/choice overlap | Refactor Required |
| Assets\GAME\Scripts\NonCombat\Interaction\INonCombatInteractable.cs | INonCombatInteractable | Game.NonCombat.Interaction | Interface | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\NonCombat\Interaction\Interactable2D.cs | Interactable2D | Game.NonCombat.Interaction | MonoBehaviour | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\NonCombat\Interaction\InteractionDetector2D.cs | InteractionDetector2D | Game.NonCombat.Interaction | MonoBehaviour | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\NonCombat\Inventory\CurrencyWallet.cs | CurrencyWallet | Game.NonCombat.Inventory | MonoBehaviour | Other | code only / meta only | reward/inventory ownership | Production Keep |
| Assets\GAME\Scripts\NonCombat\Inventory\InventoryService.cs | InventoryService | Game.NonCombat.Inventory | MonoBehaviour | Other | code only / meta only | reward/inventory ownership | Production Keep |
| Assets\GAME\Scripts\NonCombat\Inventory\ItemDefinitionSO.cs | ItemDefinitionSO | Game.NonCombat.Inventory | ScriptableObject | Other | code only / meta only | reward/inventory ownership | Production Keep |
| Assets\GAME\Scripts\NonCombat\Progress\GameProgressState.cs | GameProgressState | Game.NonCombat.Progress | MonoBehaviour | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\NonCombat\Progress\StoryFlagDatabase.cs | StoryFlagDatabase | Game.NonCombat.Progress | MonoBehaviour | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\NonCombat\Reward\RewardApplier.cs | RewardApplier | Game.NonCombat.Reward | MonoBehaviour | Other | code only / meta only | reward/inventory ownership | Refactor Required |
| Assets\GAME\Scripts\NonCombat\Save\SaveData.cs | SaveData | Game.NonCombat.Save | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\NonCombat\Save\SaveManager.cs | SaveManager | Game.NonCombat.Save | MonoBehaviour | Other | code only / meta only | - | Refactor Required |
| Assets\GAME\Scripts\NonCombat\Save\SaveSerializer.cs | SaveSerializer | Game.NonCombat.Save | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Office\CaseFileDocumentPanel.cs | CaseFileDocumentPanel | Game.Office | MonoBehaviour | Other | code only / meta only | - | Refactor Required |
| Assets\GAME\Scripts\Office\OfficeHotspot2D.cs | OfficeHotspotType | Game.Office | MonoBehaviour | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Office\OfficeMenuController.cs | OfficeMenuController | Game.Office | MonoBehaviour | Other | code only / meta only | - | Refactor Required |
| Assets\GAME\Scripts\Player\FieldSkillCaster.cs | FieldSkillCaster | Game.Player | MonoBehaviour | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Player\IPlayerInput.cs | IPlayerInput | Game.Player | Interface | Other | code only / meta only | input path overlap | Production Keep |
| Assets\GAME\Scripts\Player\Overworld\OverworldAttack2D.cs | OverworldAttack2D | <global> | MonoBehaviour | Other | asset/scene refs: 2 | input path overlap | Production Keep |
| Assets\GAME\Scripts\Player\Overworld\OverworldInputBridge.cs | OverworldInputBridge | <global> | MonoBehaviour | Other | code only / meta only | input path overlap | Production Keep |
| Assets\GAME\Scripts\Player\Overworld\OverworldPlayerDriver.cs | OverworldPlayerDriver | <global> | MonoBehaviour | Other | code only / meta only | input path overlap | Production Keep |
| Assets\GAME\Scripts\Player\Overworld\OverworldPlayerGlue.cs | OverworldPlayerGlue | GAME.Player.Overworld | MonoBehaviour | Other | code only / meta only | input path overlap | Production Keep |
| Assets\GAME\Scripts\Player\OverworldPlayerController.cs | OverworldPlayerController | Game.Player | MonoBehaviour | Other | asset/scene refs: 2 | input path overlap | Production Keep |
| Assets\GAME\Scripts\Player\PlayerAnimator2D.cs | PlayerAnimator2D | Game.Player | MonoBehaviour | Other | asset/scene refs: 1 | - | Production Keep |
| Assets\GAME\Scripts\Player\PlayerController2D.cs | PlayerController2D | Game.Player | MonoBehaviour | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Player\PlayerInputUnity.cs | PlayerInputUnity | Game.Player | MonoBehaviour | Other | code only / meta only | input path overlap | Refactor Required |
| Assets\GAME\Scripts\Player\PlayerMotor2D.cs | PlayerMotor2D | <global> | MonoBehaviour | Other | asset/scene refs: 2 | - | Production Keep |
| Assets\GAME\Scripts\Player\Runtime\PlayerAnimationController.cs | PlayerAnimationController | Game.Player | MonoBehaviour | Other | asset/scene refs: 1 | - | Production Keep |
| Assets\GAME\Scripts\Player\Runtime\PlayerFieldAttackController.cs | PlayerFieldAttackController | Game.Player | MonoBehaviour | Other | asset/scene refs: 1 | - | Production Keep |
| Assets\GAME\Scripts\Player\Runtime\PlayerInputController.cs | PlayerInputController | Game.Player | MonoBehaviour | Other | asset/scene refs: 1 | input path overlap | Production Keep |
| Assets\GAME\Scripts\Player\Runtime\PlayerMotor2D_New.cs | PlayerMotor2D_New | Game.Player | MonoBehaviour | Other | asset/scene refs: 1 | - | Production Keep |
| Assets\GAME\Scripts\Quest\QuestAdvanceInteractionEventSO.cs | QuestAdvanceInteractionEventSO | Game.Quest | Pure Model | Other | asset/scene refs: 1 | quest/mission overlap | Production Keep |
| Assets\GAME\Scripts\Quest\QuestCompleteInteractionEventSO.cs | QuestCompleteInteractionEventSO | Game.Quest | Pure Model | Other | code only / meta only | quest/mission overlap | Production Keep |
| Assets\GAME\Scripts\Quest\QuestCompletionFlow.cs | QuestCompletionFlow | Game.Quest | MonoBehaviour | Other | code only / meta only | quest/mission overlap | Production Keep |
| Assets\GAME\Scripts\Quest\QuestDataSO.cs | QuestDataSO | Game.Quest | ScriptableObject | Other | code only / meta only | quest/mission overlap | Production Keep |
| Assets\GAME\Scripts\Quest\QuestId.cs | QuestId | Game.Quest | Utility | Other | code only / meta only | quest/mission overlap | Production Keep |
| Assets\GAME\Scripts\Quest\QuestManager.cs | QuestManager | Game.Quest | MonoBehaviour | Other | asset/scene refs: 2 | quest/mission overlap | Production Keep |
| Assets\GAME\Scripts\Quest\QuestObjectiveTracker.cs | QuestObjectiveTracker | Game.Quest | MonoBehaviour | Other | code only / meta only | quest/mission overlap | Production Keep |
| Assets\GAME\Scripts\Quest\QuestProgress.cs | QuestProgress | Game.Quest | Pure Model | Other | code only / meta only | quest/mission overlap | Production Keep |
| Assets\GAME\Scripts\Quest\QuestRuntime.cs | QuestRuntime | Game.Quest | MonoBehaviour | Other | code only / meta only | quest/mission overlap | Production Keep |
| Assets\GAME\Scripts\Quest\QuestStartInteractionEventSO.cs | QuestStartInteractionEventSO | Game.Quest | Pure Model | Other | code only / meta only | quest/mission overlap | Production Keep |
| Assets\GAME\Scripts\Quest\QuestStepData.cs | QuestStepData | Game.Quest | Pure Model | Other | code only / meta only | quest/mission overlap | Production Keep |
| Assets\GAME\Scripts\Quest\QuestTrackerUI.cs | QuestTrackerUI | Game.Quest | MonoBehaviour | Other | asset/scene refs: 2 | quest/mission overlap | Refactor Required |
| Assets\GAME\Scripts\Reward\RewardResult.cs | RewardResult | Game.Reward | Pure Model | Other | code only / meta only | reward/inventory ownership | Production Keep |
| Assets\GAME\Scripts\Reward\RewardService.cs | RewardService | Game.Reward | MonoBehaviour | Other | code only / meta only | reward/inventory ownership | Production Keep |
| Assets\GAME\Scripts\Search\Runtime\Data\SearchableObjectDefinitionSO.cs | SearchObjectCategory | Game.Search.Data | ScriptableObject | Other | asset/scene refs: 6 | - | Production Keep |
| Assets\GAME\Scripts\Search\Runtime\Data\SearchEffect.cs | SearchEffectType | Game.Search.Data | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Search\Runtime\Data\SearchOutcome.cs | SearchOutcome | Game.Search.Data | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Search\Runtime\SearchableInteractable2D.cs | SearchableInteractable2D | Game.Search | MonoBehaviour | Other | asset/scene refs: 1 | - | Refactor Required |
| Assets\GAME\Scripts\Search\Runtime\SearchObjectAnchor.cs | SearchObjectAnchor | Game.Search | MonoBehaviour | Other | asset/scene refs: 1 | - | Production Keep |
| Assets\GAME\Scripts\Search\Runtime\SearchObjectVisualState2D.cs | SearchObjectVisualState2D | Game.Search | MonoBehaviour | Other | asset/scene refs: 1 | - | Refactor Required |
| Assets\GAME\Scripts\Search\Runtime\SearchResultRunner.cs | SearchResultRunner | Game.Search | MonoBehaviour | Other | asset/scene refs: 1 | - | Production Keep |
| Assets\GAME\Scripts\Search\Runtime\SearchRewardManager.cs | SearchRewardManager | Game.Search | MonoBehaviour | Other | asset/scene refs: 1 | reward/inventory ownership | Production Keep |
| Assets\GAME\Scripts\Search\Runtime\SearchRewardProposal.cs | SearchRewardKind | Game.Search | Pure Model | Other | code only / meta only | reward/inventory ownership | Production Keep |
| Assets\GAME\Scripts\Search\Runtime\UI\ItemAcquisitionHUD.cs | ItemAcquisitionHUD | Game.Search.UI | MonoBehaviour | Other | asset/scene refs: 1 | - | Refactor Required |
| Assets\GAME\Scripts\Search\Runtime\UI\SearchDecisionHUD.cs | SearchDecisionHUD | Game.Search.UI | MonoBehaviour | Other | asset/scene refs: 1 | - | Refactor Required |
| Assets\GAME\Scripts\Search\Runtime\UI\SearchResultHUD.cs | SearchResultHUD | Game.Search.UI | MonoBehaviour | Other | asset/scene refs: 1 | - | Refactor Required |
| Assets\GAME\Scripts\Story\CaseBoard.cs | CaseBoard | Game.Story | MonoBehaviour | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\CaseFileDataSO.cs | CaseFileDataSO | Game.Story | ScriptableObject | Other | asset/scene refs: 1 | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\ChapterId.cs | ChapterId | Game.Story | Utility | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\ChapterProgress.cs | ChapterProgress | Game.Story | Pure Model | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\ChapterProgressManager.cs | ChapterProgressManager | Game.Story | MonoBehaviour | Other | asset/scene refs: 2 | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\ChapterStartInteractionEventSO.cs | ChapterStartInteractionEventSO | Game.Story | Pure Model | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\LocalTeleportInteractionEventSO.cs | LocalTeleportInteractionEventSO | Game.Story | Pure Model | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\Runtime\Core\DialogueRunner.cs | DialogueRunner | Game.Story.Core | MonoBehaviour | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\Runtime\Core\StoryFlagCondition.cs | StoryConditionType | Game.Story.Core | Pure Model | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\Runtime\Core\StoryFlagManager.cs | StoryFlagManager | Game.Story.Core | MonoBehaviour | Other | asset/scene refs: 2 | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\Runtime\Data\ChoiceCondition.cs | ChoiceCondition | Game.Story.Data | Pure Model | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\Runtime\Data\ChoiceDefinition.cs | ChoiceDefinition | Game.Story.Data | Pure Model | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\Runtime\Data\ChoiceResult.cs | ChoiceResultType | Game.Story.Data | Pure Model | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\Runtime\Data\DialogueDefinitionSO.cs | DialogueDefinitionSO | Game.Story.Data | ScriptableObject | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\Runtime\Data\DialogueLine.cs | DialogueLine | Game.Story.Data | Pure Model | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\Runtime\Data\StoryChoice.cs | StoryChoice | Game.Story.Data | Pure Model | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\Runtime\Data\StoryCondition.cs | StoryConditionType | Game.Story.Data | Pure Model | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\Runtime\Data\StoryEffect.cs | StoryEffectType | Game.Story.Data | Pure Model | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\Runtime\Data\StoryEventDefinitionSO.cs | StoryEventDefinitionSO | Game.Story.Data | ScriptableObject | Other | asset/scene refs: 11 | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\Runtime\Data\StoryNode.cs | StoryNode | Game.Story.Data | Pure Model | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\Runtime\Interaction\StoryInteractionAutoUIBootstrapper.cs | StoryInteractionAutoUIBootstrapper | Game.Story.Interaction | Pure Model | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\Runtime\Interaction\StoryInteractionConfirmUI.cs | StoryInteractionConfirmUI | Game.Story.Interaction | MonoBehaviour | Other | code only / meta only | narrative/choice overlap | Refactor Required |
| Assets\GAME\Scripts\Story\Runtime\Interaction\StoryInteractionController.cs | StoryInteractionController | Game.Story.Interaction | MonoBehaviour | Other | asset/scene refs: 3 | narrative/choice overlap | Refactor Required |
| Assets\GAME\Scripts\Story\Runtime\Interaction\StoryInteractionKind.cs | StoryInteractionKind | Game.Story.Interaction | Utility | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\Runtime\Interaction\StoryInteractionPromptUI.cs | StoryInteractionPromptUI | Game.Story.Interaction | MonoBehaviour | Other | asset/scene refs: 2 | narrative/choice overlap | Refactor Required |
| Assets\GAME\Scripts\Story\Runtime\StoryEventRunner.cs | StoryEventRunner | Game.Story | MonoBehaviour | Other | asset/scene refs: 3 | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\Runtime\StoryEventTrigger2D.cs | StoryEventTrigger2D | Game.Story | MonoBehaviour | Other | asset/scene refs: 1 | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\Runtime\StoryInteractable2D.cs | StoryInteractable2D | Game.Story | MonoBehaviour | Other | asset/scene refs: 2 | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\Runtime\StoryProgressManager.cs | StoryProgressManager | Game.Story | MonoBehaviour | Other | asset/scene refs: 2 | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\Runtime\StorySpeakerAnchor.cs | StorySpeakerAnchor | Game.Story | MonoBehaviour | Other | asset/scene refs: 2 | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\Runtime\UI\ChoiceButtonUI.cs | ChoiceButtonUI | Game.Story.UI | MonoBehaviour | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\Runtime\UI\ChoiceUIPanel.cs | ChoiceUIPanel | Game.Story.UI | MonoBehaviour | Other | code only / meta only | narrative/choice overlap | Refactor Required |
| Assets\GAME\Scripts\Story\Runtime\UI\DialoguePanel.cs | DialoguePanel | Game.Story.UI | MonoBehaviour | Other | asset/scene refs: 1 | narrative/choice overlap | Refactor Required |
| Assets\GAME\Scripts\Story\Runtime\UI\DialogueUIPanel.cs | DialogueUIPanel | Game.Story.UI | MonoBehaviour | Other | code only / meta only | narrative/choice overlap | Refactor Required |
| Assets\GAME\Scripts\Story\Runtime\UI\StoryDialogueHUD.cs | StoryDialogueHUD | Game.Story.UI | MonoBehaviour | Other | asset/scene refs: 1 | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\Runtime\UI\TimedChoicePanel.cs | TimedChoicePanel | Game.Story.UI | MonoBehaviour | Other | asset/scene refs: 1 | narrative/choice overlap | Refactor Required |
| Assets\GAME\Scripts\Story\Runtime\UI\WorldDialogueBubble.cs | WorldDialogueBubble | Game.Story.UI | MonoBehaviour | Other | asset/scene refs: 1 | narrative/choice overlap | Refactor Required |
| Assets\GAME\Scripts\Story\Runtime\World\StoryDialogueTrigger2D.cs | StoryDialogueTrigger2D | Game.Story.World | MonoBehaviour | Other | code only / meta only | narrative/choice overlap | Refactor Required |
| Assets\GAME\Scripts\Story\SceneSpawnPoint.cs | SceneSpawnPoint | Game.Story | MonoBehaviour | Other | asset/scene refs: 2 | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\SceneTravelInteractionEventSO.cs | SceneTravelInteractionEventSO | Game.Story | Pure Model | Other | code only / meta only | narrative/choice overlap | Production Keep |
| Assets\GAME\Scripts\Story\SceneTravelService.cs | SceneTravelService | Game.Story | MonoBehaviour | Other | asset/scene refs: 2 | narrative/choice overlap | Refactor Required |
| Assets\GAME\Scripts\Systems\Persona\PersonaStat.cs | PersonaStat | Game.Systems.Persona | Utility | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Systems\Persona\PersonaStatusManager.cs | PersonaStatusManager | Game.Systems.Persona | MonoBehaviour | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Title\Data\TitleMissionDefinitionSO.cs | TitleMissionDefinitionSO | GAME.Title | ScriptableObject | Other | code only / meta only | quest/mission overlap | Production Keep |
| Assets\GAME\Scripts\Title\Runtime\TitleSceneAnimator.cs | TitleSceneAnimator | GAME.Title | MonoBehaviour | Other | asset/scene refs: 1 | - | Production Keep |
| Assets\GAME\Scripts\Title\Runtime\TitleSceneController.cs | TitleSceneController | GAME.Title | MonoBehaviour | Other | asset/scene refs: 1 | - | Refactor Required |
| Assets\GAME\Scripts\Tutorial\TutorialBattleStartInteractionEventSO.cs | TutorialBattleStartInteractionEventSO | Game.Tutorial | Pure Model | Other | code only / meta only | combat entry overlap | Production Keep |
| Assets\GAME\Scripts\Tutorial\TutorialQuestCombatBridge.cs | TutorialQuestCombatBridge | Game.Tutorial | MonoBehaviour | Other | asset/scene refs: 2 | quest/mission overlap | Production Keep |
| Assets\GAME\Scripts\Tutorial\TutorialReturnToOfficeInteractionEventSO.cs | TutorialReturnToOfficeInteractionEventSO | Game.Tutorial | Pure Model | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\Tutorial\TutorialSceneInstaller.cs | TutorialSceneInstaller | Game.Tutorial | MonoBehaviour | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\UI\BattleTransitionController.cs | BattleTransitionController | Game.Battle | MonoBehaviour | Other | asset/scene refs: 2 | combat entry overlap | Refactor Required |
| Assets\GAME\Scripts\UI\DemoEndController.cs | DemoEndController | Game.UI | MonoBehaviour | Other | asset/scene refs: 1 | - | Move To Legacy |
| Assets\GAME\Scripts\UI\DemoEndInteractionEventSO.cs | DemoEndInteractionEventSO | Game.UI | Pure Model | Other | asset/scene refs: 1 | - | Move To Legacy |
| Assets\GAME\Scripts\UI\GameUIRootController.cs | GameUIRootController | Game.UI | MonoBehaviour | Other | code only / meta only | - | Refactor Required |
| Assets\GAME\Scripts\UI\RewardItemUI.cs | RewardItemUI | Game.UI | MonoBehaviour | Other | asset/scene refs: 2 | reward/inventory ownership | Production Keep |
| Assets\GAME\Scripts\UI\RewardUIPanel.cs | RewardUIPanel | Game.UI | MonoBehaviour | Other | asset/scene refs: 4 | reward/inventory ownership | Refactor Required |
| Assets\GAME\Scripts\UI\Runtime\BagButtonHUD.cs | BagButtonHUD | Game.UI | MonoBehaviour | Other | asset/scene refs: 1 | - | Refactor Required |
| Assets\GAME\Scripts\UI\Runtime\CombatUIDialogueBlocker.cs | CombatUIDialogueBlocker | Game.UI | MonoBehaviour | Other | code only / meta only | narrative/choice overlap | Refactor Required |
| Assets\GAME\Scripts\UI\Runtime\CurrencyHUD.cs | CurrencyHUD | Game.UI | MonoBehaviour | Other | asset/scene refs: 1 | reward/inventory ownership | Refactor Required |
| Assets\GAME\Scripts\UI\Runtime\DialogueLogHUD.cs | DialogueLogHUD | Game.UI | MonoBehaviour | Other | asset/scene refs: 1 | narrative/choice overlap | Refactor Required |
| Assets\GAME\Scripts\UI\Runtime\MapHUD.cs | MapHUD | Game.UI | MonoBehaviour | Other | asset/scene refs: 1 | - | Refactor Required |
| Assets\GAME\Scripts\UI\Runtime\MissionTrackerHUD.cs | MissionTrackerHUD | Game.UI | MonoBehaviour | Other | asset/scene refs: 1 | quest/mission overlap | Refactor Required |
| Assets\GAME\Scripts\UI\Runtime\OverworldHUDRoot.cs | OverworldHUDRoot | Game.UI | MonoBehaviour | Other | asset/scene refs: 1 | input path overlap | Production Keep |
| Assets\GAME\Scripts\UI\Runtime\StatusHUD.cs | StatusHUD | Game.UI | MonoBehaviour | Other | asset/scene refs: 1 | - | Refactor Required |
| Assets\GAME\Scripts\UI\ScreenFader.cs | ScreenFader | Game.UI | MonoBehaviour | Other | code only / meta only | - | Production Keep |
| Assets\GAME\Scripts\UI\UIScreenRouter.cs | UIScreenRouter | Game.UI | MonoBehaviour | Other | code only / meta only | - | Production Keep |