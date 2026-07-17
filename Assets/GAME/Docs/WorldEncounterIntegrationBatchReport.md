# Runtime Refactor Batch 7 — World Encounter Integration

## 1. Implementation result

Batch 7 establishes one production field-world lifecycle around each `CombatEntryPoint`: encounter reservation, accepted-session context capture, physical lock, camera/formation entry, completion-ID matched outcome application, Reward-held lock, and one Exploration restoration. Contact and field attack share the encounter owner. Combat Core retains its legacy field-outcome fallback only when no canonical adapter claimed the session. Runtime Refactor Batch 8 was not started.

## 2. Loaded instruction files

- Repository-root `AGENTS.md`
- No nested `AGENTS.md` or `AGENTS.override.md` files existed.

## 3. Initial branch and git status

- Branch: `main`
- The initial worktree contained the completed, uncommitted Batch 6 changes: eight modified runtime files and the new Batch 6 test/report assets.
- Those changes were preserved. Batch 6’s final result was confirmed from `Logs/Batch6FullEditModeFinal.xml`: 241 passed, 0 failed, 0 skipped.

## 4. Modified files

Batch 7 modified:

- `Assets/GAME/Scripts/Camera/CombatCameraController.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Adapters/CombatStartRequest.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatEntryPoint.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatDemoFlowController.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatEncounterGroup.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatEncounterTrigger2D.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatFieldLock.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatFormationManager.cs`
- `Assets/GAME/Scripts/Player/Runtime/PlayerFieldAttackController.cs`

`CombatEntryPoint.cs` was already modified by Batch 6; Batch 7 added only the world-integration seam and fallback ownership decision. `CombatStartRequest.cs` required mechanical UTF-8 normalization before patching; its public shape and metadata were preserved.

## 5. Added files

- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatEncounterRuntime.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatEncounterRuntime.cs.meta`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatWorldLifecycleAdapter.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatWorldLifecycleAdapter.cs.meta`
- `Assets/GAME/Tests/Editor/WorldEncounterIntegrationTests.cs`
- `Assets/GAME/Tests/Editor/WorldEncounterIntegrationTests.cs.meta`
- `Assets/GAME/Docs/WorldEncounterIntegrationBatchReport.md`
- `Assets/GAME/Docs/WorldEncounterIntegrationBatchReport.md.meta`

## 6. Deleted or moved files

None.

## 7. Previous world-lifecycle owners

| Path | Classification | Trigger | Previous responsibility/risk | Final disposition |
|---|---|---|---|---|
| `CombatEncounterTrigger2D` | Production | contact trigger | local armed/in-progress booleans and direct start request | Shared encounter lifecycle/reservation; remains detector/request builder |
| `PlayerFieldAttackController` | Production | routed field attack | local latches and direct start request; no shared contact reservation | Uses the same group/trigger owner and generation-safe recovery |
| `CombatEncounterGroup` | Production authored data | trigger/attack roster lookup | roster snapshot only | Authored roster plus non-serialized availability/reservation lifecycle |
| `CombatEntryPoint` | Production core boundary | `ExitCombat` | directly deactivated/destroyed defeated field enemies | Delegates when canonical world context claimed; retains isolated fallback |
| `CombatFieldLock` | Production utility, Test serialized | Demo controller/manual calls | configured disable/freeze lists; restored stale velocity | Passive idempotent utility accepting runtime targets and restoring zero velocity |
| `CombatCameraController` | Production presentation | Demo controller/director focus | combat framing API but no production lifecycle coordinator | Existing API coordinated by adapter; exact pre-mode or camera fallback restored |
| `CombatFormationManager` | Production presentation | independent start subscription | could move actors without a centralized restore snapshot | Passive applier under adapter; legacy subscription only without adapter |
| `CombatDemoFlowController` | Demo compatibility | combat/reward events | lock/camera/UI fallback | Skips world writes whenever canonical adapter exists |
| `FieldEnemyPatrolAI2D` | Production field AI | state event plus Update guard | stops motor outside Exploration | Preserved defensive state behavior; adapter also physically disables/restores known AI |
| `FieldEnemy` | Legacy/Test | collision/player hit | compatibility start path | Preserved; not promoted |
| Debug smoke/test runners | Debug/Test | keys/direct test code | direct bootstrap/session bypasses | Preserved and isolated |
| Legacy seamless/transition components | Legacy | old state/UI path | global-state presentation without session | Preserved; not connected to Dungeon 1 |

## 8. Final Production world-integration owner

`CombatWorldLifecycleAdapter` is the sole production world-lifecycle coordinator per `CombatEntryPoint`. It observes combat and GameState, but does not detect encounters, construct plans, grant rewards, show UI, own input, or write global state.

## 9. Reason for adding a new adapter

No existing component could own the complete lifecycle without mixing unrelated responsibilities. A trigger cannot coordinate camera and restoration, formation cannot own outcomes and encounter availability, `CombatFieldLock` must stay passive, and the Demo controller is not production-serialized. The adapter is therefore one narrow Integration component, not a manager or singleton. Existing scenes are supported by one runtime-added adapter on the entry-point GameObject; future explicit serialization is compatible.

## 10. Encounter runtime lifecycle

The non-serialized lifecycle is `Idle -> StartReserved -> ActiveCombat -> AwaitingPostCombat -> Cleared` for a fully cleared victory, or `RearmPending -> Idle` after Exploration plus player exit for non-clearing outcomes. Reservations and completion results are single-use. Cleared state does not rearm during the current runtime. Scene reload reconstructs authored state.

## 11. Contact/attack reservation policy

Contact and attack resolve the same `CombatEncounterGroup` or local trigger owner. They reserve before constructing/submitting the snapshot, commit only after an accepted session identity exists, and release on false return, empty roster, missing entry, or exception. Unity executes the reservation synchronously, so same-frame contact/attack noise yields one accepted owner while `CombatEntryPoint` remains final duplicate-start authority.

## 12. CombatEncounterGroup policy

Serialized enemy lists are preserved. When any non-null manual member exists, the manual list takes precedence even if auto-collection is enabled; active unique members are returned in a fresh list and invalid active authored members remain so entry preflight can reject atomically, with a warning once. Auto-collection is used only when the manual list is empty and includes active direct children that satisfy HP adaptation; helpers are excluded and warned once. Runtime reservation/result state never modifies the authored list or a ScriptableObject.

## 13. Single-enemy compatibility policy

An ungrouped `CombatEncounterTrigger2D` implements the same local runtime-owner contract. Contact and player attack resolve that trigger when available. Rejection returns it to Idle, victory can leave it Cleared, and defeat/escape/abort wait for Exploration and player exit before rearming. Existing enemy prefabs do not require a group component.

## 14. Field context snapshot contents

For every field-adapted ally and enemy, the Integration-only snapshot records field object, `CombatantId`, side, active states, world position, rotation, local scale, parent, sibling index, Rigidbody2D reference/simulated/velocity/angular velocity, collider references/enabled states, explicit known movement/AI behaviours, and encounter owner. It also stores the ID mapping, completion identity, cleared set, and encountered owners. Nothing is serialized or placed in `CombatSession`.

## 15. Completion identity matching

The snapshot uses Batch 6’s stable `CombatSession.CompletionId`. The accepted request carries a narrow non-serialized encounter-owner reference through entry normalization, including manual members outside the group hierarchy. Results with another non-empty completion ID are ignored and warned once. Duplicate results do nothing. Legacy empty IDs are accepted only against the already captured local context.

## 16. Field-lock ownership

The adapter owns when to lock/unlock; `CombatFieldLock` only applies requests. Repeated calls are idempotent. Dungeon 1 has no serialized lock, so the adapter adds one local runtime lock component to the entry object and supplies accepted actor targets. A Play Mode warning recommends explicit configuration without blocking combat.

## 17. AI lock policy

Known field-only components are explicitly locked: `PlayerMotor2D_New`, `PlayerInputController`, `FieldEnemyPatrolAI2D`, `FieldEnemyMotor2D`, legacy `OverworldEnemyAI`, and legacy `OverworldPlayerController` when present. Prior enabled states are retained. `Animator`, `CombatantAnimationDriver`, HP adapters, and presentation components are not disabled. GameState/Input remain the command-permission owners.

## 18. Physics lock policy

Accepted actor Rigidbody2D bodies are captured, velocity/angular velocity are zeroed, and simulation is disabled so bodies cannot drift while transform-based combat presentation continues. Unlock restores only the original simulated flag and always resumes at zero velocity. Stale chase, attack, fall, or patrol velocity is never restored.

## 19. Collider policy

Actor collider enabled states are captured and restored. The adapter does not globally disable triggers or change layers/Physics2D settings; encounter lifecycle guards suppress collision noise. Only colliders explicitly configured on `CombatFieldLock` are disabled. Cleared roots are excluded from collider/behaviour restoration.

## 20. Camera lifecycle

The existing controller enters once after snapshot/lock, remains available for director action focus, holds its result frame through Reward, and exits only on Exploration. It now captures the exploration-follow enabled state. When a follow component exists, its exact prior enabled state is restored; when it does not (Dungeon 1’s serialized case), the captured camera transform and orthographic size are restored. Missing camera remains non-blocking.

## 21. Formation policy

`CombatFormationManager` remains the formation algorithm and serialized settings owner. Under canonical integration it is a passive one-shot applier called after the context snapshot. Its legacy start-event subscription remains only when no adapter exists. Full rosters are iterated; helper objects never enter the combat session.

## 22. Transform restoration policy

On Exploration, surviving actors restore captured parent, sibling position, world position, rotation, and local scale once while bodies are still frozen, then the lock restores prior component/collider/simulation states. Defeated/cleared objects and destroyed references are skipped. Final animation and Reward occur before restoration.

## 23. Reward hold policy

`OnCombatEnded` applies field outcome and camera result hold but does not unlock. Reward and other non-Exploration states retain the lock. The authoritative transition to Exploration— including Batch 6’s missing-panel immediate fallback—restores once. The adapter never calls RewardService, RewardUIPanel, or reward-close flow APIs.

## 24. Field outcome ownership

The adapter matches result IDs against its captured `CombatantId -> field object` map and applies the configured entry-point deactivate/destroy policy once. The result is not rebuilt or modified. Unrelated enemies and helper objects are not discovered or cleared through scene searches.

## 25. CombatEntryPoint compatibility fallback

The serialized `deactivateDefeatedEnemies` and `destroyDefeatedEnemies` fields remain. Before publication, entry asks whether the canonical adapter owns the ending session. If yes, core skips its old field mutation and the adapter applies the published result. Without a claimed context, the existing direct session-based fallback runs. Both cannot execute for one completion.

## 26. Victory cleanup policy

Unique `DefeatedEnemyIds` map only to captured enemy adapters. Each mapped enemy is cleared once according to existing entry configuration. An encounter becomes Cleared only if no captured active/living member remains. Forced victory with survivors warns and becomes RearmPending.

## 27. Defeat restoration policy

No victory cleanup occurs. Objects, transforms, configured AI/colliders, camera, and physics restore on Exploration. HP is not changed; a zero-HP player returning to Exploration remains a known design risk.

## 28. Escape restoration policy

No victory cleanup occurs. The world remains locked through result/reward, restores on Exploration, and the encounter rearms only after overlap clears.

## 29. Abort restoration policy

Abort follows non-victory restoration/rearm and does not clear defeated IDs opportunistically. No checkpoint, resurrection, title routing, or reload behavior was added.

## 30. Trigger rearm policy

Rejected starts return to Idle. Accepted starts cannot reserve again. Cleared remains disarmed. RearmPending requires both observed Exploration and zero tracked player colliders. No frame delay or GameState write is used.

## 31. Player overlap policy

Player collider instance IDs are tracked as a set, so multiple colliders count as one presence set and individual exits cannot rearm prematurely. Collision callbacks remain enabled during combat; therefore physical exit is authoritative. Re-entry after all exits may reserve once.

## 32. PlayerFieldAttackController recovery

Each attack coroutine carries a generation. State departure or disable increments the generation, stops the coroutine, and clears `_attackRunning`; stale hit callbacks cannot submit. Exploration clears `_combatStarted`. Rejected requests release the owner and do not latch combat. Animation timing, hitbox, layers, cooldown, and request initiative/opening data are unchanged.

## 33. Scene reload and lifetime policy

Subscriptions are symmetrical. Ownership registry entries release on disable/destroy and destroyed references are pruned. Disable with an active context logs and releases physical locks/camera safely while retaining context for re-enable recovery. Scene destruction clears the snapshot and Unity references. A fresh session creates a fresh ID/context.

## 34. Demo/Test/Legacy compatibility

`CombatDemoFlowController` detects the canonical world adapter and does not duplicate lock, camera, or restoration. Its isolated fallback remains. Test-only `CombatFieldLock`, obsolete `FieldEnemy`, Debug direct bootstrap/session runners, tutorial adapters, and Legacy state-only components remain unmoved and unpromoted. Direct Debug bypasses are deferred to Batch 9.

## 35. Public API changes

Compatible additions only:

- New public component `CombatWorldLifecycleAdapter`
- `CombatCameraController` gains an internal combat-mode diagnostic
- `CombatFieldLock`, group, trigger, formation, request, and adapter gain internal runtime/test seams

Preserved public signatures include entry start/events/active references, request constructor/fields, group `GetActiveEnemies`, field lock `Lock`/`Unlock`, camera APIs, formation serialized fields, trigger/player serialized fields, and all Batch 6 reward APIs.

## 36. Inspector impact

No automated Inspector change occurred. Current exact production state:

- Dungeon 1 `CombatManager`: one entry, camera, formation, director/orchestrator; no serialized adapter or field lock.
- Dungeon 1 camera: target camera assigned; exploration follow and hidden main-camera fallback unassigned. Code now restores captured camera pose/size.
- Dungeon 1 formation: assigned to entry; placement disabled.
- Dungeon 1 enemies `Enemy_Ghost`, `(1)`, `(2)`: ungrouped triggers with explicit common entry, patrol/motor/animation; no group.
- Dungeon 1 `Player_new`: active field attack with entry; `Player` is inactive compatibility.
- Test: one configured `CombatFieldLock`, one ungrouped trigger, camera/formation/entry stack.
- Demo: one entry and camera but two enabled formation managers; canonical auto-binding refuses that ambiguity, so formation requires an explicit adapter assignment if Demo is expected to use placement.
- Reusable prefab: entry/camera/formation stack; no serialized world adapter.
- No serialized `CombatEncounterGroup` exists.

Runtime auto-binding provides the temporary production fallback. Recommended later Inspector standard: add one adapter and one field lock beside each dungeon entry, assign the existing entry/camera/formation explicitly, and use groups for multi-enemy templates. No assignment is required for current code to run.

## 37. Scene/prefab/SO/Input Actions/.meta impact

No `.unity`, `.prefab`, `.asset`, `.inputactions`, generated input code, or existing `.meta` file changed. Only new script/test/report metadata was added with unique GUIDs. Tags, layers, and Physics2D settings are unchanged. No script was moved or renamed, no Missing Script was introduced, and no second combat entry/encounter manager was added. Unity continues to report the pre-existing malformed `Assets/GAME/Scripts/Office/OfficeFlowController.cs.meta`; Batch 7 did not modify it.

## 38. Unity runtime compilation result

Unity 6000.2.6f2 compiled runtime scripts with zero C# errors.

## 39. Unity Editor/test compilation result

Unity 6000.2.6f2 compiled the Editor/test assembly with zero C# errors.

## 40. WorldEncounterIntegrationTests result

99 passed, 0 failed, 0 skipped.

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

340 passed, 0 failed, 0 skipped, 0 inconclusive.

## 49. Manual Dungeon 1 result

Not executed. Automated EditMode component and lifecycle tests do not validate physical collision timing, camera visuals, animation timing, real Input System commands, or the requested unsaved multi-enemy/defeat scenarios.

## 50. Demo/Test scene result

Serialized YAML ownership/reference audit completed without edits. Interactive Demo/Test Play Mode regression was not executed.

## 51. Unexecuted validation

- Dungeon 1 contact and player-first-hit Play Mode procedures
- Live formation/action movement and post-Reward visual restoration
- Live victory, multi-enemy, defeat, escape/abort, overlap, and Reward-hold procedures
- Runtime component disable/re-enable manipulation in live Play Mode
- Scene reload and Demo/Test interactive checks

## 52. Known risks

- Runtime-added adapter/lock fallback is code-safe and tested, but explicit dungeon-template Inspector wiring is clearer and should be standardized later.
- Dungeon 1 has three independent 1v1 encounters and no authored multi-enemy group.
- Demo has two enabled formation managers, so the adapter deliberately does not choose one automatically.
- Physical collision and coroutine event ordering remain unverified in Play Mode.
- A disabled/destroyed adapter can release locks safely but cannot guarantee transform restoration if it is never re-enabled before scene teardown.
- Legacy/direct Debug sessions that bypass entry do not receive canonical world context.
- Manual Play Mode camera, Animator, AI, and physics behavior is still unverified.

## 53. Defeat/zero-HP behavior deferred decision

Batch 7 preserves calculated HP. Returning a defeated zero-HP player to Exploration may require a later checkpoint, resurrection, title, or game-over policy; none was invented here.

## 54. Encounter persistence deferred to Batch 10

Encounter lifecycle, cleared state, overlap state, and field snapshots are runtime-only. Scene reload may reconstruct authored encounters. Save/load persistence is deferred to Batch 10.

## 55. DemoMission/Quest migration deferred to Batch 8

World integration does not own quest progress. Batch 6’s isolated DemoMission completion notification remains, and dialogue/quest migration is deferred to Batch 8.

## 56. Preserved compatibility

All serialized fields, entry/request APIs, trigger/group APIs, camera/formation APIs, UnityEvents, Demo/Test/Legacy files, reward/UI/turn/input ownership, field-outcome flags, GUIDs, and current dungeon authoring remain compatible. No scene component was removed or automatically serialized.

## 57. Confirmation that unrelated changes were preserved

All initial Batch 6 modifications and untracked assets remain present. No reset, revert, destructive Git operation, broad cleanup, commit, push, branch, or pull request occurred.

## 58. Exact recommended next batch

Runtime Refactor Batch 8 — Dialogue and Quest Integration

Batch 8 was not started.
