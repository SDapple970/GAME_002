# Runtime Refactor Batch 6 — Combat Completion and Reward Flow

## 1. Implementation result

Batch 6 establishes one idempotent production completion path: `CombatEntryPoint` publishes one stable completion snapshot, `CombatRewardUIBinder` claims it once, `RewardService` applies one canonical request, `RewardUIPanel` presents and closes locally once, and `GameFlowController` owns the Reward and Exploration transitions. Runtime Refactor Batch 7 was not started.

## 2. Loaded instruction files

- Repository root `AGENTS.md`
- No nested `AGENTS.md` or `AGENTS.override.md` files were present.

## 3. Initial branch and git status

- Branch: `main`
- Initial worktree: clean
- The existing Batch 5 full EditMode result was confirmed from `Logs/Batch5FullEditMode.xml`: 159 passed, 0 failed, 0 skipped.

## 4. Modified files

- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatEntryPoint.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatResultBuilder.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Model/CombatResult.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Model/CombatSession.cs`
- `Assets/GAME/Scripts/Combat/Runtime/UI/CombatRewardUIBinder.cs`
- `Assets/GAME/Scripts/Core/GameFlowController.cs`
- `Assets/GAME/Scripts/Reward/RewardService.cs`
- `Assets/GAME/Scripts/UI/RewardUIPanel.cs`

## 5. Added files

- `Assets/GAME/Tests/Editor/CombatCompletionRewardFlowTests.cs`
- `Assets/GAME/Tests/Editor/CombatCompletionRewardFlowTests.cs.meta`
- `Assets/GAME/Docs/CombatCompletionRewardFlowBatchReport.md`
- `Assets/GAME/Docs/CombatCompletionRewardFlowBatchReport.md.meta`

## 6. Deleted or moved files

None.

## 7. Previous combat-completion owners

| Path | Trigger | Classification | Previous responsibilities and risk | Final disposition |
|---|---|---|---|---|
| `CombatEntryPoint` | `ExitCombat` phase | Production | Built and multicast a result, but published before clearing active references and one throwing subscriber could block later subscribers | Retained as sole result construction/publication boundary; publication hardened |
| `CombatRewardUIBinder` | `OnCombatEnded` | Production | Granted, showed UI, requested Reward, handled close, and incremented DemoMission; hash-only duplicate guard and no duplicate-binder ownership | Retained as sole production coordinator with explicit lifecycle and ownership claim |
| `RewardService` | Binder, quest, mission, interaction callers | Production service | Applied channels; combat guard stored only a `HashSet` and could not return original duplicate diagnostics | Retained as sole application/idempotency owner; combat result ledger added |
| `RewardUIPanel` | Binder and field-message callers | Production renderer | Rendered local content and emitted close; repeated clicks and field coroutine overlap were not one-shot safe | Retained as local renderer; one-shot and field-message isolation added |
| `GameFlowController` | Binder and other feature requests | Production core | Entered Reward/Exploration through void compatibility APIs | Retained as global state-request owner; guarded bool APIs added |
| `CombatDemoFlowController` | Combat events in isolated demo/test use | Demo compatibility | Could provide fallback presentation where canonical routing is absent | Preserved; it does not own canonical production completion |
| `DemoMissionRuntime` | Binder compatibility notification | Legacy/Demo compatibility | Counted an encounter-level victory | Isolated notification now counts unique defeated enemy IDs only |
| `QuestCompletionFlow`, mission/interaction reward callers | Their own feature events | Production compatibility | Use RewardService but do not own combat completion | Inspected and left source-compatible |
| UI root router/controller | `GameState` | Production UI | Own global Reward root visibility | Unchanged; no completion component writes global roots |

One production owner remains for each critical operation: publication (`CombatEntryPoint`), combat request/show/close coordination (`CombatRewardUIBinder`), application (`RewardService`), state transition (`GameFlowController`), and root visibility (`UIScreenRouter`/`GameUIRootController`).

## 8. Final production completion owner

`CombatRewardUIBinder` is the serialized production completion coordinator. It is not a reward calculator or global UI router.

## 9. CombatResult publication sequence

On the first accepted `ExitCombat`, `CombatEntryPoint` captures the ending session/state machine, unsubscribes phase and presentation callbacks, applies field outcome once, resolves the end reason, builds one result, verifies its completion identity, clears active references, and invokes a snapshot of `OnCombatEnded` subscribers individually. Subscriber exceptions are logged with context and do not prevent later subscribers.

## 10. Publication-time ActiveSession policy

`ActiveSession` and `ActiveStateMachine` are null before completion subscribers run. Subscribers receive the immutable completion snapshot and cannot mistake an ended session for active combat.

## 11. Completion identity design

Each `CombatSession` creates one non-serialized GUID-format runtime `CompletionId` in its constructor. The value is stable for that session, differs across sessions, and is copied by `CombatResultBuilder` into `CombatResult.CompletionId`.

## 12. Legacy identity fallback

Manually created legacy/test results without `CompletionId` use the existing runtime result-object hash fallback. Active combat is not serialized, and completion identity persistence is not introduced here.

## 13. Binder completion lifecycle

The binder uses `Idle -> Processing -> AwaitingClose -> Completed`. It retains only the active and most recently completed IDs. The same ID cannot grant, show, enter Reward, close, or notify DemoMission twice. A different result received while awaiting close is rejected and logged. Re-enable recovery restores subscriptions and lost panel content without regranting or re-entering Reward.

## 14. Duplicate binder policy

An internal static ownership registry claims one binder per `CombatEntryPoint` instance. A duplicate names both objects in one configuration warning and does not subscribe. Ownership is released on disable/destroy, destroyed Unity references are pruned, and an internal test-reset seam prevents test leakage. No manager or extra MonoBehaviour was added.

## 15. Reward request construction owner

`RewardService.CreateCombatRewardRequest` is the single conversion owner. The binder calls it rather than duplicating reward rules. `CombatResult.CompletionId` is the preferred combat `SourceId`; legacy results use the compatibility fallback.

## 16. Outcome eligibility policy

A populated `CombatEndReason` is authoritative. Victory is eligible for configured result gold/EXP/items. Defeat, Escape, and Abort produce zero victory rewards. Only legacy results with `EndReason.None` fall back to `IsWin`.

## 17. RewardService idempotency design

`RewardService` stores a runtime `combat SourceId -> RewardGrantResult` ledger. The first accepted source is applied once and recorded. A duplicate performs no mutations and returns the recorded diagnostic values with `DuplicateBlocked = true`. Different source IDs remain independent. Empty combat source IDs use an explicit legacy key.

## 18. Failed/partial grant consumption policy

One accepted combat request is consumed after its single application attempt, including missing-service and partial-channel outcomes. There is no automatic retry because retrying could duplicate a channel that already succeeded. No transactional rollback is claimed.

## 19. Currency behavior

Gold is clamped to zero and applied through `CurrencyWallet` at most once. Missing or throwing wallet access reports zero applied gold and logs once/contextually.

## 20. Inventory behavior

Items require a non-empty ID and positive clamped count and are applied through `InventoryService` at most once. Missing or throwing inventory access reports zero applied items.

## 21. EXP compatibility behavior

The existing compatibility behavior is retained: accepted EXP is reported/logged in the grant result, but no `CharacterProgressionService`, character mutation, persistence, or rollback exists. Batch 6 does not claim actual progression application.

## 22. Presentation order

The binder claims the result, sends one DemoMission compatibility notification, constructs and grants one request, calls `RewardUIPanel.Show` while the global root may still be inactive, then requests `GameState.Reward` and waits for close. This prevents a visible empty reward frame without delays.

## 23. RewardUIPanel lifecycle

`Show` resets local state. A presentation can emit `OnClosed` once; rapid clicks, close after hide, and repeated hide are harmless. Enable/disable listener binding remains symmetrical. A later new presentation can open and close normally. Runtime rows are destroyed normally in Play Mode and immediately in EditMode validation.

## 24. Field reward isolation

A combat result stops and supersedes the active field-message routine and clears field-only text. Field messages are rejected while a combat result is active. Field timeout never emits combat close and cannot hide a combat result. Field messages work again after combat presentation closes.

## 25. Missing RewardService behavior

The binder logs once, uses `RewardGrantResult.Empty`, still prepares/presents the result, enters Reward, and permits deterministic close/restoration. It does not fake currency or inventory mutation and does not retry an already consumed completion if a service appears later. This intentionally carries a lost-reward risk.

## 26. Missing RewardUIPanel behavior

The request is processed once. The binder logs once, enters Reward once, then immediately completes the close fallback so the state does not remain stuck. When `restoreExplorationAfterRewardClosed` is false, it marks completion finished without inventing a destination.

## 27. Missing GameFlowController behavior

The binder logs once and does not throw or mutate UI roots directly. Presentation may remain local if a panel exists; without the flow owner no global transition is fabricated.

## 28. Reward close behavior

Only the binder accepts the panel close for its `AwaitingClose` completion. Duplicate close notifications are ignored. `RewardUIPanel` owns only the local notification.

## 29. Post-close state policy

`GameFlowController.TryHandleRewardClosed` accepts close only while currently in Reward and requests Exploration once. Existing void APIs remain and delegate to guarded methods. The current default destination remains Exploration.

## 30. Victory behavior

One canonical request grants current configured result rewards once, the victory presentation is shown once, close is accepted once, and Exploration is requested once.

## 31. Defeat behavior

No victory reward is granted. Existing defeat-compatible content is shown once and the compatibility close policy returns to Exploration once.

## 32. Escape behavior

No victory reward is granted. Existing non-victory content is shown once and close returns to Exploration once.

## 33. Abort behavior

No victory reward is granted. Existing non-victory content is shown once and close returns to Exploration once. No title/game-over/reload policy was invented.

## 34. DemoMission defeated-enemy compatibility

The serialized `countEnemyDefeatOnVictory` field is retained. One accepted authoritative victory invokes a clearly isolated compatibility method that registers once per unique `DefeatedEnemyIds` value. Duplicate IDs, duplicate completion events, non-victory outcomes, and empty lists do not invent counts.

## 35. Quest/Mission compatibility retained

`QuestCompletionFlow`, `QuestManager`, `MissionCompletionController`, `DaySettlementFlow`, interaction rewards, and `RewardApplier` were inspected but not refactored. Existing RewardService APIs and quest/mission duplicate policies remain source-compatible. They do not route through the combat binder.

## 36. Public API changes

Compatible additions only:

- `CombatSession.CompletionId` getter
- `CombatResult.CompletionId` getter with internal assignment
- `GameFlowController.TryHandleCombatResult`
- `GameFlowController.TryHandleRewardClosed`
- `RewardUIPanel.IsOpen`
- `RewardUIPanel.HasActiveCombatResult`

Existing events, constructors, public fields, void flow APIs, RewardService APIs, panel Show/Hide/OnClosed APIs, and serialized binder fields remain.

## 37. Inspector impact

No Inspector changes are required. Dungeon 1 has one binder at `UI/Combat/CombatManager` with explicit entry point, reward panel, and service references. Its reward panel is at `UI/Canvas/RewardUI` and has a close button assignment. The uninstantiated `DungeonCombatRuntime.prefab` contains a compatibility binder with null panel/service auto-bind fields. No scene contains duplicate production binders. The Test scene contains two enabled legacy/compatibility reward panels without close-button assignments; it is not the Dungeon 1 production route. No serialized components were deleted.

## 38. Scene/prefab/SO/Input Actions/.meta impact

No `.unity`, `.prefab`, `.asset`, `.inputactions`, generated input code, or existing `.meta` file was modified. New test/report metadata only was added. Unity continued to report the pre-existing malformed `Assets/GAME/Scripts/Office/OfficeFlowController.cs.meta`; Batch 6 did not modify it. No missing script or second reward/completion manager was introduced.

## 39. Unity runtime compilation result

Unity 6000.2.6f2 compiled runtime scripts with zero C# errors.

## 40. Unity Editor/test compilation result

Unity 6000.2.6f2 compiled the Editor/test assembly with zero C# errors.

## 41. CombatCompletionRewardFlowTests result

82 passed, 0 failed, 0 skipped.

## 42. CombatUIRoutingTests result

54 passed, 0 failed, 0 skipped.

## 43. CombatSessionTurnFlowTests result

44 passed, 0 failed, 0 skipped.

## 44. CombatEntryConsolidationTests result

35 passed, 0 failed, 0 skipped.

## 45. CombatFoundationTests result

2 passed, 0 failed, 0 skipped.

## 46. GameStateOwnershipTests result

6 passed, 0 failed, 0 skipped.

## 47. InputOwnershipTests result

18 passed, 0 failed, 0 skipped.

## 48. Full EditMode result

241 passed, 0 failed, 0 skipped, 0 inconclusive.

## 49. Manual Dungeon 1 result

Not executed in this batch run. Automated component lifecycle and EditMode coverage does not replace the requested Dungeon 1 visual, input, camera, coroutine timing, or temporary missing-component checks.

## 50. Unexecuted validation

- Dungeon 1 victory, rapid close, multi-enemy, defeat, missing-service/panel, field-message overlap, binder recovery/duplication, and scene-reload Play Mode procedures
- Demo/Test scene interactive regression
- Temporary Inspector changes were not made or saved

## 51. Known risks

- Combat grant idempotency is runtime-only and not transactional or persisted.
- A consumed request with an unavailable/partially failing service is not retried, so its unapplied channel may be lost.
- Actual character EXP application remains absent.
- The Test scene's duplicate legacy reward panels and missing close assignments warrant later Inspector cleanup if that scene becomes production-facing.
- Defeat/Escape/Abort still use existing defeat-compatible content and Exploration restoration pending explicit design.
- Manual Play Mode behavior remains unverified in this run.

## 52. Reward idempotency persistence deferred to Batch 10

The combat grant ledger and completion IDs intentionally last only for the current runtime session. Save/load persistence and transactional recovery are deferred to Batch 10.

## 53. DemoMission migration deferred to Batch 8

The isolated unique-enemy compatibility notification remains in `CombatRewardUIBinder`. Its removal/migration to the authoritative quest path is deferred to Batch 8.

## 54. Preserved Debug/Demo/Test/Legacy compatibility

`CombatDemoFlowController`, DemoMission, quest/mission reward callers, interaction rewards, `RewardApplier`, existing serialized fields, UnityEvents, and panel overloads remain. Canonical production ownership was strengthened without deleting these paths.

## 55. Confirmation that unrelated changes were preserved

The worktree was initially clean. No unrelated source or serialized asset changes were introduced, reset, reformatted broadly, or discarded.

## 56. Exact recommended next batch

Runtime Refactor Batch 7 — World Encounter Integration

Batch 7 was not started.
