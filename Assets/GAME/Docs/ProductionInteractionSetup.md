# Production Interaction setup

`InteractionController` owns Exploration input, nearby-candidate registration, deterministic target selection, and prompt intent. It submits an `InteractionRequest` to the single `InteractionRunner`. `InteractionRunner` validates state and executes authored events; `InteractionRuntime` owns session and persistent state and participates in save/load. `InteractableObject` is only the field adapter and authored source.

## Authoring an object

Add `InteractableObject`, keep its trigger collider registered with the player controller, and choose a use policy:

- `LegacyCompatibility` preserves the old `interactOnce` and `disableAfterInteract` behavior. Do not use it for new Production content.
- `Repeatable` never records consumption.
- `OncePerSession` records consumption until the runtime session resets.
- `PersistentOnce` records consumption in Schema 5 saves and requires a non-empty globally unique `interactionId`.

Use trimmed dotted IDs such as `dungeon.01.chest.entry.01`. Never derive an ID from a GameObject name, hierarchy, instance ID, or load order. When copying `Dungeon_Template`, replace the dungeon scope in every persistent ID before treating the copy as Production. `Testing_Dungeon_Template` is excluded from cross-Production duplicate checks.

Each irreversible Event SO should author a stable `actionId`. Empty action IDs retain the compatibility fallback `event:<authored-index>`, so reordering such events changes their identity. `RewardInteractionEventSO` and `RandomLootInteractionEventSO` default to the field `interactionId`; a non-empty legacy `rewardSourceId` remains an explicit compatibility override.

## Consequence routing

- Rewards and loot call `RewardService`; Event SOs never discover or activate Reward UI.
- Story events use `StoryInteractionEventSO` and `StoryEventRunner.TryStartEvent`.
- Quest events use `QuestInteractionEventSO` and `QuestRuntime.ApplyEvent` with a canonical Interaction identity.
- SearchRewardManager, SearchableInteractable2D, StoryDialogueTrigger2D, old QuestManager Interaction events, timed local dialogue, Tutorial, and Demo events remain compatibility paths. Do not author new Production content against them.

Persistent random loot resolves an entry once with an isolated `System.Random`, stores its stable entry ID before reward execution, and restores that result after reload. Empty entry IDs use `entry:<index>` compatibility. A valid “nothing found” entry consumes `PersistentOnce`; a missing RewardService preserves the selected result but does not consume the object.

Multi-event execution is authored-order deterministic. A blocked or no-effect request is not consumed. If at least one irreversible effect is accepted and another event fails, the runner reports `PartialFailure` and consumes a one-shot interaction so already-applied consequences are never retried.

## Visuals and manual validation

Optionally add `InteractionVisualStateAdapter` and wire available/consumed sprites, active objects, and the prompt collider. Restore applies visuals without executing events. Keep the root and `InteractableObject` alive so save restoration can find it.

In Unity, place one explicit `InteractionRunner` with one `InteractionRuntime` in the Production bootstrap scene (the runtime fallback creates this pair for unchanged Legacy scenes). Verify Repeatable, session-only, and persistent objects; save/reload an opened object; temporarily remove RewardService and confirm random loot does not reroll; verify Dialogue/Combat/Reward/Pause block interaction; and confirm prompts refresh after consumption and Story ownership acceptance.
