# File Minimization Rules

## New File Creation Rules

Create a new file only when it owns a stable production responsibility, represents static data, isolates runtime state, prevents cross-layer coupling, or matches an established Unity component boundary.

Do not create new wrappers for temporary fixes unless they are explicit compatibility bridges with a migration note.

## Merge Rules

Merge when a file only forwards calls, duplicates another owner, has no independent lifecycle, or is a debug helper used by one debug tool.

Do not merge data definitions, interfaces, combat core models, UI panels, world adapters, or systems that separate static data from runtime state.

## Delete Rules

Hard-delete only after confirming all of the following:

- No .cs reference exists.
- No .unity scene GUID reference exists.
- No .prefab GUID reference exists.
- No .asset ScriptableObject reference exists.
- Replacement exists or the file is truly unused.
- Unity editor compile succeeds after deletion.

If any point is uncertain, move to Legacy or Archive instead.

## Archive Rules

Archive obsolete risky scripts that should not compile under Assets/GAME/_Archive~/Scripts/ and rename to .cs.disabled if needed. Add a README explaining replacement and migration path.

No files were archived in this pass.

## Demo Promotion Rules

DemoMission files must not be expanded as production systems. Promote data to Quest/Mission definitions, runtime progress to QuestRuntime, objective updates to QuestObjectiveTracker, completion to QuestCompletionFlow, and rewards to RewardService.

Keep DemoMission compatibility only while scenes/assets still reference it.

## Debug / Legacy Separation Rules

Debugging tools live under Assets/GAME/Scripts/Debugging and must be optional. Runtime must not require Debugging.

Legacy compatibility lives under Assets/GAME/Scripts/Legacy. Runtime should not add new dependencies on Legacy. Existing scene-referenced legacy components must have documented replacements.

## Combat Ownership Rules

- Combat starts only through CombatEntryPoint.
- Triggers build or resolve encounter field objects and pass them to CombatEntryPoint.
- CombatStateMachine owns phase flow.
- CombatTurnResolver owns turn calculation.
- CombatDirector owns presentation.
- FieldEnemy must not calculate combat.
- Old Battle scripts are Legacy compatibility only.

## Input Ownership Rules

- Unity Input System events enter through GameInputInstaller.
- InputService exposes canonical input events.
- InputRouter gates input by GameState.
- Player movement, jump, attack, and parry are Exploration-only.
- UI/dialogue/choice confirmation should use UI/dialogue adapters, not direct Input.GetKeyDown polling.
- Debug input belongs in Debugging.

## UI Ownership Rules

- GameStateMachine is the high-level state source.
- UIScreenRouter maps GameState to root visibility.
- GameUIRootController toggles root objects.
- Panels display data and emit intent events.
- Panels do not grant rewards, load scenes, or decide full game flow.

## ScriptableObject Data Rules

- ScriptableObjects store definitions only.
- Runtime progress belongs in runtime services/classes.
- Save data uses IDs such as sceneName, spawnPointId, questId, dialogueId, itemId, stableId.
- Do not save GameObject references.
- Use FormerlySerializedAs or compatibility wrappers when renaming serialized fields/classes.