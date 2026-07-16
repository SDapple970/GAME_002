# Runtime Refactor Batch 2 — Input Ownership Report

## Result

Runtime Refactor Batch 2 establishes this production path:

`Unity Input System -> GameInputInstaller -> InputRouter -> InputService -> state-appropriate runtime consumer`

`GameInputInstaller` is the only production owner that constructs `GameInput`, registers raw callbacks, and enables the generated action collection. `InputService` is the canonical logical event surface. `InputRouter` selects one destination for the shared Interact action from explicit `GameState` rules. No Input Actions asset, generated input code, scene, prefab, ScriptableObject, or existing metadata file was changed.

## Files

Modified:

- `Assets/GAME/Scripts/Input/GameInputInstaller.cs`
- `Assets/GAME/Scripts/Input/InputService.cs`
- `Assets/GAME/Scripts/Input/InputRouter.cs`
- `Assets/GAME/Scripts/Player/Runtime/PlayerInputController.cs`
- `Assets/GAME/Scripts/Story/Runtime/StoryEventRunner.cs`
- `Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionController.cs`
- `Assets/GAME/Scripts/Interaction/InteractionController.cs`
- `Assets/GAME/Scripts/Cutscene/Runtime/MissionCompleteCutsceneController.cs`

Added:

- `Assets/GAME/Tests/Editor/InputOwnershipTests.cs`
- `Assets/GAME/Tests/Editor/InputOwnershipTests.cs.meta`
- `Assets/GAME/Docs/InputOwnershipBatchReport.md`
- `Assets/GAME/Docs/InputOwnershipBatchReport.md.meta`

Deleted or moved: none.

Explicitly unchanged:

- `Assets/GAME/Scripts/Input/inputactions.cs`
- `Assets/GAME/Scripts/Input/inputactions.inputactions`
- all scenes, prefabs, ScriptableObjects, and existing `.meta` files
- `GameStateMachine`, `GameFlowController`, and `RuntimeBootstrapper`

## Canonical ownership

`GameInputInstaller` creates one persistent `GameInput`, rejects duplicate installer instances before callback registration, registers and unregisters callbacks idempotently, and exclusively enables/disables the action collection. It retains the public `Actions`, `Service`, and `Router` properties. Rebinding and device monitoring continue to use the same persistent `Actions` instance; no scene or device event recreates it.

`InputService` exposes only the commands required by current production behavior:

- exploration: `Move`, `Jump`, `Attack`, `Parry`, `ExplorationInteract`
- narrative/cutscene: `DialogueAdvance`
- general: `PauseRequested`
- state: `CurrentMove`

When Exploration ends, the installer observes the authoritative state transition and calls `InputService.ClearMove(true)`. This stores zero and emits exactly one zero Move event immediately. A later physical Move cancellation does not emit a second zero, and returning to Exploration does not restore the old value.

Pause has one physical owner. The installer emits `PauseRequested`, then calls `GameFlowController.Pause()` or `ResumePreviousState()` once according to the authoritative current state. It does not change `Time.timeScale`.

## Explicit routing matrix

| GameState | Exploration commands | Interact destination | Dialogue advance | Pause | Existing UI/EventSystem |
|---|---|---|---|---|---|
| Boot | rejected | none | rejected | rejected | rejected |
| Title | rejected | none | rejected | allowed by preserved policy | allowed |
| Loading | rejected | none | rejected | rejected | rejected |
| Exploration | allowed | `ExplorationInteract` | rejected | allowed | not routed as gameplay input |
| Dialogue | rejected | `DialogueAdvance` | allowed | allowed | allowed |
| Choice | rejected | none | rejected | allowed | allowed; buttons remain authoritative |
| CombatTransition | rejected | none | rejected | allowed by preserved policy | rejected |
| CombatPlanning | rejected | none | rejected | allowed by preserved policy | allowed; buttons remain authoritative |
| CombatResolving | rejected | none | rejected | allowed by preserved policy | rejected |
| Reward | rejected | none | rejected | allowed by preserved policy | allowed; close button remains authoritative |
| Cutscene | rejected | `DialogueAdvance` | allowed for the existing routed skip consumer | allowed | allowed |
| UIOnly | rejected | none | rejected | allowed | allowed |
| Paused | rejected | none | rejected | allowed only to resume | allowed |

If `GameStateMachine` is temporarily absent, the documented compatibility fallback is Exploration input plus Pause, while dialogue advance and UI routing remain rejected. This preserves early/legacy scene operability without allowing one Interact press to reach both narrative and exploration.

## Production consumers migrated

- `PlayerInputController`: subscribes to `InputService.Move`, `Jump`, and `Attack`; serialized `PlayerMotor2D_New` and `PlayerFieldAttackController` fields are unchanged. Its state check and `ForceStopHorizontal` remain defensive.
- `StoryInteractionController`: subscribes only to `ExplorationInteract`; the serialized legacy fallback-key fields remain for compatibility but direct polling is no longer active.
- `Game.Interaction.InteractionController`: subscribes to `ExplorationInteract`.
- `StoryEventRunner`: subscribes only to `DialogueAdvance` and rejects input when no event is running, a choice is waiting, or the state is not Dialogue/Cutscene. Existing dialogue-panel callbacks are unchanged.
- `MissionCompleteCutsceneController`: uses routed `DialogueAdvance` while playing in Cutscene. Its serialized key and `InputActionReference` are retained but it no longer enables, disables, or directly subscribes to that action.

Each migrated scene consumer rechecks the persistent installer in `Update`, unsubscribes symmetrically, and reconnects if bootstrap creation is late or the installer/service instance changes.

## Compatibility surfaces

Retained on `GameInputInstaller`: `Move`, `Jump`, `Attack`, `Parry`, `Interact`, and `Pause`. They are compatibility accessors over the same `InputService` events, not separately invoked event streams. Compatibility `Interact` maps only to `ExplorationInteract`; therefore it cannot also advance dialogue. Existing consumers such as Demo/Test `OverworldPlayerController`, DemoMission `RescueNpcActor`, and the currently unreferenced `FieldSkillCaster`/`InteractionDetector` continue to compile without duplicate logical emission.

Compatibility events removed from `InputService`: the ambiguous public `Interact` and `Pause` events. Repository call-site inspection found no remaining subscribers. They are replaced by `ExplorationInteract` / `DialogueAdvance` and `PauseRequested`; the same-named `GameInputInstaller.Interact` and `Pause` compatibility accessors remain for older consumers. Serialized compatibility fields removed: none.

## InputActionReference owner classification

| Owner | Classification | Serialized references found | Batch 2 disposition |
|---|---|---|---|
| `MissionCompleteCutsceneController` | Production | No scene/prefab component reference found | Migrated to `InputService`; retained `skipAction` and `skipKey` fields |
| `OverworldInputAdapter` | Unclear / Legacy candidate | No scene/prefab reference found | Preserved unchanged; not newly connected; independently enables actions and is deferred |
| `CombatFieldCallDebug` | Debug | `Assets/GAME/Scenes/Test.unity` | Preserved isolated F1/F2/F3 action callbacks |
| `CombatStartSmokeTest` | Debug | `Assets/GAME/Scenes/Test.unity` | Preserved isolated smoke-test action callback |

## PlayerInput owner classification

`OverworldInputBridge` is the only script declaring a Unity `PlayerInput` component field. It is a Legacy/unclear bridge with no scene or prefab references found and was preserved unchanged. No scene or prefab YAML under `Assets/GAME/Scenes` or `Assets/GAME/Prefabs` contained a serialized `PlayerInput.m_Actions` assignment. `PlayerInputUnity` is a legacy `IPlayerInput` implementation using the old Input API; despite its name it does not own a Unity Input System `PlayerInput` component. Production `PlayerInputController` consumes `InputService` and does not add or own a `PlayerInput` component.

## Direct polling

Migrated:

- `StoryInteractionController` legacy fallback Interact polling -> `ExplorationInteract`
- `MissionCompleteCutsceneController` keyboard/action polling -> routed `DialogueAdvance`

Deferred and preserved:

- debug/test: `CombatEntryPoint` F9/F10 editor shortcuts, `CombatTestRunner`, `CombatSkillDebugInvoker`, `InspirationDebugHotkey`, `StoryInteractionDebugHotkey`, and `VerticalSliceSceneValidator`
- save debug: `SaveManager` F5/F6
- Demo/Legacy: `DemoRescueNpcEndFlow`, `PlayerInputUnity`, and `StoryDialogueTrigger2D` optional debug E key
- existing explicit UI/test fallbacks: `TimedChoicePanel` number keys, `SearchableInteractable2D`, `ItemAcquisitionHUD`, and `OfficeMenuController` mouse polling

These were not routed into production `InputService`, because they are Debug/Demo/Legacy, have no matching existing production action, or remain explicit UI compatibility behavior. Debug F-key commands were not changed.

## Scene, prefab, and Inspector audit

Scene references inspected:

- `Dungeon 1`: persistent installer, `StoryEventRunner`, two serialized `StoryInteractionController` components, and active/inactive `PlayerInputController` objects
- `InGame`: installer, story runner, and story interaction controller
- `TitleScene`: installer
- `Demo`: installer plus Demo/Legacy overworld input consumers
- `Test`: installer, story consumers, Demo/Legacy overworld consumers, and isolated combat debug components
- `CombatTest`: direct debug test runner

No prefab reference to a modified component required rewiring. No Inspector change is required. In particular, existing motor, field-attack, fallback key, and cutscene skip fields are preserved. The two newly added repository assets require only their newly generated `.meta` files; no existing GUID changed.

The duplicated `StoryInteractionController` presence in Dungeon 1 is preserved because scene YAML changes are prohibited in this batch. The input layer emits only one logical `ExplorationInteract`, but both existing scene consumers can observe it; manual scene validation should confirm their registered interactable scopes do not overlap.

## Validation

Validation used an isolated copy because the main Unity project was already open. Authority was Unity `6000.2.6f2`, not `dotnet build`.

- Unity C# compilation: passed, zero errors.
- `GameStateOwnershipTests`: 6 passed, 0 failed.
- `InputOwnershipTests`: 18 passed, 0 failed.
- Combined requested EditMode run: 24 passed, 0 failed, 0 skipped.
- Missing-script / missing-reference inspection: source/YAML reference search completed; no changed serialized references and no missing-script signature attributable to this batch were found. Full live-scene Inspector validation was not performed.
- `git diff --check`: run after implementation; result recorded in the completion response.

Unity reported these six unique C# warnings:

- existing obsolete TMP word-wrapping API in `TimedChoiceDialoguePanel`
- existing unused `FieldEnemy.OnBattleRequested` event
- retained compatibility fields `StoryInteractionController.fallbackInteractKey` and `useLegacyFallbackKey`
- existing unused DemoMission `RescueNpcActor.interactKey`
- retained compatibility field `MissionCompleteCutsceneController.skipKey`

Unity also reported licensing refresh messages and the existing MCP-for-Unity shutdown message; neither blocked compilation or tests.

Manual Play Mode was not run in this environment. Use the procedure below before release.

## Manual Play Mode procedure (Dungeon 1)

1. Open `Assets/GAME/Scenes/Dungeon 1.unity` and enter Play Mode.
2. Confirm exactly one `GameInputInstaller`, `GameStateMachine`, and `GameFlowController` persists. Reload Dungeon 1 or return through Title and confirm no duplicate installer, callback log, or `MissingReferenceException` appears.
3. In Exploration, move left/right, jump, field-attack, and interact. Confirm each command executes once.
4. Hold movement and start dialogue. Confirm movement stops immediately. Press Interact once per node and confirm the NPC interaction does not restart and exactly one node advances.
5. Reach a choice. Press Interact and confirm it neither advances nor selects. Select with the UI button and confirm exactly one next node appears.
6. Hold movement and enter combat. Confirm immediate stop, no movement/field attack in CombatPlanning, UI-button plan submission still works, and exploration/dialogue commands remain blocked in CombatResolving.
7. Finish combat and close Reward with its UI button. Confirm movement remains zero until a new physical Move input.
8. Pause during Exploration, press Pause again, and confirm exact restoration to Exploration. Repeat from another policy-supported state and confirm exact previous-state restoration and the pause root routing.
9. Exercise a mission-complete cutscene in Cutscene state. Confirm one routed Interact skips only when allowed and the preserved serialized skip reference is not independently enabled.
10. Inspect Dungeon 1's two `StoryInteractionController` objects and verify their registered interactables do not produce two world interactions from one logical event.

## Known risks and retained compatibility

- Play Mode behavior, pause-root presentation, rebinding persistence, device switching, and actual scene reload were not manually exercised.
- `MissionCompleteCutsceneController` has no current serialized scene/prefab reference; its routed skip behavior is compile/test covered only indirectly and needs a suitable runtime mission configuration.
- Dungeon 1 retains two story interaction consumers and active/inactive player controllers exactly as serialized. No duplicate raw/logical command is emitted by the input layer, but overlapping consumer scopes require the manual check above.
- `OverworldInputAdapter` can still independently enable referenced actions if manually added to a scene. It has no discovered scene/prefab reference and remains an isolated Legacy/unclear path for a later cleanup batch.
- Debug, Demo, and Legacy input paths were intentionally retained and were not connected to new production events.
- Existing UI Button/EventSystem ownership for choice, combat planning, reward close, and other panels is unchanged.

## Recommended next batch

Runtime Refactor Batch 3 — Combat Entry Consolidation

This report does not begin or implement Batch 3.
