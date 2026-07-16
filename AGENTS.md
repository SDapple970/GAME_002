# AGENTS.md — GAME_002 Repository Instructions

This file is the repository-level instruction source for Codex and other coding agents working on GAME_002.

## 1. Authority and scope

- These rules apply to the entire repository unless a closer nested `AGENTS.md` or `AGENTS.override.md` provides more specific instructions.
- Follow the current task prompt first, then the active instruction files, then the actual repository state.
- The current code, Unity scenes, prefabs, ScriptableObjects, `.meta` files, tests, and compile results are the implementation source of truth.
- External planning documents, Google Docs, chat history, and roadmap documents are reference material only unless the current task explicitly names them as required inputs.
- Do not silently implement roadmap items that are outside the requested task.

## 2. Mandatory startup protocol

Before editing any file:

1. Report the active instruction files that were loaded.
2. Inspect the current branch, `git status`, and existing uncommitted changes.
3. Preserve unrelated user changes. Never reset, revert, overwrite, or reformat them.
4. Inspect the relevant implementation, references, namespaces, tests, and call sites.
5. Search for scene, prefab, ScriptableObject, and serialized-field risks before deleting, moving, renaming, or changing public/serialized APIs.
6. Identify the current production owner or entry point for the requested behavior.
7. Identify duplicate, Legacy, Demo, Debug, or compatibility paths that could conflict with the change.
8. State the expected file scope, compatibility risks, and validation plan before making a non-trivial change.

Do not begin a broad refactor from filenames or planning documents alone.

## 3. Source-of-truth priority

When instructions or implementations conflict, use this order:

1. The current user task.
2. Active `AGENTS.md` and `AGENTS.override.md` files.
3. Current repository code and Unity serialized assets.
4. Tests, compiler output, and reproducible runtime behavior.
5. Repository documents explicitly named by the current task.
6. External planning documents and old reports.

Report material conflicts instead of guessing.

## 4. Project environment

- Repository: `SDapple970/GAME_002`
- Unity version: `6000.2.6f2`
- Main runtime script root: `Assets/GAME/Scripts`
- Main project data root: `Assets/GAME/Data`
- Main prefab root: `Assets/GAME/Prefabs`
- Main scene root: `Assets/GAME/Scenes`
- Project Input Actions asset: `Assets/GAME/Input/GAME002_InputActions.inputactions`
- The project uses the Unity Input System and URP.

Do not change Unity, package, render-pipeline, input-system, or dependency versions unless the task explicitly requires it.

## 5. Core architecture principles

- Give each system one clear production entry point.
- Do not create a god manager that owns unrelated responsibilities.
- Separate immutable configuration data from runtime state and execution logic.
- Connect field objects to combat core only through adapters and requests.
- Drive global flow through `GameState` and the Core flow layer.
- Route UI visibility through the UI router and game state, not independent panel decisions.
- Receive Unity Input System actions in the Input layer and route commands to feature systems.
- Keep Runtime, Debug, Test, Demo, Legacy, and Editor responsibilities visibly separated.
- Prefer migration and compatibility shims over immediate deletion.
- Keep the repository compiling throughout large refactors.

## 6. System ownership

### Core — `Game.Core`

Owns global state, scene flow, bootstrap, save/load coordination, and cross-system game flow.

Key owners include:

- `GameState`
- `GameStateMachine`
- `GameFlowController`
- `SceneFlowController`
- `RuntimeBootstrapper`
- `SaveLoadService`

Rules:

- `GameStateMachine` stores and validates global state.
- Production features should request state changes through `GameFlowController`.
- Do not add new direct global-state writers in feature code.
- Exploration input must never be active during dialogue, choice, combat, reward, cutscene, loading, UI-only, or pause states.

### Input — `Game.Input`

Owns Unity Input System actions, action-map activation, command routing, and rebind persistence.

Rules:

- Restrict `InputActionReference` and action-map ownership to the Input layer where practical.
- `PlayerController`, combat HUDs, and dialogue runners should receive commands or values, not own input assets.
- Do not add direct keyboard polling to production runtime code.
- Editor-only debug shortcuts must be guarded and must not become required production flow.
- Do not manually edit generated Input System wrapper code.

### World — `Game.World`

Owns player movement, field enemies, interactions, encounters, and stage objects.

Rules:

- World objects detect and request behavior; they do not calculate combat rules or grant rewards directly.
- Encounters create an `EncounterRequest` or combat start request and send it to `CombatEntryPoint`.
- Field enemies must not become alternate combat engines.

### Combat — `Game.Combat`

Owns turn planning, rule resolution, skills, combat results, and combat presentation coordination.

Rules:

- `CombatEntryPoint` is the production start/end entry point.
- `CombatStateMachine` owns combat-local phases.
- `CombatTurnResolver` and related core services calculate outcomes.
- `CombatDirector` presents already-resolved outcomes on field objects.
- Combat core must not depend directly on scene GameObjects.
- Field objects enter combat through `FieldCombatantAdapter` or equivalent adapter/factory paths.
- Camera, formation, field lock, global-state synchronization, and field presentation belong in Combat Integration or presentation layers.
- Do not create a second production combat-start path.

### Narrative — `Game.Narrative`

Owns dialogue, choice, timed-choice, and story-event execution.

Rules:

- NPCs provide a dialogue definition or ID; they do not directly control the dialogue UI.
- Dialogue and choice must request their global states through the flow layer.
- Choice results should publish or call explicit integrations for quests, flags, rewards, or scene flow.

### Quest — `Game.Quest`

Owns quest definitions, runtime progress, objective tracking, completion, and quest HUD data.

Rules:

- New production quest work targets `QuestDefinitionSO`, `QuestRuntime`, `QuestObjectiveTracker`, and `QuestCompletionFlow`.
- Existing DemoMission and Mission compatibility paths must not be deleted until references and serialized bindings are verified.
- Do not add another independent quest model.

### Reward and progression — `Game.Reward`, `Game.Inventory`

Own rewards, inventory, currency, and character progression.

Rules:

- Combat and quest results create reward requests.
- Reward services grant data changes.
- Reward UI displays the result but does not independently decide the entire global flow.
- Combat may transition through `Reward` before returning to `Exploration`.

### UI — `Game.UI`

Owns UI roots, screen routing, HUDs, panels, and widgets.

Rules:

- `UIScreenRouter` and the current `GameState` decide which UI roots are visible.
- Feature panels should not independently own global flow.
- Subscribe and unsubscribe symmetrically, especially in `OnEnable` and `OnDisable`.
- When a UI appears only when manually enabled, inspect router state, event subscription, root activation, and serialized references before adding another controller.

### Data

ScriptableObjects contain configuration data, identifiers, references, and authored content.

Rules:

- Do not store mutable session progress in definition assets.
- Execution belongs in runtime services, runners, controllers, resolvers, or state objects.
- Avoid embedding scene-specific runtime ownership inside ScriptableObjects.

### Debugging

Debug and test helpers belong under explicit Debugging, Tests, or Editor locations.

Rules:

- Production runtime must not require debug components.
- Editor-only debug input must be guarded with `UNITY_EDITOR` where appropriate.
- Demo controllers must not become competing production owners.
- Do not move Debug or Legacy code into a production namespace merely to make a compile error disappear.

## 7. C# and Unity coding rules

Follow the style already used by the surrounding production code.

- Use namespaces that match system ownership, normally under `Game.*`.
- Use PascalCase for types, methods, properties, and events.
- Use camelCase for local variables, parameters, and serialized private fields.
- Use `_camelCase` for non-serialized private runtime state when consistent with the surrounding file.
- Use explicit access modifiers.
- Keep one primary public top-level type per file, and match the filename to that type unless compatibility requires otherwise.
- Prefer small focused classes and explicit dependencies over hidden global searches.
- Cache required references; do not call broad object searches every frame.
- Use early returns for invalid state and null preconditions when they improve clarity.
- Include useful context in warnings and errors.
- Subscribe and unsubscribe events symmetrically.
- Do not add speculative abstractions, generic frameworks, or dependencies for a single current use.
- Do not perform unrelated formatting, renaming, or cleanup in a focused task.
- Preserve existing public APIs and serialized fields unless the task explicitly authorizes a migration.
- Comments should explain ownership, intent, or non-obvious constraints, not restate the code.

## 8. Unity serialization and asset safety

- Never delete, move, or rename a script, class, serialized field, prefab, scene, or ScriptableObject before checking references.
- Preserve existing `.meta` files and GUIDs.
- Do not reuse or invent an existing asset GUID.
- Prefer Unity Editor asset moves when available.
- When a serialized field must be renamed, preserve compatibility with an appropriate migration such as `FormerlySerializedAs` when valid.
- Do not change field types merely to satisfy a compile error without auditing serialized data.
- Do not edit scene or prefab YAML unless the task requires it and the change can be validated.
- Report every required Inspector reconnection, removed component, new component, changed asset reference, or migration step.
- If serialization safety cannot be verified, retain the old field or compatibility component and report the limitation.

## 9. Legacy, Demo, and compatibility policy

- Do not immediately delete uncertain code.
- First inspect code references, scene components, prefab components, ScriptableObject references, tests, and reflection/string-based usage.
- Prefer this sequence:
  1. Identify the production owner.
  2. Stop adding new production dependencies to the old path.
  3. Add a compatibility adapter or forwarding path if needed.
  4. Validate scenes and prefabs.
  5. Move the old code to Legacy/Debug only when safe.
  6. Delete only in an explicitly approved cleanup after verification.
- Keep compatibility behavior compiling while migrating ownership.

## 10. Scope and Git discipline

- Make the smallest coherent change that completes the task.
- Preserve unrelated modified and untracked files.
- Do not use destructive Git commands.
- Do not reset the repository, rewrite history, force-push, or discard user work.
- Do not create commits, branches, or pull requests unless the user asks.
- Do not add or update packages without explicit approval.
- Do not mass-rename namespaces or folders in the same batch as behavioral work unless explicitly requested.
- Do not claim a scene, prefab, or Inspector setup was tested when only code compilation was run.

## 11. Validation requirements

Use the best validation available for the task.

Minimum checks for C# changes:

1. Inspect `git diff` and confirm only intended files changed.
2. Run whitespace or patch sanity checks such as `git diff --check`.
3. Verify Unity C# compilation using the repository's established Unity workflow when available.
4. Run relevant EditMode tests.
5. Run relevant PlayMode tests when the behavior depends on scenes, components, coroutines, animation, physics, input, or UI.
6. Provide a manual Unity test sequence for anything that cannot be automated.
7. Record warnings separately from errors.
8. Never fabricate a passing test.

If Unity or a required scene cannot be executed, explicitly state what was not run and why.

## 12. Required completion report

Every completed coding task must report:

1. Summary of the implemented behavior.
2. Active instruction files loaded.
3. Modified files.
4. Added files.
5. Deleted or moved files.
6. Public API changes.
7. Unity Inspector, scene, prefab, ScriptableObject, and `.meta` implications.
8. Tests and compile checks actually run, with results.
9. Manual test procedure.
10. Known risks, deferred work, and compatibility paths retained.
11. Confirmation that unrelated existing changes were preserved.

Do not hide partial completion or unverified behavior.

## 13. Expected task structure

Task prompts may provide:

1. Objective.
2. Files to modify or add.
3. Existing structure that must remain.
4. Detailed changes.
5. Unity Inspector connection changes.
6. Test procedure.
7. Completion criteria.
8. Cautions.

Treat unspecified files as out of scope unless inspection proves that a minimal supporting change is required. Explain any necessary scope expansion in the completion report.

## 14. Definition of done

A task is done only when:

- The requested behavior is implemented within the agreed scope.
- The project compiles, or an external blocker is clearly documented.
- Relevant tests have passed, or missing test capability is explicitly stated.
- Serialized Unity compatibility has been preserved or migration steps are documented.
- No competing production owner or duplicate entry path was introduced.
- Inspector and manual validation steps are documented.
- The final report accurately distinguishes verified results from assumptions.
