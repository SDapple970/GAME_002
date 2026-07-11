# Current Runtime Code Audit

Audit date: 2026-07-11. Scope: runtime C# under `Assets/GAME/Scripts`, with detailed inventory of the requested folders and supporting traces through `Story`, `Dialogue`, `Interaction`, `Mission`, `Tutorial`, `Legacy`, `Camera`, `Enemies`, and `NonCombat`. `World` and `Narrative` do not exist.

## Executive findings

- `Game.Core.GameStateMachine` is the global state service. It accepts unrestricted transitions; many feature scripts write it directly.
- Production combat creation converges on `Game.Combat.Core.CombatEntryPoint`. Debug tests bypass it and `Game.Legacy.Battle.SeamlessBattleManager` only changes state/UI.
- Combat calculation is separated from presentation: `CombatTurnResolver`, `SkillRunner`, `CombatEndEvaluator`, and `CombatResultBuilder` calculate; `CombatDirector`, animation drivers, cameras, formations, widgets, and HUDs present.
- `CombatEntryPoint.StartCombat` copies only the first non-null ally and first non-null enemy from a request (`AddFirstFieldObject`). Encounter groups may collect many enemies, but only one reaches the session.
- Two quest families coexist: `QuestManager`/`QuestDataSO` and `QuestRuntime`/`QuestDefinitionSO`; `MissionManager` is a third compatibility layer. DemoMission bridges into `QuestRuntime` but still owns progress and completion UI.
- Runtime, demo, and legacy UI visibility owners overlap. `UIScreenRouter`, `CombatUIRootController`, `CombatDemoFlowController`, individual panels, and DemoMission controllers all call `SetActive`.

## Inventory notation

Each row is `path | namespace | main type | class | script references / important dependencies | YAML usage | action`. “None found” means no project C# or GAME YAML reference was found; it is not proof of safe deletion. Scene usage is listed when found; otherwise `none identified`. Data/model rows are referenced by their containing subsystem unless stated otherwise.

## Core and Input inventory

| File / type | Namespace | Class | References and wiring | Action |
|---|---|---|---|---|
| `Assets/GAME/Scripts/Core/GameState.cs` / `GameState` | `Game.Core` | Runtime | Used across Core, Combat, Player, Story, Quest, UI, enemy AI; enum only | Keep |
| `Assets/GAME/Scripts/Core/GameStateMachine.cs` / `GameStateMachine` | `Game.Core` | Runtime | Central state API; in Demo, Dungeon 1, InGame, New_Dungeeon_Manual, Test, TitleScene | Modify: sole owner façade in batch 1 |
| `Assets/GAME/Scripts/Core/GameFlowController.cs` / `GameFlowController` | `Game.Core` | Runtime | Writes Exploration/Reward; used by combat reward binder; six scenes | Modify |
| `Assets/GAME/Scripts/Core/RuntimeBootstrapper.cs` / `RuntimeBootstrapper` | `Game.Core` | Runtime | Creates core/input/reward/UI services; six scenes including untracked New_Dungeeon_Manual | Keep, then modify carefully |
| `Assets/GAME/Scripts/Core/SaveLoadService.cs` / `SaveLoadService` | `Game.Core` | Runtime | Wraps save contracts; six scenes | Keep |
| `Assets/GAME/Scripts/Core/SceneFlowController.cs` / `SceneFlowController` | `Game.Core` | Runtime | Writes Loading/Exploration, scene-name string; six scenes | Keep |
| `Assets/GAME/Scripts/Input/GameInputInstaller.cs` / `GameInputInstaller` | global | Runtime | Owns generated `inputactions`, forwards into `InputService`; five scenes | Modify: canonical input owner |
| `Assets/GAME/Scripts/Input/inputactions.cs` / `inputactions` (generated wrapper, nested action types) | generated global | Runtime | Generated from Input Actions; used by installer | Keep generated; never hand-edit |
| `Assets/GAME/Scripts/Input/InputService.cs` / `InputService` | `Game.Input` | Runtime | Static event hub used by player/story | Keep, modify ownership |
| `Assets/GAME/Scripts/Input/InputRouter.cs` / `InputRouter` | `Game.Input` | Runtime | Reads GameState permissions; few/no direct consumers | Merge candidate with input ownership |
| `Assets/GAME/Scripts/Input/OverworldInputAdapter.cs` / `OverworldInputAdapter` | global | Legacy | Independently enables action references; used by old glue via Inspector only | Move to Legacy after scene validation |
| `Assets/GAME/Scripts/Input/InputDeviceWatcher.cs` / `InputDeviceWatcher` | global | Runtime | Input-device change utility; no GAME YAML found | Keep |
| `Assets/GAME/Scripts/Input/RebindSaveLoad.cs` / `RebindSaveLoad` | global | Runtime | Saves binding overrides; no GAME YAML found | Keep |

## Combat inventory

| Files / main types | Namespace | Class | References and wiring | Action |
|---|---|---|---|---|
| `Assets/GAME/Scripts/Combat/CombatLogHUD.cs` / `CombatLogHUD` | `Game.Combat.UI` | Runtime UI | Subscribes entry/state; Demo and Test | Keep |
| `Assets/GAME/Scripts/Combat/FieldEnemy.cs` / `FieldEnemy` | `Game.Battle` | Legacy | Starts via `CombatEntryPoint`; only Test; marked obsolete | Move to Legacy after reference migration |
| `Assets/GAME/Scripts/Combat/Data/OpeningEffectSO.cs` / `OpeningEffectSO` | `Game.Combat.Adapters` | Runtime data | Request/opening applier; SO asset exists | Keep |
| `Assets/GAME/Scripts/Combat/Data/SkillDefinitionSO.cs` / `SkillDefinitionSO` | `Game.Combat.Data` | Runtime data | Entry skill book/skills; multiple SO assets | Keep |
| `Assets/GAME/Scripts/Combat/Data/SkillMovementMode.cs` / `SkillMovementMode` | `Game.Combat.Data` | Runtime data | Skill/presentation movement mode | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Actions/SkillRunner.cs` / `SkillRunner` | `Game.Combat.Actions` | Runtime calculation | Called by resolver; mutates HP and emits events | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Actions/SoSkill.cs` / `SoSkill` | `Game.Combat.Actions` | Runtime adapter | Wraps `SkillDefinitionSO` as `ISkill` | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Adapters/CombatHpComponent.cs` / `CombatHpComponent` | `Game.Combat.Adapters` | Runtime | Field HP adapter; Demo, Dungeon 1, Test | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Adapters/CombatKeywordComponent.cs` / `CombatKeywordComponent` | same | Runtime | Field keyword adapter; Demo, Dungeon 1, Test | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Adapters/CombatSkillLoadoutComponent.cs` / `CombatSkillLoadoutComponent` | same | Runtime | Serialized skill loadout; Demo, Dungeon 1, Test | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Adapters/CombatStartRequest.cs` / `CombatStartRequest` | same | Runtime | Shared request into entry/bootstrapper | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Adapters/DummyCombatant.cs` / `DummyCombatant` | same | Debug | Used by debug factory/tests | Move to Debugging |
| `Assets/GAME/Scripts/Combat/Runtime/Adapters/DummyCombatantFactory.cs` / `DummyCombatantFactory` | same | Debug | Optional bootstrap factory/test path | Move to Debugging |
| `Assets/GAME/Scripts/Combat/Runtime/Adapters/FieldCombatantAdapter.cs` / `FieldCombatantAdapter` | same | Runtime | `ICombatant` over field object/HP accessor | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Adapters/FieldCombatantFactory.cs` / `FieldCombatantFactory` | same | Runtime | Entry uses it to adapt field objects | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Adapters/HpAccessor.cs` / `HpAccessor` | same | Runtime | Finds `CombatHpComponent`/`IDamageable` compatibility | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Adapters/ICombatantFactory.cs` / `ICombatantFactory` | same | Runtime | Bootstrap abstraction | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Adapters/OpeningEffectApplier.cs` / `OpeningEffectApplier` | same | Runtime calculation | Applies opening playbook/effects | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Animation/CombatantAnimationDriver.cs` / `CombatantAnimationDriver` | `Game.Combat.Animation` | Runtime presentation | Director animation bridge; Dungeon 1 | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Core/CombatBootstrapper.cs` / `CombatBootstrapper` | `Game.Combat.Core` | Runtime | Sole production `CombatSession` constructor; debug smoke bypass | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Core/CombatEndEvaluator.cs` / `CombatEndEvaluator` | same | Runtime calculation | Evaluates living sides | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Core/CombatEntryPoint.cs` / `CombatEntryPoint` | same | Runtime | Production entry/event owner; Demo, Dungeon 1, InGame, Test | Modify; preserve as single entry |
| `Assets/GAME/Scripts/Combat/Runtime/Core/CombatFlowOrchestrator.cs` / `CombatFlowOrchestrator` | same | Runtime | Validates draft, plans enemy, calls entry submit; Dungeon 1 | Keep/Modify |
| `Assets/GAME/Scripts/Combat/Runtime/Core/CombatPlanValidator.cs` / `CombatPlanValidator` | same | Runtime calculation | Orchestrator validation | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Core/CombatResultBuilder.cs` / `CombatResultBuilder` | same | Runtime calculation | Entry creates final result/reward totals | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Core/CombatStateMachine.cs` / `CombatStateMachine` | same | Runtime calculation/control | Entry ticks phases and director callback | Modify phase/state synchronization |
| `Assets/GAME/Scripts/Combat/Runtime/Core/CombatTimeline.cs` / `CombatTimeline` | same | Runtime calculation | Orders planned actions by speed/tie rules | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Core/CombatTurnResolver.cs` / `CombatTurnResolver` | same | Runtime calculation | Plans enemy actions, builds timeline, runs skills | Keep/Modify tests |
| `Assets/GAME/Scripts/Combat/Runtime/Core/InspirationPool.cs` / `InspirationPool` | same | Runtime model | Session resource; HUD/debug references | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Core/KnowledgeBook.cs` / `KnowledgeBook` | same | Runtime model | Inspection/knowledge events | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Core/SkillBook.cs` / `SkillBook` | same | Runtime model | Entry/factory skill registry | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Core/StaggerSystem.cs` / `StaggerSystem` | same | Runtime calculation | Resolver/playbook effects | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Effects/CombatDirector.cs` / `CombatDirector` | `Game.Combat.Effects` | Runtime presentation | Plays playbook/coroutines, reports completion; four scenes | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Environment/CombatEnvironment.cs` / `CombatEnvironment` | `Game.Combat.Environment` | Runtime model | Session environment | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatDemoFlowController.cs` / `CombatDemoFlowController` | `Game.Combat.Integration` | Demo | Competing camera/UI/lock/reward/state owner; no YAML found | Move to Legacy or Debugging after validation |
| `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatEncounterGroup.cs` / `CombatEncounterGroup` | same | Runtime | Trigger enemy grouping | Keep; fix multi-member handoff later |
| `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatEncounterTrigger2D.cs` / `CombatEncounterTrigger2D` | same | Runtime | Collision entry through request/entry; Dungeon 1, Test | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatFieldLock.cs` / `CombatFieldLock` | same | Runtime | Independently disables behaviours; Test | Merge candidate with state-based lock |
| `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatFormationManager.cs` / `CombatFormationManager` | same | Runtime presentation | Entry events; four scenes | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatStateSyncer.cs` / `CombatStateSyncer` | `Game.Integration` | Runtime | Duplicate start-state writer; four scenes | Merge candidate |
| `Assets/GAME/Scripts/Combat/Runtime/Integration/EncounterAdvantageApplier.cs` / `EncounterAdvantageApplier` | `Game.Combat.Integration` | Runtime presentation | Opening event subscriber; Demo/InGame/Test | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Model/ActionPlan.cs` / `PlannedAction`, `ActionPlan` | `Game.Combat.Model` | Runtime model | Planner/resolver | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Model/CombatEndReason.cs` / `CombatEndReason` | same | Runtime model | State/result | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Model/CombatEnums.cs` / `Side`, `Phase`, `StartReason`, etc. | same | Runtime model | Whole combat | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Model/CombatIds.cs` / combat ID structs | same | Runtime model | Whole combat | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Model/CombatPlanDraft.cs` / `CombatPlanDraft` | same | Runtime model | HUD/orchestrator | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Model/CombatPlaybook.cs` / `PlaybookEvent`, `CombatPlaybook` | `Game.Combat.Data` | Runtime model | Resolver/director | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Model/CombatResult.cs` / `CombatResult` | `Game.Combat.Model` | Runtime model | Entry, rewards, UI | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Model/CombatSession.cs` / `CombatSession` | same | Runtime model | Entry/state/planner/presentation | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Model/CombatTurn.cs` / `CombatTurn` | same | Runtime model | State/resolver/director | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Model/ICombatant.cs` / `ICombatant` | same | Runtime model | Factory/resolver/UI | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Model/ISkill.cs` / `ISkill` | same | Runtime model | Skill book/planning/resolution | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/Model/KeywordMask.cs` / `KeywordMask` | same | Runtime model | Combatant/effects | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/UI/CombatantWidget.cs` / `CombatantWidget` | `Game.Combat.UI` | Runtime UI | Widget prefab | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/UI/CombatInspirationHUD.cs` / `CombatInspirationHUD` | same | Runtime UI | Entry session; Demo/Test | Keep |
| `Assets/GAME/Scripts/Combat/Runtime/UI/CombatPlanningHUD.cs` / `CombatPlanningHUD` | same | Runtime UI | Builds skill/target buttons, submits draft; Demo/Dungeon 1/Test | Keep/Modify |
| `Assets/GAME/Scripts/Combat/Runtime/UI/CombatRewardUIBinder.cs` / `CombatRewardUIBinder` | same | Runtime UI/integration | Grants rewards and restores state; Dungeon 1 | Modify ownership |
| `Assets/GAME/Scripts/Combat/Runtime/UI/CombatUIRootController.cs` / `CombatUIRootController` | same | Runtime UI | Direct visibility owner; Demo/Test | Modify/merge candidate |
| `Assets/GAME/Scripts/Combat/Runtime/UI/CombatWidgetManager.cs` / `CombatWidgetManager` | same | Runtime UI | Entry events/widget prefab; Demo/InGame/Test | Keep |

## DemoMission, Quest, Reward, Debug, Player, and UI inventory

All paths below are exact. Compact group rows share the stated classification/action; serialized usage exceptions are called out.

| Files / types | Namespace | Class and dependencies | Action |
|---|---|---|---|
| `Assets/GAME/Scripts/DemoMission/Data/DemoMissionDefinitionSO.cs` / `DemoMissionDefinitionSO`; `MonsterBriefingEntry.cs` / `MonsterBriefingEntry`; `RescueNpcDefinitionSO.cs` / `RescueNpcDefinitionSO` | `Game.DemoMission.Data` | Demo data; runtime/UI and tutorial assets reference them | Promote compatible fields into Quest; keep assets during migration |
| `Assets/GAME/Scripts/DemoMission/Runtime/DemoMissionRuntime.cs` / `DemoMissionRuntime` | `Game.DemoMission.Runtime` | Demo; bridges to QuestRuntime; Dungeon 1 | Merge candidate into QuestRuntime |
| `Assets/GAME/Scripts/DemoMission/Runtime/MissionObjectiveTracker.cs` / `MissionObjectiveTracker` | same | Demo UI bridge; Dungeon 1 | Promote generic tracking to Quest UI |
| `Assets/GAME/Scripts/DemoMission/Runtime/MissionCompletionController.cs` / `MissionCompletionController` | same | Demo completion/input lock/UI owner; Dungeon 1 | Merge candidate with quest completion flow |
| `Assets/GAME/Scripts/DemoMission/Runtime/RescueNpcActor.cs` / `RescueNpcActor` | same | Demo world interaction; Dungeon 1 | Promote rescue event adapter to Quest |
| `Assets/GAME/Scripts/DemoMission/Runtime/Ending/DemoEndPanelController.cs` / `DemoEndPanelController`; `DemoRescueNpcEndFlow.cs` / `DemoRescueNpcEndFlow` | `Game.DemoMission` | Demo ending/local input/state; Dungeon 1 | Keep Demo/Legacy until replacement is wired |
| `Assets/GAME/Scripts/DemoMission/UI/CaseFileAcceptController.cs` / `CaseFileAcceptController`; `CaseFilePanel.cs` / `CaseFilePanel`; `MissionCompletePanel.cs` / `MissionCompletePanel` | `Game.DemoMission.UI` | Demo UI and scene transition; panel usage is indirect/Inspector | Keep Demo; promote reusable presentation later |
| `Assets/GAME/Scripts/Quest/QuestDataSO.cs` / `QuestDataSO`; `QuestId.cs` / `QuestId`; `QuestProgress.cs` / `QuestProgress`; `QuestStepData.cs` / `QuestStepData` | `Game.Quest` | Legacy/unclear first quest model used by QuestManager | Move to Legacy only after asset validation |
| `Assets/GAME/Scripts/Quest/QuestManager.cs` / `QuestManager` | `Game.Quest` | Legacy/unclear parallel runtime; Demo/Dungeon 1 | Merge candidate; preserve references |
| `Assets/GAME/Scripts/Quest/QuestDefinitionSO.cs` / `QuestDefinitionSO`; `QuestObjectiveDefinition.cs` / `QuestObjectiveDefinition`; `QuestEvent.cs` / event structs; `QuestEventType.cs` / `QuestEventType`; `QuestEventChannel.cs` / `QuestEventChannel` | `Game.Quest` | Runtime canonical candidate used by QuestRuntime/Demo bridge | Keep |
| `Assets/GAME/Scripts/Quest/QuestRuntime.cs` / `QuestRuntime`; `QuestObjectiveTracker.cs` / `QuestObjectiveTracker`; `QuestCompletionFlow.cs` / `QuestCompletionFlow` | `Game.Quest` | Runtime; New_Dungeeon_Manual; completion grants rewards/state | Keep/Modify |
| `Assets/GAME/Scripts/Quest/QuestStartInteractionEventSO.cs` / start; `QuestAdvanceInteractionEventSO.cs` / advance; `QuestCompleteInteractionEventSO.cs` / complete | `Game.Quest` | Runtime interaction adapters; SO assets exist | Keep |
| `Assets/GAME/Scripts/Quest/QuestTrackerUI.cs` / `QuestTrackerUI` | `Game.Quest` | Runtime UI for QuestManager; Demo/Dungeon 1 | Merge candidate with objective HUD |
| `Assets/GAME/Scripts/Reward/RewardGrantRequest.cs` / request structs; `RewardGrantResult.cs` / result structs; `RewardResult.cs` / compatibility result; `RewardSourceType.cs` / enum | `Game.Reward` | Runtime model; service/binders/quest flows | Keep |
| `Assets/GAME/Scripts/Reward/RewardService.cs` / `RewardService` | `Game.Reward` | Runtime singleton; six scenes; inventory/currency refs | Keep/Modify idempotency/save |
| `Assets/GAME/Scripts/Debugging/Combat/CombatAutoPlanner.cs` / `CombatAutoPlanner`; `CombatFieldCallDebug.cs` / `CombatFieldCallDebug`; `CombatSkillDebugInvoker.cs` / `CombatSkillDebugInvoker`; `CombatStartSmokeTest.cs` / `CombatStartSmokeTest`; `CombatTestRunner.cs` / `CombatTestRunner`; `InspirationDebugHotkey.cs` / `InspirationDebugHotkey` | mixed combat namespaces | Debug; several wired in Test/CombatTest; smoke/test bypass production entry | Keep in Debugging; never production-wire |
| `Assets/GAME/Scripts/Debugging/StoryInteractionDebugHotkey.cs` / `StoryInteractionDebugHotkey`; `VerticalSliceSceneValidator.cs` / `VerticalSliceSceneValidator` | `Game.Story.Interaction`, `Game.Debugging` | Debug; validator in Demo/Dungeon 1 | Keep in Debugging |
| `Assets/GAME/Scripts/Player/Runtime/PlayerInputController.cs` / `PlayerInputController`; `PlayerMotor2D_New.cs` / `PlayerMotor2D_New`; `PlayerAnimationController.cs` / `PlayerAnimationController`; `PlayerFieldAttackController.cs` / `PlayerFieldAttackController` | `Game.Player` | Runtime current Dungeon 1 stack; attack enters CombatEntryPoint | Keep/Modify input ownership |
| `Assets/GAME/Scripts/Player/FieldSkillCaster.cs` / `FieldSkillCaster` | `Game.Player` | Runtime field damage/entry interaction; no YAML found | Merge candidate with field attack |
| `Assets/GAME/Scripts/Player/OverworldPlayerController.cs` / `OverworldPlayerController`; `PlayerAnimator2D.cs` / `PlayerAnimator2D`; `PlayerMotor2D.cs` / `PlayerMotor2D` | mixed | Legacy stack; Demo/Test usage | Move to Legacy after scene migration |
| `Assets/GAME/Scripts/Player/IPlayerInput.cs` / `IPlayerInput`; `PlayerInputUnity.cs` / `PlayerInputUnity`; `PlayerController2D.cs` / `PlayerController2D` | `Game.Player` | Legacy old Input Manager abstraction | Move to Legacy after validation |
| `Assets/GAME/Scripts/Player/Overworld/OverworldAttack2D.cs` / `OverworldAttack2D`; `OverworldInputBridge.cs` / `OverworldInputBridge`; `OverworldPlayerDriver.cs` / marker; `OverworldPlayerGlue.cs` / `OverworldPlayerGlue` | global / `GAME.Player.Overworld` | Legacy parallel PlayerInput/glue stack; Demo/Test partial usage | Move to Legacy after validation |
| `Assets/GAME/Scripts/UI/GameUIRootController.cs` / `GameUIRootController`; `UIScreenRouter.cs` / `UIScreenRouter` | `Game.UI` | Runtime global visibility route; six scenes | Keep/Modify as sole route |
| `Assets/GAME/Scripts/UI/RewardUIPanel.cs` / `RewardUIPanel`; `RewardItemUI.cs` / `RewardItemUI` | `Game.UI` | Runtime reward UI; panel four scenes, item two prefabs | Keep/Modify |
| `Assets/GAME/Scripts/UI/BattleTransitionController.cs` / `BattleTransitionController` | `Game.Battle` | Legacy transition-only state writer; InGame/Test | Move to Legacy after validation |
| `Assets/GAME/Scripts/UI/DemoEndController.cs` / `DemoEndController`; `DemoEndInteractionEventSO.cs` / `DemoEndInteractionEventSO` | `Game.UI` | Demo ending; Dungeon 1 / tutorial asset | Keep Demo |
| `Assets/GAME/Scripts/UI/DaySettlementPanel.cs` / `DaySettlementPanel`; `MissionSelectPanel.cs` / `MissionSelectPanel` | `Game.UI` | Runtime non-combat UI | Keep |
| `Assets/GAME/Scripts/UI/Runtime/OverworldHUDRoot.cs` / `OverworldHUDRoot`; `BagButtonHUD.cs`; `CurrencyHUD.cs`; `DialogueLogHUD.cs`; `MapHUD.cs`; `MissionTrackerHUD.cs`; `StatusHUD.cs` / same-name types | `Game.UI` | Runtime field HUD pieces, mostly Test only | Keep; route through field root |
| `Assets/GAME/Scripts/UI/Runtime/CombatUIDialogueBlocker.cs` / `CombatUIDialogueBlocker` | `Game.UI` | Runtime workaround hiding dialogue on combat; no YAML found | Merge candidate into router |
| `Assets/GAME/Scripts/UI/ScreenFader.cs` / `ScreenFader` | `Game.UI` | Runtime transition presentation | Keep |

## Supporting flow files inspected

Important supporting types outside the primary inventory: `Game.Story.DialogueRunner`, `StoryEventRunner`, `DialoguePanel`, `DialogueUIPanel`, `ChoiceUIPanel`, `TimedChoicePanel`, `StoryInteractionController`, `StoryInteractable2D`, `StoryEventTrigger2D`, `StoryDialogueTrigger2D`, `SceneTravelService`; `Game.Dialogue.TimedChoiceDialoguePanel`; `Game.Interaction.InteractionController`, `InteractableObject`; `Game.Mission.MissionManager`, `MissionHUD`; `Game.NonCombat.Dialogue.DialogueController`; tutorial combat adapters; `Game.Legacy.Battle.BattleTrigger2D` and `SeamlessBattleManager`; player/enemy field motors. Keep runtime Story components, classify old NonCombat dialogue and explicit Legacy battle as Legacy, and do not delete any until YAML/SO reference migration is complete.

## Validation baseline

- Pre-report `git status` already contained user changes: modified `Assets/GAME/Scenes/Dungeon 1.unity`, deleted `Dungeon 4.unity` and `.meta`, and untracked `New_Dungeeon_Manual.unity` and `.meta`.
- `dotnet build GAME_002.sln --no-restore` cannot start because the generated solution contains duplicate `Unity.Timeline` project names (`MSB5004`).
- A Unity 6000.2.6f2 batch compile attempt could not connect to Package Manager IPC and exited before compilation; existing `unity-compile*.log` files contain no `error CS` lines.
- The YAML GUID scan resolved every `m_Script` GUID either to a GAME script meta or a package script meta. No missing script GUID was found by text scan. This does not validate broken object references or UnityEvent method signatures in the Editor.

