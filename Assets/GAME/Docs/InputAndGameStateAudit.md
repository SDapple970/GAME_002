# Input and Game State Audit

## State definitions

Global: `Assets/GAME/Scripts/Core/GameState.cs`, `Game.Core.GameState`: Boot, Title, Exploration, Dialogue, Choice, CombatTransition, CombatPlanning, CombatResolving, Reward, Cutscene, UIOnly, Loading, Paused, and compatibility alias Combat=CombatPlanning.

Combat-local: `Game.Combat.Model.Phase` in `CombatEnums.cs`: EnterCombat, Planning, Resolving, ExitCombat. Other local state equivalents include `OverworldEnemyAI.AIState`, `RuntimeBootstrapper.InitialStateMode`, quest progress flags, Story runner activity, and many panel booleans; they are not global game states.

## Global state writers

`GameStateMachine.SetState` is callable without transition validation. Writers found: `GameFlowController`, `SceneFlowController`, `RuntimeBootstrapper`, `CombatEntryPoint`, `CombatStateSyncer`, `CombatDemoFlowController`, `CombatRewardUIBinder`, `BattleTransitionController`, `SeamlessBattleManager`, `DialogueRunner`, `StoryEventRunner`, `TimedChoiceDialoguePanel`, `NonCombat.Dialogue.DialogueController`, `SceneTravelService`, `QuestCompletionFlow`, `MissionCompletePanel`, `MissionCompletionController`, `DemoRescueNpcEndFlow`, `DemoEndPanelController`, and `MissionCompleteCutsceneController`.

Independent movement/input locks: `PlayerInputController`, `PlayerFieldAttackController`, `FieldSkillCaster`, `OverworldPlayerController`, `PlayerController2D`, `OverworldAttack2D`, `InteractionController`, `StoryInteractionController`, `InteractionDetector2D`, `StoryInteractable2D`, enemy patrol/AI, `CombatFieldLock`, `MissionCompletionController.behavioursToDisableOnComplete`, and local `_inputLocked`/running flags in DemoMission/Story panels. Most check Exploration, but `CombatFieldLock` and mission completion also disable behaviours directly.

## Transition reality

- Exploration -> combat: field/tutorial/debug request -> `CombatEntryPoint` -> local Planning -> global CombatPlanning. `CombatTransition` is used only by legacy `BattleTransitionController`.
- Planning -> resolving: local `CombatStateMachine` changes phase; global state incorrectly remains CombatPlanning.
- Combat completion -> reward: entry result event -> `CombatRewardUIBinder` -> `GameFlowController.HandleCombatResult` -> Reward. Demo controller instead writes UIOnly.
- Reward -> exploration: `RewardUIPanel.OnClosed` -> binder -> `GameFlowController.HandleRewardClosed`; demo controller may do the same independently.
- Exploration -> dialogue: StoryEventRunner normally writes UIOnly (serialized configurable); DialogueRunner normally writes Cutscene; NonCombat DialogueController writes UIOnly. The explicit Dialogue enum is not the dominant live path.
- Dialogue -> choice: panels change local visibility; no active runner was found writing `GameState.Choice`.
- Dialogue/choice -> exploration: StoryEventRunner.EndEvent or DialogueRunner.EndDialogue writes Exploration based on flags; TimedChoiceDialoguePanel and NonCombat DialogueController also restore it independently.

The enum describes a cleaner model than the implemented transitions. UIOnly and Cutscene currently substitute for Dialogue/Choice, while CombatResolving is unused globally.

## Input owners

Input System paths:

- `GameInputInstaller` owns generated `inputactions`, enables the Gameplay map on enable, subscribes Move/Jump/Attack/Parry/Interact/Pause, and forwards to static `InputService` events.
- `PlayerInputController` subscribes `InputService` for movement/jump/attack and checks exploration permission.
- `StoryEventRunner` and `StoryInteractionController` subscribe to `InputService.Interact` for advance/interact.
- `OverworldInputAdapter` independently enables/disables `InputActionReference` fields move/jump/attack.
- `OverworldInputBridge` owns a `PlayerInput` component and calls `SwitchCurrentActionMap("Gameplay")`.
- `CombatFieldCallDebug` has three `InputActionReference` fields and enables/disables them itself.
- `CombatStartSmokeTest` has `debugStep`; `MissionCompleteCutsceneController` has `skipAction` and subscribes/enables it.

All `InputActionReference` users found: `OverworldInputAdapter` (move, jump, attack), `CombatFieldCallDebug` (three debug combat actions), `CombatStartSmokeTest` (debugStep), and `MissionCompleteCutsceneController` (skipAction). The only `PlayerInput` owner found in GAME scripts is `OverworldInputBridge`.

Direct legacy polling includes `PlayerInputUnity` axes/buttons; CombatEntryPoint F9/F10 editor completion; `CombatTestRunner` Space; debug validators/hotkeys; DemoRescueNpcEndFlow interaction key; StoryDialogueTrigger2D E; TimedChoicePanel choice keys; StoryInteractionController fallback; cutscene skip fallback; Search interact/yes/no; SaveManager F5/F6; Office mouse raycast. These paths bypass `InputService` and action-map/state routing.

## Actual per-mode paths

- Exploration: Gameplay action map -> `GameInputInstaller` -> `InputService` -> `PlayerInputController`; Story interaction also consumes Interact. Older Demo/Test scenes retain legacy controller/input variants.
- Dialogue: StoryEventRunner consumes the same Interact event; DialoguePanel next button invokes local event. DialogueRunner UI is button-driven. Fallback direct keys exist.
- Choice: generated UI Button listeners in ChoiceUIPanel/DialoguePanel; TimedChoicePanel additionally polls number keys. No dedicated Choice map.
- Combat planning: Unity UI Buttons built by CombatPlanningHUD; confirm submits through orchestrator. No dedicated combat action map.
- Combat resolving: no intended player input gate beyond UI hidden/local submitted flag; Gameplay map remains enabled but movement code rejects non-Exploration. Because global state remains CombatPlanning, this is still blocked for exploration but cannot distinguish resolving.
- Reward: RewardUIPanel close Button; no dedicated map. Gameplay remains enabled but exploration consumers reject Reward.
- Pause: Pause event exists, and `InputRouter` allows pause logic, but no authoritative pause menu flow/owner was identified in the requested runtime.

## Required consolidation

Keep one enabled Gameplay/UI map policy in `GameInputInstaller` (or its successor without adding a parallel manager). Route interaction, confirm, cancel, choice, combat selection, reward close, and pause through state-aware commands. Remove component-level action enable/disable only after migrating serialized `InputActionReference` fields. Preserve fallback keys exclusively in Debug/Legacy builds or components.

