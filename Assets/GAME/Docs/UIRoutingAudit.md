# UI Routing Audit

## Current owners

| Surface | Current controller(s) | Routing behavior |
|---|---|---|
| Field HUD | `GameUIRootController.fieldRoot`, `OverworldHUDRoot`, Bag/Currency/DialogueLog/Map/MissionTracker/Status HUDs, `MissionHUD`, `QuestTrackerUI` | Global router toggles field root; children also own local visibility/events |
| Dialogue panel | `StoryEventRunner` + `DialoguePanel`/`StoryDialogueHUD`; separately `DialogueRunner` + `DialogueUIPanel`; `TimedChoiceDialoguePanel`; NonCombat dialogue panel | Multiple complete dialogue stacks; mostly local Show/Hide |
| Choice panel | `ChoiceUIPanel`, `DialoguePanel` embedded choices, `TimedChoicePanel`, `TimedChoiceDialoguePanel` | Local flags/buttons; global Choice state rarely/never written |
| Combat planning HUD | `CombatPlanningHUD`, plus `CombatUIRootController` and `CombatDemoFlowController` roots | Entry events and local Show/Hide |
| Combat log | `CombatLogHUD` | Entry/state events; wired in Demo/Test |
| Reward panel | `RewardUIPanel`, `CombatRewardUIBinder`, `CombatDemoFlowController`, `GameUIRootController.rewardRoot` | Four potential visibility/state owners |
| Pause menu | `GameUIRootController.pauseRoot` and `UIScreenRouter` | Root slot exists; no authoritative pause controller discovered |

## Subscription hygiene

The primary components generally pair subscriptions correctly: `UIScreenRouter`, `CombatUIRootController`, `CombatRewardUIBinder`, `CombatDemoFlowController`, `CombatStateSyncer`, `CombatPlanningHUD`, `CombatLogHUD`, `StoryEventRunner`, `DialoguePanel`, `MissionObjectiveTracker`, `MissionCompletionController`, `QuestCompletionFlow`, and `RewardUIPanel` bind in OnEnable and unbind in OnDisable. Several also defensively subtract before add.

Risks:

- Auto-binding in Awake/OnEnable can bind a different instance after scene reload while boolean `_subscribed` flags still assume the old reference.
- Inactive objects do not receive OnEnable and therefore cannot subscribe merely because another controller later activates only a nested root.
- RuntimeBootstrapper can create empty `GameUIRootController` and `UIScreenRouter` objects. Auto-binding by type/name then makes behavior dependent on scene hierarchy names.
- Button listeners created by planning/choice panels are destroyed with spawned buttons. Serialized UnityEvent listeners remain opaque to C# search and must be checked in Inspector.

## Direct visibility conflicts

`UIScreenRouter.Apply` is intended to map GameState to `GameUIRootController`. However, `CombatUIRootController.SetCombatVisible` directly toggles combat HUD, widget container, planning panel, overworld canvas, and reward canvas. `CombatDemoFlowController` toggles another combatCanvasRoot and reward panel. `CombatPlanningHUD`, `RewardUIPanel`, Dialogue/Choice panels, DemoMission panels, interaction prompts, and field HUD children also call `SetActive`.

Panels at special risk of Inspector-only operation:

- `CombatPlanningHUD` needs entry point, orchestrator, button prefab, skill/target roots, confirm button, and texts. Auto-bind covers only some references.
- `CombatUIRootController` needs explicit root objects; auto-bind uses names/types and can silently select unintended objects.
- `RewardUIPanel` needs root, close button, row root/prefab/texts. It can be found inactive, but its own OnEnable button binding depends on component activation semantics.
- Story `DialoguePanel`, `DialogueUIPanel`, `ChoiceUIPanel`, `TimedChoicePanel`, and legacy `TimedChoiceDialoguePanel` each require different serialized roots/prefabs.
- `GameUIRootController` has eight optional roots and name-based fallback; a bootstrapped empty instance is valid C# but not functional UI.
- Pause has only a root slot; there is no discovered controller to open/close it.

## Recommended route

Make `UIScreenRouter` plus `GameUIRootController` the sole top-level visibility route. Panels should own their contents and interaction, not global canvas visibility. Map actual states including Dialogue, Choice, CombatPlanning, CombatResolving, Reward, and Paused. During migration, disable one competing owner per compile-safe batch and preserve all serialized roots until scene checks pass.

