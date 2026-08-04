# Production Narrative Setup

`StoryEventRunner` is the only Production owner of Story lifecycle, node branching, choice outcomes, completion, cancellation, and Narrative GameState ownership. `StoryDialogueHUD`, `TimedChoicePanel`, and `DialoguePanel` only present runner-resolved data and emit user intent.

## Starting a Story

An NPC or field actor authors a `StoryEventDefinitionSO` reference and may provide a `StorySpeakerAnchor`. It requests execution with `StoryEventRunner.TryStartEvent`; it must not show Narrative UI, change `GameState`, apply effects, or advance Quest state itself.

Every Production event needs a stable, non-empty `eventId`. Every node needs a stable `nodeId`. Nodes use `nextNodeId`, choice `nextNodeId`, or `endEvent` for flow.

## Choices

A node may author zero to three displayable choices. The runner resolves choices once in authored order and passes the same resolved list to either Production presenter. If more than three choices would be displayed, the first three are used and an authoring warning is logged.

- Conditions met: visible and enabled.
- Conditions unmet with `hideIfConditionNotMet`: omitted.
- Conditions unmet without hiding: visible but disabled; `disabledReason` is appended by the current presenters.
- Disabled choices reject clicks, routed shortcuts, and timeout selection.

New choices should author a `choiceId` that is unique within their node. This keeps their effect identity stable if choices are reordered. Existing choices with an empty ID retain their authored-index compatibility identity and require no migration.

`timeoutChoiceIndex` refers to the resolved displayed position after hidden choices are removed. A valid `timeoutNodeId` takes precedence. Otherwise the indexed resolved choice must still be enabled when the timer expires. With no valid selectable or timeout outcome, the runner follows a valid `nextNodeId` or ends safely.

The current Input Actions asset has no dedicated choice-index bindings. UI button selection supports three choices, and an Input-layer adapter may call `InputService.SelectNarrativeChoice(0..2)`. Do not add device polling to presenters.

## Effects and persistence

Node effects are Story outcomes. Selected-choice effects are Choice outcomes for Reward routing. Choice Quest events retain the established Story source type so existing Schema 4 Quest identities remain consumed. `RewardService` owns gold, item, and requested EXP application; `QuestRuntime` owns Quest progress. Never mutate wallets, inventories, Quest progress, or `GameState` directly from a presenter or NPC.

`StoryProgressManager` owns completed Story event IDs and Story progress; `StoryFlagManager` owns current Production flags. Persona, chapter, Mission, and legacy flag effects remain compatibility forwarding paths for existing authored data.

## Inspector setup

- Assign one Production `StoryEventRunner`.
- Prefer `StoryDialogueHUD` with its world bubble and `TimedChoicePanel`; `DialoguePanel` is the Production fallback.
- `TimedChoicePanel` accepts one to three button/text slots. Existing two-slot setups remain valid and simply cannot display a third button until a third slot is connected.
- Do not use `TimedChoiceDialoguePanel`, `DialogueRunner`, NonCombat `DialogueController`, or `ChoiceRunner` for new Production Story content. They remain serialized compatibility/local-flow paths.

## Manual validation

1. Start a Story from Exploration through an NPC/field request and confirm Dialogue state.
2. Verify a node with three choices preserves authored order and all three UI buttons work when configured.
3. Verify an unmet hidden choice is absent and an unmet non-hidden choice is disabled with its reason.
4. Race a click against timeout and confirm one branch/effect wins.
5. Verify a valid timeout can select the third displayed choice.
6. Complete and cancel separate runs; completion should mark once, cancellation should not mark, and neither should overwrite Combat, Reward, Loading, or Pause.
7. Save/load after Story and Choice rewards and confirm replay grants nothing.
