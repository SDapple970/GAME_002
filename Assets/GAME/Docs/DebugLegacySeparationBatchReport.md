# Debug and Legacy Separation Batch Report

Date: 2026-07-20

## 1. Implementation result

Batch 9 isolates production-scene-capable debug automation behind `UNITY_EDITOR` lifecycle guards and neutralizes the serialized Legacy battle transition whenever the canonical `CombatEntryPoint` exists. Canonical owners from Batches 1–8 are unchanged. No mass deletion, move, namespace rewrite, assembly migration, or Batch 10 work was performed.

## 2. Loaded instruction files

- `AGENTS.md` at repository root. No nested `AGENTS.md` or `AGENTS.override.md` exists.

All fourteen documents named by the task were read as supporting references. Current code, YAML, metadata, compilation, and tests were used as authority.

## 3. Initial branch and git status

Branch: `main`, tracking `origin/main`.

The initial worktree contained modified Batch 8 Combat reward, DemoMission, Quest, and Story files plus untracked Batch 8 report, `QuestStatus`, and `DialogueQuestIntegrationTests` files/metas. They were preserved. No reset, revert, checkout, cleanup, commit, push, or branch operation was run.

## 4. Modified files

Batch 9 modified:

- `Assets/GAME/Scripts/Debugging/Combat/CombatAutoPlanner.cs`
- `Assets/GAME/Scripts/Debugging/Combat/CombatFieldCallDebug.cs`
- `Assets/GAME/Scripts/Debugging/Combat/CombatStartSmokeTest.cs`
- `Assets/GAME/Scripts/Debugging/StoryInteractionDebugHotkey.cs`
- `Assets/GAME/Scripts/Debugging/VerticalSliceSceneValidator.cs`
- `Assets/GAME/Scripts/UI/BattleTransitionController.cs`

## 5. Added files

- `Assets/GAME/Tests/Editor/DebugLegacySeparationTests.cs`
- `Assets/GAME/Tests/Editor/DebugLegacySeparationTests.cs.meta`
- `Assets/GAME/Docs/DebugLegacySeparationBatchReport.md`
- `Assets/GAME/Docs/DebugLegacySeparationBatchReport.md.meta`

## 6. Deleted files

None.

## 7. Moved files

None. Existing locations and namespaces were safer than an assembly/folder migration.

## 8. GUID-preservation result

No existing `.meta` changed and no file moved. New GUIDs are unique. Repository scan found zero duplicate GUIDs.

## 9. Complete classification map

All rows are in `Assembly-CSharp` unless marked Editor tests. “YAML” lists current serialized evidence; no YAML reference means no C# caller alone was treated as deletion proof.

| Path/type family | Namespace | References/YAML and responsibility | Classification | Production owner | Action/risk |
|---|---|---|---|---|---|
| Core `GameStateMachine`, `GameFlowController`, `SceneFlowController`, `RuntimeBootstrapper` | `Game.Core` | canonical state/bootstrap; production scenes | Production | same | retain; no ownership change |
| `GameInputInstaller`, `InputRouter`, `InputService` | global/`Game.Input` | canonical generated-action owner/router | Production | same | retain; Input Actions untouched |
| Combat entry/session/state/flow/resolver/director/world lifecycle | `Game.Combat.*` | canonical combat pipeline | Production | requested baseline | retain |
| Combat UI/reward binder and `RewardService` | `Game.Combat.UI`, `Game.UI`, `Game.Reward` | canonical local UI/reward pipeline | Production | requested baseline | retain |
| Story runner/interaction and Quest runtime/tracker/completion | `Game.Story`, `Game.Quest` | canonical narrative/quest; Batch 8 worktree | Production | requested baseline | retain; preserve Batch 8 changes |
| `CombatAutoPlanner` | `Game.Combat.Debugging` | no YAML; auto-submits through `CombatEntryPoint` | Debug | `CombatEntryPoint` | Editor lifecycle guard; direct calculation not duplicated |
| `CombatFieldCallDebug` | `Game.Combat.Core` | `Test/CombatFieldCallDebug`; direct InputAction refs, starts via entry | Debug | `GameInputInstaller`, `CombatEntryPoint` | Editor subscription guard; test scene retained |
| `CombatStartSmokeTest` | `Game.Combat.Core` | `Test/CombatStartSmoke`; direct bootstrap calculation smoke | Test | none for pure fixture | Editor Start/Update guard; bypass documented |
| `CombatTestRunner` | `Game.Combat.Core` | `CombatTest/CombatTest`; direct session/resolver fixture | Test | none for calculation fixture | retained unchanged; isolated test scene |
| `CombatSkillDebugInvoker` | `Game.Combat.Debugging` | `Test/CombatDebugInvoker`; direct `SkillRunner` | Debug/Test | canonical resolver for production | retained in test scene only |
| `InspirationDebugHotkey` | `Game.Combat.Core` | no YAML; G/H mutation | Debug | canonical combat session | retained; no production serialized reference |
| `StoryInteractionDebugHotkey` | `Game.Story.Interaction` | no YAML; direct E interaction | Debug | `InputService` + `StoryInteractionController` | Editor Update guard |
| `VerticalSliceSceneValidator` | `Game.Debugging` | `Demo/Systems`, `Dungeon 1/Systems`, enabled | Debug | none | Editor Start/F8 guard; production serialized component inert in builds |
| `CombatDemoFlowController` | `Game.Combat.Integration` | no YAML; local fallback UI/camera/lock | Demo compatibility | UI router/world lifecycle | retain; existing canonical-mode detection |
| Demo folder controllers/data | `Game.Demo*` | isolated Demo/title/objective flows | Demo | canonical subsystem when present | retain; no new production caller |
| `DemoMissionRuntime` | `Game.DemoMission.Runtime` | `Dungeon 1/Mission`; fallback/bridge | Production Compatibility Adapter | `QuestRuntime` | retain Batch 8 canonical bridge/fallback |
| `MissionObjectiveTracker`, `MissionCompletionController` | same | `Dungeon 1` mission objects | Production Compatibility Adapter | Quest tracker/completion | retain; canonical authority exits already present |
| `RescueNpcActor` | same | `Dungeon 1/RescueNpc` | Production Compatibility Adapter | story interaction/quest event | retain; serialized mapping risk |
| `DialogueRunner`, `DialogueUIPanel` | `Game.Story.Core/UI` | no current GAME YAML found | Legacy compatibility | `StoryEventRunner` and current presenters | retain APIs; removal needs UnityEvent/scene validation |
| `MissionManager`, `MissionDefinitionSO` | `Game.Mission` | no current YAML found; old mission API/assets possible | Legacy | `QuestRuntime` | retain APIs; no new production dependency |
| `FieldEnemy` | `Game.Battle` | `Test/Enemy_Angel`; old field engine plus canonical entry | Legacy/Test | encounter trigger/entry/world lifecycle | retain; no production scene reference |
| `BattleTransitionController` | `Game.Battle` | `InGame` and `Test`, object `BattleTrasitionController` | Legacy | `CombatEntryPoint` | canonical-mode early return added; fallback preserved |
| `SeamlessBattleManager`, `BattleTrigger2D`, request | `Game.Battle` | no manager YAML found; legacy event route | Legacy | `CombatEntryPoint` | retain; no new caller; removal needs scene/build validation |
| `OverworldInputAdapter`, old player stack/bridge | mixed | no adapter YAML found; old direct action ownership remains in compatibility code | Legacy/Unclear | input installer/service and current player stack | unchanged; require Inspector/build-scene proof before move/delete |
| old Demo/NonCombat UI and dialogue controllers | mixed | compatibility/local panels; reference evidence varies | Legacy/Unclear | UI router/current story presenters | unchanged; local `SetActive` not automatically global ownership |
| Editor tests | `Game.Tests.*` | `Assets/GAME/Tests/Editor` | Test | none | Editor assembly only |

## 10. Production dependency map

Allowed dependency direction remains Debug/Demo/Test/Legacy to canonical public APIs. Source and reflection tests verify Core does not name Debugging owners, Combat Runtime does not name debug combatants, `StoryEventRunner` does not name legacy `DialogueRunner`, Quest does not name `DemoMissionRuntime`, Runtime does not reference UnityEditor/NUnit assemblies, and bootstrap creates no Debug/Demo/Legacy services.

Known compatibility exception: `FieldEnemy` imports DemoMission, but it is classified Legacy/Test, serialized only in `Test`, and is not a production owner.

## 11. Production owners preserved

All owners listed in the task baseline remain unchanged. No public owner, entry point, state transition policy, calculation, reward value, quest rule, narrative content, or UI layout changed.

## 12. Debug paths retained

All useful debug components remain compiled and available in the Editor. Production-scene-capable automatic paths are guarded. Test-scene-only direct calculation utilities remain intact.

## 13. Test paths retained

`CombatStartSmokeTest`, `CombatTestRunner`, debug invokers, dummy combatants/factory, and all existing EditMode fixtures remain. Direct `CombatSession`/resolver calls occur in tests/debug or `CombatBootstrapper` production construction.

## 14. Demo paths retained

Demo controllers/data and `CombatDemoFlowController` remain. Canonical routing/world lifecycle take priority; fallback executes only when canonical owners are absent.

## 15. Legacy paths retained

Battle transition/trigger/manager, FieldEnemy, old input/player, DialogueRunner/UI, MissionManager/data, and older UI paths remain for compatibility. They receive no new production caller.

## 16. Unclear paths retained

Old player/input and NonCombat/Demo UI components with incomplete Inspector/UnityEvent evidence remain unmoved and unchanged. Reclassification requires opening every referencing scene/prefab and verifying build-scene and UnityEvent bindings.

## 17. Direct combat bypasses

Repository search found 8 `new CombatSession` results and 8 direct resolver results; production construction is confined to `CombatBootstrapper`, with the remainder in Debug/Test fixtures. Two `CombatBootstrapper.StartCombat` calls are smoke/test-oriented. Demo canonical flow does not construct a session. No second production `CombatResult` publisher was introduced.

## 18. Direct GameState writers

Search found 19 `TrySetState` calls across runtime/tests plus compatibility `SetState` calls. Canonical production writers from prior batches remain. `BattleTransitionController` now exits before state calls when `CombatEntryPoint` exists. Legacy/Demo fallbacks are retained and documented rather than deleted.

## 19. Input ownership conflicts

`GameInputInstaller` remains the sole `new GameInput()` owner. Debug input is not added to `InputService` or Input Actions. `CombatFieldCallDebug` subscriptions are Editor-only. Old action owners remain compatibility/unclear and are not production dependencies.

## 20. Global UI ownership conflicts

`UIScreenRouter` remains global route owner and `GameUIRootController` passive applier. `CombatDemoFlowController` already suppresses root/planning/reward writes in canonical routing. Legacy battle UI writes are bypassed when canonical entry exists.

## 21. Reward ownership conflicts

No reward code changed. Combat reward binder/service remain canonical; Demo flow only presents in fallback. No duplicate grant path was added.

## 22. Narrative ownership conflicts

`StoryEventRunner` remains canonical. Legacy `DialogueRunner`/`DialogueUIPanel` remain compiled with no current GAME YAML reference found; no production source dependency was added.

## 23. Quest ownership conflicts

`QuestRuntime` remains authoritative. Batch 8 bridge/claim logic in DemoMission and Mission compatibility was preserved. No quest asset or rule changed.

## 24. Runtime auto-creation audit

One production `RuntimeInitializeOnLoadMethod` exists on `RuntimeBootstrapper`. It creates only canonical Core/Input/Reward/UI services. It does not add Debug, Demo, MissionManager, SeamlessBattleManager, or other Legacy owners.

## 25. DontDestroyOnLoad audit

Search found 14 occurrences across runtime/tests. Canonical singleton persistence remains unchanged. No Debug component gained persistence or auto-creation. Legacy persistent types were retained; manual scene reload validation remains required.

## 26. Debug hotkey policy

| Keys/behavior | Component | Availability |
|---|---|---|
| F8 scene validation | `VerticalSliceSceneValidator` | Editor only |
| E forced story interaction | `StoryInteractionDebugHotkey` | Editor only |
| F1/F2/F3 combat start actions | `CombatFieldCallDebug` | Editor only |
| automatic combat planning | `CombatAutoPlanner` | Editor only |
| smoke-test step action | `CombatStartSmokeTest` | Editor only |
| 0–3 skill invocation, G/H inspiration, Space test stepping | test-scene/unserialized debug utilities | retained for Test/Editor; absent from production scene references |
| F9/F10 | no current audited production implementation found | unavailable |

No debug keys were added to Input Actions or `InputService`.

## 27. Editor-only guard policy

Only lifecycle code that starts automation, polls keys, or subscribes debug InputActions is guarded. Types and serialized fields remain compiled to prevent Missing Script references.

## 28. Development-build diagnostics policy

No Batch 9 diagnostic uses `DEVELOPMENT_BUILD`. The audited tools are Editor/Test utilities, not player-build diagnostics.

## 29. Assembly structure decision

No runtime GAME asmdef currently provides a safe production/debug split. Creating one would move the whole runtime across assembly boundaries and risk serialized/test/package references. Targeted guards plus Editor tests are lower risk.

## 30. asmdef changes

None. `Assets/GAME/Scenes/Tests/Tests.asmdef` still warns that it has no scripts; it is pre-existing and unchanged. No cycle was introduced.

## 31. File movements and reasons

None. Movement was unnecessary for runtime isolation and would add GUID/assembly risk.

## 32. Scene and prefab reference audit

Audited `Dungeon 1`, `InGame`, `TitleScene` (task “Title”), `Demo`, `Test`, `CombatTest`, all GAME prefabs, and relevant narrative/quest scene objects by script GUID. No scene/prefab YAML was edited.

## 33. Production scene Debug/Demo/Legacy references

| Asset / hierarchy | Component | Class | Enabled | Replacement/risk |
|---|---|---|---|---|
| `Dungeon 1/Systems` | `VerticalSliceSceneValidator` | Debug | yes | no gameplay replacement; now inert outside Editor |
| `Demo/Systems` | same | Debug | yes | same; optional Inspector removal |
| `InGame/BattleTrasitionController` | `BattleTransitionController` | Legacy | serialized | canonical `CombatEntryPoint`; code-neutralized when present |
| `Dungeon 1/Mission` | `DemoMissionRuntime` | Compatibility | serialized | `QuestRuntime`; fallback retained because canonical wiring incomplete |
| `Dungeon 1/MissionObjectiveTracker` | tracker | Compatibility | serialized | canonical quest tracker |
| `Dungeon 1/Completion Controller` | completion controller | Compatibility | serialized | `QuestCompletionFlow` |
| `Dungeon 1/RescueNpc` | rescue actor | Compatibility | serialized | canonical story/quest event adapters |
| `Test` debug objects and `CombatTest/CombatTest` | debug/test tools | Test | serialized | intentional test-only content |

No Debug/Demo/Legacy component reference was found in the reusable combat/UI prefabs scanned. Player/enemy prefab script GUIDs were audited; no Batch 9 asset edit occurred.

## 34. Code-only neutralizations

- Five Debug lifecycle entry points use `UNITY_EDITOR` guards.
- `BattleTransitionController.HandleBattleRequested` returns immediately when any canonical `CombatEntryPoint` exists, including inactive objects.
- Existing Combat Demo canonical UI/world detection was retained.

## 35. Required future Inspector cleanup

| Asset | Hierarchy | Component | Recommendation | Required? | Serialization risk |
|---|---|---|---|---|---|
| `Dungeon 1` | `Systems` | validator | remove after Editor validation workflow is replaced | optional | low but save scene deliberately |
| `Demo` | `Systems` | validator | retain for Demo validation or remove | optional | low |
| `InGame` | `BattleTrasitionController` | legacy transition | remove only after normal encounter Play Mode proof | later required for clean production scene | medium; UnityEvents/refs unknown |
| `Dungeon 1` | mission objects above | compatibility types | wire canonical Quest/Story first, then remove only after migration | not in Batch 9 | high |

## 36. OfficeFlowController.cs.meta findings

Path: `Assets/GAME/Scripts/Office/OfficeFlowController.cs.meta`. Current GUID text is `6bd14988aa4a45a794929d9f59e463c` (33 characters; malformed). Unity reports that the GUID cannot be parsed and ignores the corresponding script asset. No scene/prefab/SO reference to this exact text and no duplicate were found. It affects metadata import, not C# compilation. It was not changed. Recommended repair: a dedicated metadata repair after a reference/migration plan, not Batch 9.

## 37. Public API changes

None.

## 38. Serialized field impact

None. Existing field names and types were preserved.

## 39. Scene/prefab/SO/Input Actions impact

None modified. No `.unity`, `.prefab`, `.asset`, `.inputactions`, or generated input file changed.

## 40. Existing `.meta` impact

None. Only new files received new metas.

## 41. Unity runtime compilation

Passed under Unity 6000.2.6f2 during targeted and full EditMode runs: zero C# errors. Warnings were pre-existing obsolete TMP wrapping, unused compatibility fields/events, malformed Office meta, and empty Tests asmdef.

## 42. Unity Editor/test compilation

Passed: zero C# errors.

## 43–52. Automated test results

| Fixture | Passed | Failed | Skipped/inconclusive |
|---|---:|---:|---:|
| DebugLegacySeparationTests | 22 | 0 | 0 |
| DialogueQuestIntegrationTests | 57 | 0 | 0 |
| WorldEncounterIntegrationTests | 99 | 0 | 0 |
| CombatCompletionRewardFlowTests | 82 | 0 | 0 |
| CombatUIRoutingTests | 54 | 0 | 0 |
| CombatSessionTurnFlowTests | 44 | 0 | 0 |
| CombatEntryConsolidationTests | 35 | 0 | 0 |
| CombatFoundationTests | 2 | 0 | 0 |
| GameStateOwnershipTests | 6 | 0 | 0 |
| InputOwnershipTests | 18 | 0 | 0 |

The Batch 9 fixture uses parameterized source, reflection, assembly-reference, API, serialization-name, bootstrap, direct-session, canonical-fallback, and GUID cases. Existing focused fixtures provide behavioral coverage for canonical teardown, state, input, combat, UI, reward, world, narrative, and quest invariants.

## 53. Full EditMode result

`Logs/Batch9FullEditMode.xml`: 419 passed, 0 failed, 0 skipped, 0 inconclusive. `Logs/Batch9FullEditMode.log` contains no C# compilation error.

## 54. Development or Production build result

Not run. No player build was produced; release stripping/scene inclusion is therefore not claimed as tested.

## 55. Dungeon 1 Play Mode result

Not run. Execute the task’s Dungeon 1 sequence, including normal field combat/reward, dialogue, quest progress, F8/F9/F10 policy, duplicate-owner observation, and scene reload.

## 56. Demo/Test Play Mode result

Not run manually. Automated compatibility and ownership tests passed. Open Demo, Test, and CombatTest and verify fallback, direct calculation tools, teardown, and no persistence into Dungeon 1.

## 57. Unexecuted validation

- Interactive Dungeon 1, Demo, Test, CombatTest Play Mode procedures.
- Development/release player build.
- Visual Inspector Missing Script/UnityEvent inspection in every scene.
- Physical input, animation, camera, coroutine, and rendered UI behavior.

## 58. Known risks

- `Dungeon 1` retains compatibility mission/story wiring because canonical Inspector migration is incomplete.
- `InGame` retains a serialized Legacy transition component; code neutralization depends on a canonical entry being present.
- Some test-scene-only older debug files use legacy text encoding and remain unmodified; their production safety currently relies on absence from production serialized assets/build flow.
- Single-assembly source-boundary tests cannot provide the hard exclusion of a dedicated runtime asmdef.
- Office meta remains malformed.

## 59. Preserved compatibility

All legacy public methods, UnityEvent-compatible APIs, serialized field names, Demo fallback, direct calculation test tools, asset GUIDs, and Batch 1–8 canonical ownership are retained.

## 60. Removal prerequisites for retained Legacy types

- Battle transition/manager/trigger: verify all scene GUID/UnityEvent references, canonical encounter replacement, InGame/Test Play Mode, then remove components before code.
- FieldEnemy: migrate Test scene to canonical encounter trigger and verify defeat/mission adapters.
- DialogueRunner/UI: verify no reflection/UnityEvent references and canonical Story presenter wiring in every scene.
- MissionManager/data: migrate/verify all mission assets and save compatibility against QuestRuntime.
- old input/player: verify player prefabs/scenes use installer/service only and no duplicate maps enable.
- old UI/NonCombat controllers: prove they own local content only or migrate global roots to UIScreenRouter.

## 61. Unrelated changes preserved

Confirmed. All initial Batch 8 modified/untracked work remains; Batch 9 did not overwrite, reformat, move, delete, or revert it. Final diff review distinguishes Batch 9 files from pre-existing changes.

## 62. Exact recommended next batch

Runtime Refactor Batch 10 — Save/Load Preparation

Batch 10 was not started.

## Validation commands

- `git branch --show-current`; `git status --short --branch`
- `rg` searches for the requested combat/state/input/UI/bootstrap/reflection patterns
- PowerShell script-GUID-to-YAML reference and hierarchy scan
- PowerShell GUID duplicate, malformed meta, and serialized script-GUID scans
- Unity 6000.2.6f2 targeted filter `Game.Tests.Integration.DebugLegacySeparationTests`
- Unity 6000.2.6f2 complete EditMode suite
- `git diff --check`; complete `git diff` and forbidden-asset diff review

