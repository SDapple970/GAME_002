# Runtime Refactor Batch 8 — Dialogue and Quest Integration Report

## 1. Implementation result

Batch 8 establishes `StoryEventRunner` as the sole Production story-event executor, `GameFlowController` as the narrative `GameState` transition owner, `QuestRuntime` as the canonical Production quest state owner, `QuestObjectiveTracker` as the single channel-delivery adapter per runtime, and `QuestCompletionFlow` as the sole quest-completion side-effect owner. DemoMission and Mission APIs remain compatibility paths. Batch 9 work was not started.

## 2. Loaded instruction files

- Repository root `AGENTS.md` was loaded completely.
- No nested `AGENTS.md` or `AGENTS.override.md` applied to the inspected or changed files.

The task prompt and all eleven required context documents were read before edits. Documents were treated as context; code, serialized assets, and Unity results remained authoritative.

## 3. Initial branch and git status

- Branch: `main`, tracking `origin/main`.
- Initial status: clean; no modified or untracked files.
- The requested preservation check therefore found no pre-existing Batch 6, Batch 7, or unrelated local changes to merge around.

## 4. Modified files

- `Assets/GAME/Scripts/Combat/Runtime/UI/CombatRewardUIBinder.cs`
- `Assets/GAME/Scripts/DemoMission/Runtime/DemoMissionRuntime.cs`
- `Assets/GAME/Scripts/DemoMission/Runtime/MissionCompletionController.cs`
- `Assets/GAME/Scripts/DemoMission/Runtime/RescueNpcActor.cs`
- `Assets/GAME/Scripts/Quest/QuestCompletionFlow.cs`
- `Assets/GAME/Scripts/Quest/QuestEvent.cs`
- `Assets/GAME/Scripts/Quest/QuestObjectiveDefinition.cs`
- `Assets/GAME/Scripts/Quest/QuestObjectiveTracker.cs`
- `Assets/GAME/Scripts/Quest/QuestRuntime.cs`
- `Assets/GAME/Scripts/Quest/QuestTrackerUI.cs`
- `Assets/GAME/Scripts/Story/Runtime/Data/StoryChoice.cs`
- `Assets/GAME/Scripts/Story/Runtime/Data/StoryEffect.cs`
- `Assets/GAME/Scripts/Story/Runtime/StoryEventRunner.cs`
- `Assets/GAME/Scripts/Story/Runtime/StoryEventTrigger2D.cs`
- `Assets/GAME/Scripts/Story/Runtime/StoryInteractable2D.cs`
- `Assets/GAME/Scripts/Story/Runtime/StoryProgressManager.cs`
- `Assets/GAME/Scripts/Story/Runtime/UI/DialoguePanel.cs`
- `Assets/GAME/Scripts/Story/Runtime/UI/StoryDialogueHUD.cs`
- `Assets/GAME/Scripts/Story/Runtime/UI/TimedChoicePanel.cs`

## 5. Added files

- `Assets/GAME/Scripts/Quest/QuestStatus.cs`
- `Assets/GAME/Tests/Editor/DialogueQuestIntegrationTests.cs`
- `Assets/GAME/Docs/DialogueQuestIntegrationBatchReport.md`
- Unity `.meta` files for new Unity-visible files.

## 6. Deleted or moved files

None.

## 7. Previous narrative owners

| Path/type | Trigger and state/UI/effects | Classification | Competition risk and disposition |
|---|---|---|---|
| `StoryInteractionController` / `StoryInteractable2D` / `StoryEventTrigger2D` | Field interaction or trigger; forwarded definitions to a runner | Production adapter | Retained; now records one-shot use only after `TryStartEvent` succeeds |
| `StoryEventRunner` | Story start, node traversal, dialogue/choice presentation, story effects, direct state fallback | Production but insufficiently guarded | Promoted to the only guarded Production executor; direct Production state fallback removed |
| `DialoguePanel`, `StoryDialogueHUD`, `TimedChoicePanel` | Local rendering, buttons, timeout | Production/compatibility presentation | Retained as renderers; cannot own global state, quests, or rewards |
| `DialogueRunner`, `DialogueUIPanel` | Separate legacy dialogue traversal/UI | Legacy | Preserved and not connected to Production; no serialized scene/prefab references were found |
| tutorial/cutscene/story trigger components | Authored or test entry paths | Demo/Test/Unclear by scene | Preserved; Production execution still enters through `StoryEventRunner` |
| `StoryInteractionDebugHotkey` and debug publishers | Debug keyboard shortcuts | Debug | Preserved outside Production input ownership; Batch 9 candidate |

## 8. Final Production story-event owner

`StoryEventRunner` is the single Production owner. A static active-run claim prevents two enabled runners from running concurrently. One runner owns one event, and rejected starts do not consume field interactions or triggers. Static ownership is released on completion, disable, destruction, and test reset.

## 9. Narrative runtime lifecycle

The non-serialized lifecycle is `Idle -> Starting -> ShowingDialogue -> WaitingForChoice -> Ending -> Completed`, with `Cancelled` for disable/destroy teardown. Each run gets a monotonically increasing generation. Node presentation gets its own token; stale advance, choice, and timeout callbacks are rejected. Effects, completion flags, event-completion notification, and cleanup are each claimed once. `EndEvent` is idempotent.

Cancellation policy: disabling or destroying the active runner clears only presentation owned by that runner, releases static ownership and input subscriptions, marks the run cancelled, and does not publish normal story completion or overwrite a newer/higher-priority state.

## 10. Narrative GameState policy

Normal Production stories start only from `Exploration`; authored cutscene starts may begin from `Cutscene`. Starts are rejected in combat transition/planning/resolving, reward, loading, paused, title, dialogue, and choice states. State requests go through `GameFlowController`; a missing Production flow controller warns and rejects safely.

The runner records the last state it successfully owned. It returns to `Exploration` only if the current state still belongs to that run. Delayed completion/cancellation cannot overwrite combat, reward, loading, title, paused, or another higher-priority flow.

## 11. Dialogue presenter policy

An explicitly valid `StoryDialogueHUD` is preferred. `DialoguePanel` is the compatibility fallback. Only one presenter is bound and shown for a line; the other is not cleared or controlled. Inactive candidates may be resolved once during binding, without per-frame search. Multiple viable auto-bind candidates log one clear ambiguity warning. `TimedChoicePanel` retains its authored local compatibility role and existing serialized reference.

Serialized audit risk: `Dungeon 1/StorySystem` is inactive and its runner has neither a `DialoguePanel` nor `StoryDialogueHUD` reference. The separate active `Systems` interaction controller has no runner. No YAML was changed, so this remains a required Inspector repair before the Dungeon 1 manual scenario can work.

## 12. Dialogue input policy

`InputService.DialogueAdvance` remains the canonical Production command. Next-button callbacks remain supported. A frame/token gate prevents input and a button click from advancing twice. Advance is rejected while waiting for a choice and outside the run-owned dialogue/cutscene state. Subscriptions are symmetric and late `GameInputInstaller` creation is recovered through its event rather than broad `Update` polling. Generated Input Actions and the input asset were untouched.

## 13. Choice and timeout policy

Entering an authored choice requests `Choice` once. The runner filters invalid choices, binds only the current token, and atomically claims either selection or timeout. A click/timeout race therefore produces one branch and one effect set. Stale panel listeners and timeout coroutines are cleared or token-rejected. With no valid option, the documented fallback ends the event safely. A continuing branch requests `Dialogue` once and does not restore `Exploration` early. Existing displayed-choice and timeout presentation behavior was preserved.

## 14. Story progress ownership

`StoryProgressManager` remains the owner of completed story-event flags and narrative conditions/effects. It does not own quest counters, quest rewards, or `QuestDefinitionSO` progress. The only lifecycle change is test-safe `DontDestroyOnLoad` behavior outside Play Mode.

## 15. Previous quest models

| File/type | Trigger and runtime/UI/reward role | Classification | Competition risk and final disposition |
|---|---|---|---|
| `QuestRuntime` | Definitions, objective progress, save contract, completion events | Production | Retained and made authoritative with explicit status and canonical APIs |
| `QuestObjectiveTracker` | Static `QuestEventChannel` subscriber | Production adapter | Retained; now one claimant per `QuestRuntime` |
| `QuestCompletionFlow` | Reward, settlement, optional Reward state | Production integration | Retained as sole completion side-effect owner with deferral and duplicate guards |
| `QuestTrackerUI` | Previously Mission-oriented HUD presentation | Compatibility UI | Extended to prefer canonical `QuestRuntime`; legacy fallback retained |
| `DemoMissionRuntime` | Local counters, events, bridge, save mirror | Compatibility/Demo | Canonical bridge now publishes/derives from `QuestRuntime`; isolated fallback retained |
| `MissionObjectiveTracker`, `MissionCompletionController` | DemoMission event forwarding/completion | Compatibility | Preserved; completion controller exits when canonical runtime is authoritative |
| `MissionManager`, `MissionDefinitionSO` | Older mission state/API | Legacy | Public overloads retained; no longer allowed to overwrite canonical quest state |
| debug/test quest publishers | Direct test/debug events | Debug/Test | Preserved and not promoted; Batch 9 candidate |

## 16. Final Production quest owner

There is one `QuestRuntime` class and no `QuestManager` or second Production quest model. It owns activation/status, objective state, definition lookup, completion state, duplicate-event ledgers, progress events, and existing save capture/restore. It owns no story traversal, dialogue UI, reward grant, combat calculation, or `GameState` routing.

## 17. Quest activation/status policy

New non-serialized status is `Inactive`, `Active`, or `Completed`. Registered authored definitions begin inactive. `StartQuest(QuestDefinitionSO)` activates once; an active start is idempotent and a completed quest does not restart. Production events affect active quests only. `ResetQuestProgress` explicitly clears progress/deduplication and reactivates the quest. `ConfigureCompatibilityQuest` explicitly activates the current DemoMission compatibility quest.

Restore policy, without DTO changes: saved completed entries restore as `Completed`; incomplete saved entries restore as `Active`; registered definitions absent from save remain `Inactive`. Restore emits neither completion nor reward-driving events. No `QuestDefinitionSO` assets currently exist in the repository, so no existing authored asset depended on automatic activation.

## 18. Canonical QuestRuntime APIs

- `StartQuest(QuestDefinitionSO)` registers and activates canonical state.
- `ApplyEvent(QuestEvent)` validates active status, definition/objective identity, amount, duplicate identity, clamp, progress notification, and one completion.
- `CompleteObjective(questId, objectiveId)` completes the matching canonical objective and uses the same progress/completion path.
- `CompleteQuest(questId)` completes canonical state and raises one event without granting rewards.
- `StartQuest(MissionDefinitionSO)` remains a Legacy compatibility overload and forwards only to `MissionManager` where available.

Existing APIs were preserved.

## 19. QuestEvent identity design

`QuestEvent.EventId` is an optional constructor parameter, so existing constructor calls compile unchanged. Identity is runtime-only. Story identities include story event/run generation plus node or choice identity. Combat identities include completion source plus defeated enemy ID. NPC talk and rescue use stable one-shot identities owned by the actor instance/action.

## 20. QuestEvent duplicate policy

A non-empty ID is accepted once per quest. Different IDs apply independently. Empty legacy IDs retain prior compatibility behavior. Each quest uses a bounded FIFO/hash ledger of 256 IDs; reset clears it. The ledger is not serialized and persistence is explicitly deferred to Batch 10.

## 21. QuestObjectiveTracker ownership

One enabled tracker claims one target `QuestRuntime`; duplicates warn and do not subscribe. Disable/destroy releases ownership and channel subscription symmetrically. Re-enable can reclaim once. Missing runtime warns once. Static registry entries are cleaned defensively and an internal test reset seam supports scene-like teardown. Direct `QuestRuntime.ApplyEvent` remains valid for explicit adapters.

Serialized audit: `New_Dungeeon_Manual` contains one `QuestRuntime`, tracker, and completion flow. `Dungeon 1` contains no `QuestRuntime`, so its DemoMission bridge currently falls back rather than becoming canonical.

## 22. Objective matching policy

Quest ID is matched first, followed by event type and existing definition contract. A non-empty authored objective ID requires the same non-empty event objective ID. Unknown objectives never create state for definition-backed quests. Explicit compatibility objectives remain supported. Zero/negative Production amounts are rejected, positive progress is clamped, and events fire only on change. Completion is evaluated after change, optional objectives do not block it, and a definition with no required objective does not complete accidentally.

## 23. Story-to-quest integration

`StoryEffectType.PublishQuestEvent` was appended without reordering existing enum values. It reuses existing story-effect quest/mission ID, objective ID, integer amount, and the new compatible event-type field. No existing story asset was modified. The runner publishes through `QuestEventChannel`; it never mutates `QuestRuntime` or grants rewards. Accepted node/choice effects execute once, and missing quest infrastructure does not interrupt dialogue.

Future authoring requires selecting `PublishQuestEvent` and assigning quest ID, objective ID, event type, and positive amount on the relevant story node or choice effect.

## 24. Combat-kill quest integration

Combat ownership remains unchanged: `CombatEntryPoint` publishes results, `CombatRewardUIBinder` coordinates combat rewards, and `CombatWorldLifecycleAdapter` owns world outcome. The existing Batch 6 DemoMission adapter remains the quest-facing publisher. Each unique defeated enemy now gets `combat:<completion-source>:enemy:<enemy-id>`, so duplicate results deduplicate while multi-enemy victories remain distinct. No dependency was added to Combat Core or `RewardService`, and quest progress does not wait for reward UI close.

## 25. NPC-talk integration

`RescueNpcActor` publishes an authored talk event only after an accepted interaction completes, not on trigger entry. It publishes once with the actor's stable runtime one-shot identity. Existing collider and prompt behavior remains. Missing canonical quest runtime falls back without breaking interaction.

## 26. NPC-rescue integration

Rescue is claimed only after requirements pass and interaction is accepted. One stable rescue event ID prevents repeated progress. Canonical kill checks derive from `QuestRuntime` through the DemoMission adapter when available; fallback counters remain available. The actor does not grant rewards, write `GameState`, or directly open global roots.

Serialized audit risk: the `Dungeon 1` `RescueNpcActor` has no `npcDefinition`, `missionRuntime`, or prompt reference serialized. Auto-resolution may preserve fallback behavior, but the authored mapping must be checked in Inspector before claiming the manual rescue flow works.

## 27. DemoMission compatibility mode

With `bridgeToQuestRuntime` enabled and a runtime available, `DemoMissionRuntime` explicitly configures/activates its compatibility quest, publishes direct canonical events, derives public getters from `QuestRuntime`, mirrors progress locally only for compatibility/save display, and mirrors canonical completion into `OnMissionCompleted` once. It neither grants rewards nor changes state. Reset delegates to canonical reset.

## 28. DemoMission fallback mode

With bridging disabled or no runtime found, existing isolated local counters, completion events, and save capture remain. This is compatibility/Demo behavior, not a Production owner. `Dungeon 1` currently uses this fallback because no `QuestRuntime` is serialized there.

## 29. MissionManager compatibility

`MissionManager`, `MissionDefinitionSO`, and the public `StartQuest(MissionDefinitionSO)` overload remain compilable. Canonical `CompleteObjective` and `CompleteQuest` no longer bypass `QuestRuntime`. Mission completion compatibility is suppressed when DemoMission reports canonical authority. No Mission files were deleted or moved.

## 30. Quest completion deferral policy

`QuestCompletionFlow` claims each quest ID once and queues completions FIFO. Side effects run only in `Exploration` (or the existing no-state-machine compatibility environment). Combat, combat Reward, dialogue, choice, cutscene, loading, title, paused, and other unsafe states defer processing. `GameStateMachine` events trigger deterministic draining without polling. Disable/re-enable retains claims and cannot double-process pending entries.

## 31. Quest reward policy

Values remain sourced from `QuestDefinitionSO` or the existing serialized fallback. `RewardService` receives one request with stable source `quest:<quest-id>`. `enterRewardStateOnCompletion == false` grants and settles without entering Reward. When true, Reward is requested only from safe Exploration through `GameFlowController`.

No new quest reward panel was created. No verified compatible quest-reward presenter is wired in current scenes; enabling `enterRewardStateOnCompletion` can therefore expose an empty Reward-root design/Inspector risk. The currently inspected `New_Dungeeon_Manual` policy is false.

## 32. DaySettlement compatibility

`DaySettlementFlow` is notified once per claimed completion after reward processing. Missing settlement flow remains non-fatal. It does not become authoritative for quest status or reward calculation.

## 33. Quest HUD ownership

No `QuestHUD` class existed; the serialized compatibility component is `QuestTrackerUI`. It now subscribes to canonical start/progress/completion events, rebuilds from current active runtime state on enable, uses definition titles/objectives with safe fallbacks, and hides on completion according to existing behavior. It does not grant rewards or write state. Legacy `MissionManager` display remains only when no canonical active quest is available.

## 34. Save compatibility

`QuestRuntime` still implements the existing provider/consumer interfaces, and `QuestSaveData`/`DemoMissionSaveData` structures and IDs are unchanged. Restore does not emit completion, progress, or reward-driving events. Active dialogue/combat is not saved. Runtime event IDs are not persisted. This batch does not claim compatibility beyond the existing contracts.

## 35. Public API changes

Compatible additions only:

- `StoryEventRunner.TryStartEvent(...)`, `OnEventStarted`, and `OnEventCompleted`.
- `QuestEvent.EventId` and an optional constructor argument.
- `QuestStatus`; `QuestRuntime.OnQuestStarted`, `IsQuestActive`, `GetQuestStatus`, `TryGetDefinition`, and `TryGetFirstActiveQuestId`.
- DemoMission event-ID overloads and canonical-authority/query helpers.
- Read-only presenter readiness properties used for safe binding.

No public method was removed. Existing `StartEvent`, `Advance`, `SelectChoice`, `EndEvent`, quest overloads/events/save interfaces, DemoMission APIs, Mission APIs, serialized fields, and UnityEvents remain.

## 36. Inspector impact

Automatic Inspector changes: none. Required manual review:

| Scene/hierarchy | Component/field | Expected value | Reason/risk |
|---|---|---|---|
| `Dungeon 1/StorySystem` | `StoryEventRunner` presenter references and active state | One valid preferred HUD or fallback panel; active Production system | Current object is inactive and presenter refs are empty |
| `Dungeon 1/Systems` | `StoryInteractionController.storyEventRunner` | Canonical active runner | Current active adapter has no runner |
| `Dungeon 1` mission systems | `DemoMissionRuntime.questRuntime`, tracker/completion runtime refs | One canonical `QuestRuntime` if Production bridging is intended | Currently no runtime exists, so fallback is used |
| `Dungeon 1` rescue NPC | `RescueNpcActor` definition/runtime/prompt and authored IDs | Correct NPC definition, compatibility runtime, prompt, quest/objective IDs | Current serialized refs are missing |
| Relevant story node/choice assets | `StoryEffect` quest metadata | Authored only where a quest event is intended | No assets were auto-converted |
| Quest reward scene integration | compatible presenter with `enterRewardStateOnCompletion` | Existing presenter or keep policy false | No verified quest reward presenter exists |

## 37. Scene/prefab/SO/Input Actions/.meta impact

No `.unity`, `.prefab`, `.asset`, `.inputactions`, generated input wrapper, or existing `.meta` file changed. New scripts/tests/report have new Unity metadata only. No class/file was renamed or moved, no serialized enum was reordered, and no scene/prefab YAML was edited.

## 38. Unity runtime compilation result

Unity 6000.2.6f2 compiled the runtime assemblies with zero C# errors during the final targeted and full EditMode runs.

## 39. Unity Editor/test compilation result

Editor/test assemblies compiled with zero C# errors. The final full suite executed successfully. No new C# compiler warnings were reported. Separate pre-existing Unity import warnings remain: `OfficeFlowController.cs.meta` contains an invalid GUID, and `Assets/GAME/Scenes/Tests/Tests.asmdef` has no associated scripts. Neither file was modified by this batch.

## 40. DialogueQuestIntegrationTests result

57 passed, 0 failed, 0 skipped/inconclusive. The fixture uses deterministic combined cases to cover the requested ownership, lifecycle, state, presenter, choice race, quest status/matching/deduplication, tracker, DemoMission, completion, HUD, and save invariants.

## 41. WorldEncounterIntegrationTests result

99 passed, 0 failed, 0 skipped.

## 42. CombatCompletionRewardFlowTests result

82 passed, 0 failed, 0 skipped.

## 43. CombatUIRoutingTests result

54 passed, 0 failed, 0 skipped.

## 44. CombatSessionTurnFlowTests result

44 passed, 0 failed, 0 skipped.

## 45. CombatEntryConsolidationTests result

35 passed, 0 failed, 0 skipped.

## 46. CombatFoundationTests result

2 passed, 0 failed, 0 skipped.

## 47. GameStateOwnershipTests result

6 passed, 0 failed, 0 skipped.

## 48. InputOwnershipTests result

18 passed, 0 failed, 0 skipped.

## 49. Full EditMode result

397 passed, 0 failed, 0 inconclusive, 0 skipped. Final result file: `Logs/Batch8FullEditModeFinal.xml`. `git diff --check` passed. The complete changed-file list and diff were reviewed; only the intended C# files, test, report, and new metadata remain.

## 50. Manual Dungeon 1 result

Not executed. Batchmode compilation/EditMode tests cannot verify scene interaction, input blocking, rendered UI, physics triggers, coroutines in Play Mode, or reward overlap. Execute task sections A–J after the Inspector repairs in section 36, without saving temporary content.

## 51. Demo/Test result

Automated compatibility tests passed. Demo/Test scenes were not manually opened, so visual layout, collider behavior, and absence of runtime `MissingReferenceException` in those scenes remain unverified.

## 52. Unexecuted validation

- Dungeon 1 manual Play Mode scenarios A–J.
- Demo/Legacy manual scene scenario K.
- PlayMode test suite; no new PlayMode tests were added because the requested fixture targets EditMode and no scene YAML changes were authorized.
- Visual confirmation of quest Reward presentation.
- Inspector reconnection and Missing Script checks inside the Unity scene UI. Text/YAML inspection found no newly introduced missing-script reference, but that is not equivalent to opening every scene.

## 53. Known risks

- Dungeon 1 canonical story and quest components are currently incomplete/inactive or unassigned as detailed above.
- No authored `QuestDefinitionSO` asset exists, so Production quest authoring must be supplied before non-compatibility quests can run.
- New story quest effects require deliberate authoring; existing assets publish no speculative events.
- Quest Reward state lacks a verified compatible presenter when its serialized policy is enabled.
- Legacy/debug direct state and UI bypasses remain intentionally isolated for Batch 9.
- Runtime event-ID history is bounded and intentionally lost across save/reload.

## 54. Story/quest event-ledger persistence deferred to Batch 10

The 256-entry per-quest runtime deduplication ledger is not part of save data. Stable ledger persistence, migration, and replay policy are deferred to Batch 10.

## 55. DemoMission duplicate save state deferred to Batch 10

DemoMission compatibility mirrors and canonical QuestRuntime state can represent overlapping progress in existing save contracts. DTO consolidation/migration was not attempted and remains deferred to Batch 10.

## 56. Debug/Legacy migration deferred to Batch 9

Legacy `DialogueRunner`, `DialogueUIPanel`, `MissionManager`, debug hotkeys/publishers, Demo/Test components, and direct compatibility bypasses were preserved in place. No folder or namespace migration was started.

## 57. Preserved compatibility

Existing Story/Dialogue UI methods, StoryEventRunner APIs, TimedChoice local behavior, story flags, quest public APIs/events/save contracts, DemoMission public getters/events/save capture, Mission APIs, serialized fields, UnityEvents, asset GUIDs, Batch 1–7 state/input/combat/UI/reward/world ownership, and isolated Demo/Test behavior were retained.

## 58. Confirmation that unrelated changes were preserved

The worktree was clean at startup. No unrelated file was edited, reset, reverted, deleted, moved, reformatted, committed, pushed, or placed in a new branch. The final diff contains only Batch 8 implementation, tests, report, and new-file metadata.

## 59. Exact recommended next batch

Runtime Refactor Batch 9 — Debug and Legacy Separation

Batch 9 was not started.
