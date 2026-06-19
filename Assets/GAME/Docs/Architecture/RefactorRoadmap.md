# Refactor Roadmap

## Completed In This Pass

- Added required GameState values while preserving old GameState.Combat compatibility.
- Added official thin owners: GameFlowController, SceneFlowController, RuntimeBootstrapper, SaveLoadService.
- Added InputService and InputRouter; GameInputInstaller now gates runtime movement by GameState.
- Moved combat debug tools and story debug hotkey to Debugging with .meta files preserved.
- Moved old Battle scripts and deprecated story flag manager to Legacy with .meta files preserved.
- Added UIScreenRouter and GameUIRootController for GameState-based UI visibility.
- Removed reward granting from RewardUIPanel; added RewardService.
- Added QuestRuntime, QuestObjectiveTracker, and QuestCompletionFlow wrappers.
- Routed central story advance and interaction through GameInputInstaller events.

## Not Completed

- Did not rename DemoMission ScriptableObject classes because asset references are risky.
- Did not hard-delete files because old scripts have unknown scene/prefab usage or compatibility value.
- Did not migrate every direct SceneManager.LoadScene call.
- Did not wire UIScreenRouter into scenes.
- Did not replace all physical input in cutscene/search/timed-choice compatibility paths.
- Did not implement full save/load schema, migrations, stable world object IDs, or save slots.
- Did not normalize all namespaces because Unity serialized script GUID safety was prioritized.

## Next Recommended Refactor Steps

1. Open Unity and resolve any missing scripts after project refresh.
2. Add RuntimeBootstrapper, RewardService, SaveLoadService, GameUIRootController, and UIScreenRouter to boot/test scenes.
3. Replace BattleTransitionController and old BattleTrigger usage with CombatEncounterTrigger2D/CombatEncounterGroup.
4. Split FieldEnemy into field AI + EncounterTrigger2D + quest objective event bridge.
5. Convert DemoMissionDefinitionSO assets to MissionDefinitionSO or future QuestDefinitionSO assets.
6. Replace DemoMissionRuntime progress calls with QuestRuntime/QuestObjectiveTracker calls.
7. Convert search/random loot/choice reward grants to RewardService requests.
8. Move direct SceneManager calls behind SceneFlowController wrappers.
9. Replace timed choice panel hotkeys with TimedChoiceController and UIInputAdapter.
10. Regenerate Unity project files and run editor compile/tests.

## Risk Order

1. Scene/prefab missing scripts after moves.
2. Duplicate input subscriptions in scenes that have both old and new player controllers.
3. Reward grant timing after RewardUIPanel stopped applying rewards.
4. DemoMission migration because assets and scenes are coupled.
5. UI root routing because old panel scripts still toggle their own roots.
6. Save/load schema changes because current save file lacks versioning and scene IDs.

## Test Order

1. Editor compile after project refresh.
2. Open TitleScene, Demo, Dungeon 1, Test, InGame and check missing scripts.
3. Exploration movement works only in Exploration.
4. Story interaction starts only through routed Interact.
5. CombatEncounterTrigger2D starts combat and blocks movement.
6. Combat planning HUD can submit a turn.
7. Combat victory grants gold once through RewardService.
8. RewardUIPanel close returns to Exploration through GameFlowController or fallback.
9. Demo mission completion still fires if demo scene is used.
10. Scene travel still loads and restores player spawn.