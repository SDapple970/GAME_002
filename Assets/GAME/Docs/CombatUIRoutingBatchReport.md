# Runtime Refactor Batch 5 — Combat UI Routing Report

## 1. Implementation result

Batch 5 established the intended two-level production routing structure:

`GameStateMachine -> UIScreenRouter -> GameUIRootController -> global UI roots`

`CombatEntryPoint / CombatStateMachine -> CombatUIRootController and CombatPlanningHUD -> combat-internal UI`

`UIScreenRouter` is the production global route decision owner. `GameUIRootController` remains a passive root applier. Combat planning now follows `CombatStateMachine.OnPhaseChanged` rather than polling in `Update`. Canonical combat routing no longer permits `CombatUIRootController` or `CombatDemoFlowController` to toggle global Field or Reward roots.

## 2. Loaded instruction files

- `AGENTS.md` at repository root
- No nested `AGENTS.md` or `AGENTS.override.md` files were present.

## 3. Initial branch and git status

- Branch: `main`
- Initial status: clean
- No branch, commit, push, or pull request was created.

## 4. Batch 4 validation confirmed before Batch 5 editing

Fresh Unity 6000.2.6f2 Test Runner XML confirmed and the Batch 4 report now records:

- Isolated `MissingPresenter_UsesImmediateFallbackOnce`: 1 passed, 0 failed, 0 skipped
- `CombatSessionTurnFlowTests`: 44 passed, 0 failed, 0 skipped
- Complete pre-Batch-5 EditMode suite: 105 passed, 0 failed, 0 skipped
- Unity runtime and Editor/test compilation: zero C# errors

## 5. Modified files

- `Assets/GAME/Docs/CombatSessionTurnFlowBatchReport.md`
- `Assets/GAME/Scripts/UI/UIScreenRouter.cs`
- `Assets/GAME/Scripts/UI/GameUIRootController.cs`
- `Assets/GAME/Scripts/Combat/Runtime/UI/CombatUIRootController.cs`
- `Assets/GAME/Scripts/Combat/Runtime/UI/CombatPlanningHUD.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatDemoFlowController.cs`

`RewardUIPanel`, `CombatRewardUIBinder`, `CombatEntryPoint`, `CombatStateMachine`, and `RuntimeBootstrapper` required no modification.

## 6. Added files

- `Assets/GAME/Tests/Editor/CombatUIRoutingTests.cs`
- `Assets/GAME/Tests/Editor/CombatUIRoutingTests.cs.meta`
- `Assets/GAME/Docs/CombatUIRoutingBatchReport.md`
- `Assets/GAME/Docs/CombatUIRoutingBatchReport.md.meta`

## 7. Deleted or moved files

None.

## 8. Previous UI visibility ownership map

| File / type | Visibility writes | Source | Class | Competition and action |
|---|---|---|---|---|
| `UIScreenRouter` | All eight global root requests | `GameStateMachine.OnStateChanged` | Production | Retained as global decision owner; corrected resolved-state-machine use and pause behavior. |
| `GameUIRootController` | All eight global root `SetActive` calls | Router requests | Production | Retained as passive applier; duplicate auto-bind candidates now warn instead of arbitrary selection. |
| `CombatUIRootController` | Combat HUD, planning, widgets, Field canvas, Reward canvas | Combat start/end | Production compatibility | Migrated to phase-driven internal ownership; legacy Field/Reward writes run only without canonical routing. |
| `CombatPlanningHUD` | Planning panel, dynamic skill/target buttons, messages | Combat start plus per-frame phase polling | Production | Removed normal polling; phase-driven entry/exit now owns planning content only. |
| `CombatDemoFlowController` | Combat canvas, planning HUD, reward panel | Combat start/end and reward close | Demo | Canonical routing detection neutralizes competing UI writes; isolated Demo fallback retained. |
| `CombatRewardUIBinder` | Calls `RewardUIPanel.Show`; requests Reward/Exploration | Combat result and reward close | Production compatibility | Retained unchanged as the Batch 5/6 bridge. |
| `RewardUIPanel` | Panel-local reward content root and rows | Binder, close button, field reward message | Production content renderer | Retained unchanged; it does not activate Combat or Field roots. |
| `OverworldHUDRoot` and child HUDs | Field-HUD child content | Feature-local calls | Production local | Retained; these are internal field content, not global route decisions. |
| Dialogue/Choice panels | Their own panel roots and content | Narrative runners | Production local | Retained; global eligibility is routed by game state. |
| `CombatUIDialogueBlocker` | Dialogue/timed-choice compatibility roots | Combat start | Production compatibility | Retained for compatibility; canonical state routing makes its hide operation redundant rather than authoritative. |
| `CombatWidgetManager`, `CombatantWidget`, combat log/inspiration HUDs | Widgets and local content | Combat events or local refresh | Production internal | Retained as combat content behavior. |
| `TitleSceneController` | Title-sequence local groups | Title sequence | Production local | Retained; does not replace the global router. |
| Mission, quest, cutscene, office, search, and Demo panels | Panel-local roots | Feature events | Production local / Demo / Legacy | Retained; no global combat-route ownership was added. |
| `SeamlessBattleManager` | Legacy combat UI root | Legacy battle flow | Legacy | Retained unchanged; no new production dependency added. |

One production owner now decides every configured global root: `UIScreenRouter`.

## 9. Final global UI route owner

`UIScreenRouter` exclusively converts `GameState` into global root requests. It consistently uses its resolved `GameStateMachine`, replaces stale state-machine subscriptions on scene recovery, applies the current route on enable, and re-applies idempotently after a scene load. `RuntimeBootstrapper` still creates only the existing router/controller types and does not introduce a parallel UI framework.

## 10. Global GameState routing matrix

| GameState | Content route | Pause |
|---|---|---|
| Boot | Loading | Hidden |
| Title | Title | Hidden |
| Loading | Loading | Hidden |
| Exploration | Field HUD | Hidden |
| Dialogue | Field HUD + Dialogue overlay | Hidden |
| Choice | Field HUD + Dialogue + Choice overlay | Hidden |
| CombatTransition | Combat | Hidden |
| CombatPlanning | Combat | Hidden |
| CombatResolving | Combat | Hidden |
| Reward | Reward | Hidden |
| Cutscene | Dialogue/cutscene presentation | Hidden |
| UIOnly | No speculative global root; compatibility behavior retained | Hidden |
| Paused | Exact `GameStateMachine.Previous` content route | Visible |

Field world objects and the world camera are not global UI roots and are not hidden by this matrix.

## 11. Pause overlay policy

Paused is an overlay. `UIScreenRouter` derives the underlying content from `GameStateMachine.Previous`; it does not store a second competing previous-state field. Exploration, combat planning, combat resolution, dialogue, choice, and reward all remain as the underlying route. Reapplication is idempotent and resume removes Pause while applying the restored state directly.

No pause root is currently serialized in Dungeon 1, Test, Demo, or InGame. Missing Pause remains null-safe and warning-once when requested; no arbitrary object is chosen.

## 12. Dialogue and Choice underlying-screen policy

Dungeon 1 serializes dialogue and choice panels under its main UI canvas as overlay presentation. The implemented policy preserves the Field HUD beneath Dialogue, and preserves Field plus Dialogue beneath Choice. This does not affect exploration input blocking, which remains owned by GameState/Input routing.

## 13. CombatTransition visibility policy

CombatTransition shows the global Combat root, hides Planning through combat-local phase handling, hides Reward, and does not manipulate world cameras or field GameObjects. Field HUD is hidden, while seamless field-world presentation remains outside UI routing.

## 14. Combat internal UI ownership

`CombatUIRootController` now:

- subscribes symmetrically to `CombatEntryPoint` and the active `CombatStateMachine`
- recovers the active session and phase on enable
- shows internal HUD/widgets for Planning, Resolution, and EndTurn
- shows Planning only in Planning
- hides internal interaction on EnterCombat and ExitCombat
- clears session/state-machine references on combat end
- avoids normal `Update` polling
- preserves serialized `overworldCanvas` and `rewardCanvas` fields strictly as non-canonical Demo/legacy fallback

Dungeon 1 has no serialized `CombatUIRootController`; its serialized `CombatPlanningHUD` independently consumes the same authoritative phase events, so planning still converges correctly without a scene edit. Test and Demo contain enabled canonical controllers plus disabled duplicates; no YAML cleanup was performed.

## 15. Planning panel visibility policy

- Planning: visible and rebuilt once per new `TurnIndex`
- Resolution and EndTurn: hidden; Confirm made non-interactable
- EnterCombat and ExitCombat: hidden
- Accepted submission: hidden immediately by the existing submission path
- Rejected submission: remains visible and eligible controls are refreshed
- Repeated phase notification: no duplicate rebuild for the same visible turn

## 16. Combat widget visibility policy

- Planning: visible
- Resolution: visible
- EndTurn: visible
- EnterCombat and ExitCombat: hidden

## 17. Event-order recovery policy

Global and local routing are independent idempotent projections of authoritative state. Global-state-before-combat-event and combat-event-before-global-state both converge. Enabling combat UI or planning HUD after combat start reads `CombatEntryPoint.ActiveSession`, `ActiveStateMachine`, and the current phase. Reward state may precede or follow content binding because routing controls the global Reward root while `RewardUIPanel.Show` binds panel-local content.

No frame delays were added.

## 18. Inactive-root and lifetime recovery

All compatibility searches include inactive objects and run only during binding/recovery, never per frame. Router and combat subscriptions are removed on disable. Scene-load recovery re-resolves the authoritative state machine/entry point and reapplies current state. Missing references warn once. Ambiguous type/name candidates are not selected silently.

A root that contains `GameUIRootController` is rejected as unsafe so routing cannot disable its own owner.

## 19. CombatPlanningHUD responsibility changes

`CombatPlanningHUD` retains `Bind`, `Show`, and `Hide`. `Show`/`Hide` now delegate to phase-compatible `EnterPlanning`/`ExitPlanning` behavior. It continues to own skill/target button construction, validation, draft construction, submission, selection, and button listener cleanup. It no longer uses `Update` to decide visibility and never toggles global Combat, Field, Reward, or Pause roots.

## 20. CombatDemoFlowController classification

Classification: Demo compatibility. Its script GUID has no scene or prefab YAML references in the audited assets.

## 21. Demo fallback behavior

When both canonical `UIScreenRouter` and `CombatUIRootController` exist, Demo flow retains camera and field-lock compatibility but does not toggle combat canvas, Planning HUD, or Reward panel. Without canonical routing it retains the original isolated Demo/Test UI fallback.

## 22. CombatRewardUIBinder boundary retained

Unchanged. It still receives `CombatResult`, grants through the current service, requests Reward/Exploration through `GameFlowController`, binds `RewardUIPanel`, and observes close. This boundary is intentionally deferred to Batch 6.

## 23. RewardUIPanel boundary retained

Unchanged. It remains a content renderer with idempotent `Hide`, content-row construction, close-button symmetry, and `OnClosed`. It does not activate Combat or Field roots. Existing pending-result protection prevents a field reward message from hiding an active combat-result reward.

## 24. Direct SetActive calls migrated

- Canonical `CombatUIRootController` Field and Reward writes are neutralized.
- Canonical `CombatDemoFlowController` combat canvas, planning, and reward writes are neutralized.
- Planning visibility moved from polling/start-event assumptions to phase application.

## 25. Direct SetActive calls retained

- `GameUIRootController`: canonical global root application
- `CombatUIRootController`: canonical combat-internal roots
- `CombatPlanningHUD`: planning panel and dynamic button content
- `RewardUIPanel`: reward content root and rows
- Dialogue/Choice panels: local narrative content
- Overworld HUD children, widgets, log, inspiration, labels, effects, mission/cutscene/Demo/Legacy local panels: local or compatibility behavior

## 26. Compatibility fields retained

All existing serialized fields remain, including `CombatUIRootController.overworldCanvas`, `rewardCanvas`, and every existing planning/Demo/reward field. New optional serialized diagnostic/binding fields default safely and auto-bind where uniquely resolvable.

## 27. Public API changes

Compatible additions only:

- `UIScreenRouter.ApplyCurrentRoute()`
- `CombatPlanningHUD.EnterPlanning(CombatTurn)`
- `CombatPlanningHUD.ExitPlanning()`

All required existing public APIs remain intact.

## 28. Inspector impact

No required Inspector changes. No serialized scene was edited. If desired in a later cleanup, assign the new optional `CombatUIRootController.planningHUD` reference explicitly; unique child auto-binding currently covers it.

Audited duplicate cleanup recommendations, not performed:

- `Test.unity`: duplicate `CombatUIRootController`, `CombatPlanningHUD`, and `RewardUIPanel` components; disabled compatibility copies should be reviewed in a dedicated serialized cleanup.
- `Demo.unity`: duplicate `CombatUIRootController` and `CombatPlanningHUD` components; disabled compatibility copies should be reviewed separately.
- `Dungeon 1.unity`: no serialized `CombatUIRootController`; add only through an explicitly approved Inspector batch if a dedicated widget-container owner becomes necessary.

## 29. Serialized asset impact

- Scenes: none
- Prefabs: none
- ScriptableObjects / `.asset`: none
- Input Actions: none
- Generated input code: none
- Existing `.meta` files: none
- Missing Script introduced: none indicated; no serialized assets changed

Only new `.meta` files for the new test and report were added. Existing GUIDs were preserved.

## 30. Unity runtime compilation result

Unity 6000.2.6f2 runtime assembly compiled with zero C# errors.

Pre-existing warnings remained, including obsolete TMP wrapping, unused fields/events, an empty test asmdef warning, and the pre-existing malformed `OfficeFlowController.cs.meta` GUID warning. No new compile error was introduced.

## 31. Unity Editor/test compilation result

Unity 6000.2.6f2 Editor/test assembly compiled with zero C# errors.

## 32. CombatUIRoutingTests result

- 54 passed
- 0 failed
- 0 skipped

## 33. CombatSessionTurnFlowTests result

- 44 passed
- 0 failed
- 0 skipped

## 34. CombatEntryConsolidationTests result

- 35 passed
- 0 failed
- 0 skipped

## 35. CombatFoundationTests result

- 2 passed
- 0 failed
- 0 skipped

## 36. GameStateOwnershipTests result

- 6 passed
- 0 failed
- 0 skipped

## 37. InputOwnershipTests result

- 18 passed
- 0 failed
- 0 skipped

## 38. Full EditMode result

- 159 total
- 159 passed
- 0 failed
- 0 skipped

`git diff --check`: passed.

## 39. Manual Dungeon 1 result

Not executed in this headless batch. The requested Dungeon 1 Play Mode sequence remains an explicit manual validation procedure; no manual result is claimed.

## 40. Demo/Test scene result

Serialized YAML references and controller modes were audited without modifying or opening/saving the scenes in Play Mode. Automated canonical and fallback mode tests passed. Manual Demo/Test Play Mode regression was not executed.

## 41. Unexecuted validation

- Dungeon 1 manual Play Mode walkthrough
- defeat flow manual observation
- disabled UI root manipulation in live Play Mode
- live scene reload observation
- manual Demo/Test scene walkthrough

## 42. Known risks

- Dungeon 1 has no serialized pause root, loading root, field HUD root, or `CombatUIRootController`; missing roots fail safely but cannot display absent content.
- Duplicate disabled UI components remain serialized in Test and Demo.
- `CombatUIDialogueBlocker` remains as a redundant compatibility hide path.
- Runtime auto-binding deliberately refuses ambiguous candidates; affected scenes require explicit Inspector assignment rather than arbitrary selection.
- Manual camera, animation, EventSystem, and scene-reload behavior was not exercised headlessly.

## 43. Remaining reward ownership conflicts for Batch 6

- `CombatRewardUIBinder` still combines result receipt, reward grant request, state request, content binding, and close observation.
- Field reward messages and combat-result rewards share `RewardUIPanel` compatibility content.
- Defeat presentation remains on the current general reward/result route.
- Reward close sequencing remains the current bridge rather than a fully consolidated combat-completion owner.

No reward values, source IDs, grants, or duplicate protection changed in Batch 5.

## 44. Preserved Debug/Demo/Legacy compatibility

No Debug, Demo, Test, or Legacy component was deleted or moved. Demo fallback, legacy `overworldCanvas`/`rewardCanvas` fields, public `Show`/`Hide` methods, UnityEvent callbacks, and all existing GUIDs remain.

## 45. Unrelated changes preserved

The worktree was initially clean. The final diff is limited to the listed Batch 4 report correction, Batch 5 runtime controllers, the new tests, and this report. No unrelated user work was reset, reformatted, or overwritten.

## 46. Exact recommended next batch

Runtime Refactor Batch 6 — Combat Completion and Reward Flow

Batch 6 was not started.
