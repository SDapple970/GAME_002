# Production Interaction setup

`InteractionController` owns Exploration input, nearby-candidate registration, deterministic target selection, and prompt intent. It submits an `InteractionRequest` to the single `InteractionRunner`. `InteractionRunner` validates state and executes authored events; `InteractionRuntime` owns session and persistent state and participates in save/load. `InteractableObject` is only the field adapter and authored source.

## Authoring an object

Add `InteractableObject`, keep its trigger collider registered with the player controller, and choose a use policy:

The canonical `Gameplay/Interact` keyboard binding is `F`, so new objects default to `F: 조사`. Keep authored Production prompt text aligned with that binding. The current Input layer does not expose a reusable binding-display API, so rebinding does not yet update prompt text dynamically.

- `LegacyCompatibility` preserves the old `interactOnce` and `disableAfterInteract` behavior. Do not use it for new Production content.
- `Repeatable` never records consumption.
- `OncePerSession` records consumption until the runtime session resets.
- `PersistentOnce` records consumption in Schema 5 saves and requires a non-empty globally unique `interactionId`.

Use trimmed dotted IDs such as `dungeon.01.chest.entry.01`. Never derive an ID from a GameObject name, hierarchy, instance ID, or load order. When copying `Dungeon_Template`, replace the dungeon scope in every persistent ID before treating the copy as Production. `Testing_Dungeon_Template` is excluded from cross-Production duplicate checks.

Each irreversible Event SO should author a stable `actionId`. Empty action IDs retain the compatibility fallback `event:<authored-index>`, so reordering such events changes their identity. `RewardInteractionEventSO` and `RandomLootInteractionEventSO` default to the field `interactionId`. A non-empty legacy `rewardSourceId` has priority and preserves the Schema 4 identity exactly: Interaction rewards keep an empty action ID, while Loot keeps the selected item ID as its action ID when the new authored `actionId` is empty.

## Consequence routing

- Rewards and loot call `RewardService`; Event SOs never discover or activate Reward UI.
- Story events use `StoryInteractionEventSO` and `StoryEventRunner.TryStartEvent`.
- Quest events use `QuestInteractionEventSO` and `QuestRuntime.ApplyEvent` with a canonical Interaction identity.
- SearchRewardManager, SearchableInteractable2D, StoryDialogueTrigger2D, old QuestManager Interaction events, timed local dialogue, Tutorial, and Demo events remain compatibility paths. Do not author new Production content against them.

Persistent random loot resolves an entry once with an isolated `System.Random`, stores its stable entry ID before reward execution, and restores that result after reload. Empty entry IDs use `entry:<index>` compatibility. A valid “nothing found” entry consumes `PersistentOnce`; a missing RewardService preserves the selected result but does not consume the object.

Multi-event execution is authored-order deterministic. A blocked or no-effect request is not consumed. If at least one irreversible effect is accepted and another event fails, the runner reports `PartialFailure` and consumes a one-shot interaction so already-applied consequences are never retried.

## Production NPC dialogue fixture

`Assets/GAME/Prefabs/Interaction/ProductionNpcInteraction.prefab` is the minimal reusable NPC interaction fixture. It authors `F: 대화`, a trigger `Collider2D`, `Repeatable`, and one `StoryInteractionEventSO` that references the existing stable Story definition. The prefab does not own input, UI, `GameState`, or Story execution.

Place it only in a scene that already has the canonical `StoryEventRunner` and a connected Production presenter. `Dungeon_Template` intentionally has an empty Narrative extension slot, so placing the NPC there without installing the existing Narrative runtime/presenter is an incomplete setup. Existing Dungeon 1 `StoryInteractable2D` and Demo/Legacy dialogue assets remain compatibility content and are not migrated by this fixture.

## Visuals and manual validation

Optionally add `InteractionVisualStateAdapter` and wire available/consumed sprites, active objects, and the prompt collider. Restore applies visuals without executing events. Keep the root and `InteractableObject` alive so save restoration can find it.

`RuntimeBootstrapper` is the Production installation owner and creates or adopts exactly one `InteractionRuntime` followed by one `InteractionRunner` before `SaveLoadService`. Existing scenes with authored instances keep them. `InteractionRunner.ResolveOrCreate` remains only an on-demand compatibility fallback for callers that execute before the global bootstrap is available.

Every Production dungeon must contain exactly one `InteractionController` on the moving player root. `Dungeon_Template` authors it as an added component on `Actors/Player/PlayerRoot`; do not apply it to `Player.prefab`, because Demo and compatibility scenes retain scene-local controllers. Do not promote Story, Search, Demo, or Legacy interaction controllers into this Production path.

`ProductionDungeonUI/FieldRoot/InteractionPromptHost` owns the canonical `InteractionPromptUI`. Its child `InteractionPromptRoot` is the display object and contains the non-raycast `PromptText`; the host remains active when the prompt is hidden. `Dungeon_Template` explicitly connects the player `InteractionController.promptUI` to this prefab component. The prompt only presents text. `InteractionController`, `InteractionRunner`, and the Input/Core state owners continue to decide targeting, execution, and whether interaction is allowed.

In Unity, verify Repeatable, session-only, and persistent objects; save/reload an opened object; temporarily remove RewardService and confirm random loot does not reroll; verify Dialogue/Combat/Reward/Pause block interaction; and confirm prompts refresh after consumption and Story ownership acceptance.
