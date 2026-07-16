# Reusable Dungeon Combat Setup

This setup is verified from `Assets/GAME/Scenes/Dungeon 1.unity` and the current runtime code. It preserves the production flow:

`Field encounter -> CombatStartRequest -> CombatEntryPoint -> FieldCombatantAdapter -> CombatSession -> CombatPlanning -> CombatResolving -> CombatResult -> CombatRewardUIBinder -> Reward -> GameFlowController -> Exploration`

Use `Assets/GAME/Prefabs/DungeonCombatRuntime.prefab` once per dungeon scene. Do not copy Dungeon 1's entire `Systems` or `UI` hierarchy with it.

## Ownership and classification

| Layer | Verified owner or object | Classification | Reuse rule |
|---|---|---|---|
| Global state | `GameStateMachine` | Production global service | One persistent instance; never add to the dungeon-runtime prefab. |
| Global flow | `GameFlowController` | Production global service | One instance; combat and reward request states through it. |
| Bootstrap | `RuntimeBootstrapper` | Production global service creator | Let the bootstrap find/create services. Do not copy it with a dungeon. |
| Rewards | `RewardService` | Production global service | One instance; `CombatRewardUIBinder` resolves it if its field is empty. |
| Input | `GameInputInstaller`, `InputService`, `InputRouter` | Production global input | One persistent installer; exploration commands are allowed only in `Exploration`. |
| UI routing | `UIScreenRouter`, `GameUIRootController` | Production global UI ownership | One routing path; do not duplicate it in the runtime prefab. |
| Dungeon combat | `DungeonCombatRuntime` prefab | Production dungeon-local | Exactly one instance per loaded dungeon scene. |
| Planning UI | `CombatPlanningHUD` under Dungeon 1 `UI/Canvas/CombatHUD` | Production combat UI | One HUD, connected to the scene's runtime instance. |
| Reward UI | `RewardUIPanel` under Dungeon 1 `UI/RewardUI` | Production reward UI | One panel, used by one `CombatRewardUIBinder`. |
| Camera | `CombatCameraController` | Optional production presentation | Included in the runtime prefab; may auto-resolve `Camera.main` or be explicitly connected. |
| Formation | `CombatFormationManager` | Optional production presentation | Included but formation placement remains disabled by the verified Dungeon 1 setting. |
| Field lock | `CombatFieldLock` | Optional production presentation/integration | Not used by Dungeon 1. Add only for physics/behaviours not already gated by `GameState`. |
| State observer | `CombatStateSyncer` | Compatibility-only | Excluded. `CombatEntryPoint` already owns production combat phase synchronization. |
| Tutorial bridge | `TutorialQuestCombatBridge` | Tutorial-specific compatibility | Excluded. Add separately only in tutorial content that needs it. |
| Demo flow/UI | `CombatDemoFlowController`, DemoMission/Demo UI components | Demo | Excluded; never required by production dungeon combat. |
| Debug tools | `CombatFieldCallDebug`, `CombatStartSmokeTest`, `CombatTestRunner`, `CombatAutoPlanner`, `VerticalSliceSceneValidator` | Debug/test | Excluded; never required by production flow. |
| Old battle path | `SeamlessBattleManager`, `BattleTrigger2D`, battle transition components | Legacy | Retained in the repository but must not be connected to production encounters. |

Dungeon 1 currently places global services, Demo/Debug components, and production services together on `Systems`. `GameStateMachine` calls `DontDestroyOnLoad` on that GameObject, so copying `Systems` can persist and duplicate everything attached to it. A new dungeon must receive global services from the existing bootstrap/persistent root, not from `DungeonCombatRuntime`.

## Reusable dungeon-local runtime

The prefab contains:

- `CombatEntryPoint`: sole production combat start/end entry point and defeated-enemy deactivation.
- `CombatFlowOrchestrator`: validates/commits the player's plan and advances the current session.
- `CombatDirector`: presents the already-resolved playbook.
- `CombatRewardUIBinder`: grants through `RewardService`, enters `Reward`, shows `RewardUIPanel`, and returns through `GameFlowController` when the panel closes.
- `CombatCameraController`: optional camera framing.
- `CombatFormationManager`: optional formation placement, disabled by default.

It deliberately excludes global services, canvases, HUDs, players, enemies, encounter triggers/groups, tutorial bridges, `CombatStateSyncer`, Demo/Debug components, and Legacy battle components.

## Required combat UI

The verified Dungeon 1 UI is scene-authored, not part of the runtime prefab:

- One `CombatPlanningHUD` with its planning panel, skill list root, target list root, choice-button prefab, confirm button, and error text.
- One `RewardUIPanel` with its root, display fields/rows, and close button.
- One `GameUIRootController` whose `combatRoot` is the combat HUD and whose `rewardRoot` is the reward UI.
- One `UIScreenRouter` connected to the UI root controller and global state machine.
- One `CombatRewardUIBinder` only. The reusable runtime prefab provides it; do not add a second binder to the UI hierarchy.

`CombatPlanningHUD` subscribes to `CombatEntryPoint` and uses the same `CombatFlowOrchestrator`. `CombatRewardUIBinder` subscribes to that same entry point. This is the single production connection path.

## Player and enemy requirements

Required player setup:

- An active player root resolvable by `PlayerInputController`, `CombatHpComponent`, or the `Player` tag.
- `CombatHpComponent` (or a compatible readable/writable integer HP component).
- `CombatSkillLoadoutComponent` for an authored loadout; registered ally fallback skills are used only when it is empty.
- `PlayerFieldAttackController` for field-attack entry, with attack origin, enemy layer mask/tag, and the dungeon `CombatEntryPoint`.
- Normal production movement/input components. They remain locked outside `Exploration` by the global input/state path.
- `CombatKeywordComponent` and animation drivers are optional presentation/rules data.

Required enemy setup:

- An active enemy root with a `Collider2D`; Dungeon 1 uses enemy layer 7 and the `Enemy` tag.
- `CombatHpComponent` (or a compatible HP component).
- `CombatEncounterTrigger2D` for contact entry, connected to the dungeon `CombatEntryPoint` and its enemy root.
- `CombatSkillLoadoutComponent` for authored enemy skills; otherwise the registered fallback skill is used.
- `CombatKeywordComponent`, motor/patrol, animator, and `CombatantAnimationDriver` as required by that enemy's rules/presentation.

`CombatEntryPoint` deactivates defeated enemy field objects with the verified prefab setting. `CombatEncounterTrigger2D` also disarms after a successful start and only rearms after player exit when combat is no longer active. An inactive defeated enemy therefore cannot immediately retrigger combat.

## Encounter setup

For one enemy, put `CombatEncounterTrigger2D` on the enemy collider/root, set `enemyObject` to that root, leave `encounterGroup` empty, and connect `entryPoint` to the dungeon runtime instance. Both contact and `PlayerFieldAttackController` then create `CombatStartRequest` objects and call the same `CombatEntryPoint`.

For multiple enemies, create an encounter root with `CombatEncounterGroup`:

- With `autoCollectChildren` enabled, make direct child GameObjects the actual enemy roots. Do not put non-enemy helper children directly under the group.
- Alternatively disable auto-collection and explicitly populate `enemies`.
- Put/retain `CombatEncounterTrigger2D` on an enemy or trigger collider and connect its `encounterGroup` to the group root.
- A player field attack resolves a parent `CombatEncounterGroup` automatically.

Dungeon 1 currently has three independent single-enemy triggers and no serialized `CombatEncounterGroup`. The code copies all active group members into the request and `FieldCombatantFactory` adapts every member, but multi-enemy scene behavior still requires Play Mode validation.

## Inspector reference table

| Component.field | Required value | Connection type |
|---|---|---|
| `CombatEntryPoint.director` | Prefab `CombatDirector` | Connected inside prefab |
| `CombatEntryPoint.flowOrchestrator` | Prefab `CombatFlowOrchestrator` | Connected inside prefab |
| `CombatEntryPoint.skillDefinitions` | Dungeon 1's verified Basic Attack, Skill 2, Angel Skill, Angel Skill 2 assets | Stored in prefab |
| `CombatEntryPoint.deactivateDefeatedEnemies` | `true` | Stored in prefab |
| `CombatEntryPoint.destroyDefeatedEnemies` | `false` | Stored in prefab |
| `CombatFlowOrchestrator.entryPoint` | Same prefab `CombatEntryPoint` | Connected inside prefab |
| `CombatDirector.entryPoint` | Same prefab `CombatEntryPoint` | Connected inside prefab |
| `CombatDirector.cameraController` | Prefab `CombatCameraController` | Connected inside prefab; optional presentation |
| `CombatFormationManager.entryPoint` | Same prefab `CombatEntryPoint` | Connected inside prefab |
| `CombatRewardUIBinder.entryPoint` | Same prefab `CombatEntryPoint` | Connected inside prefab |
| `CombatRewardUIBinder.rewardPanel` | Scene/global `RewardUIPanel` | Leave empty for auto-bind or assign manually |
| `CombatRewardUIBinder.rewardService` | Global `RewardService` | Leave empty for singleton auto-bind or assign manually |
| `CombatCameraController.targetCamera` | Dungeon camera | Leave empty for `Camera.main` or assign manually |
| `CombatCameraController.explorationCameraFollow` | Dungeon camera-follow behaviour | Optional manual connection; needed to suspend/resume that follow behaviour |
| `CombatPlanningHUD.entryPoint` | Prefab instance `CombatEntryPoint` | Manual scene connection |
| `CombatPlanningHUD.flowOrchestrator` | Same prefab instance `CombatFlowOrchestrator` | Manual scene connection |
| `CombatPlanningHUD.panelPlanning` | Planning panel | Required UI connection |
| `CombatPlanningHUD.skillListRoot` / `targetListRoot` | Respective list transforms | Required UI connections |
| `CombatPlanningHUD.buttonPrefab` | `Prefabs/UI/Combat/CombatChoiceButtonPrefab` | Required asset connection |
| `CombatPlanningHUD.confirmButton` | Planning confirm button | Required UI connection |
| `GameUIRootController.combatRoot` | Combat HUD root | Required UI routing connection |
| `GameUIRootController.rewardRoot` | Reward UI root | Required UI routing connection |
| `UIScreenRouter.uiRoot` / `stateMachine` | Global UI root controller / state machine | Required global routing connections or verified auto-bind |
| `CombatEncounterTrigger2D.entryPoint` | Prefab instance `CombatEntryPoint` | Manual per-encounter connection |
| `CombatEncounterTrigger2D.enemyObject` | Enemy root | Manual per-encounter connection; defaults to its own GameObject |
| `CombatEncounterTrigger2D.encounterGroup` | Group root, or empty for single enemy | Manual when grouped |
| `PlayerFieldAttackController.entryPoint` | Prefab instance `CombatEntryPoint` | Manual player connection |

## Create a new dungeon

1. Enter the dungeon through the existing production bootstrap/scene flow. Do not copy Dungeon 1's `Systems` object into the new scene.
2. Add exactly one `DungeonCombatRuntime` prefab instance.
3. Add or reuse one combat HUD and one reward UI owned by the existing UI routing design; do not duplicate the full UI root if it persists from the bootstrap scene.
4. Connect `CombatPlanningHUD` to the prefab instance's entry point and flow orchestrator. Verify all HUD child references.
5. Ensure exactly one `CombatRewardUIBinder` is active. Assign the reward panel/service or verify auto-binding finds the intended single instances.
6. Connect the prefab camera fields only if the dungeon needs explicit camera-follow suspension. Enable formation placement only when authored for that dungeon.
7. Add the player HP/loadout/field-attack bindings and connect field attack to the same entry point.
8. Configure each enemy for a single encounter or place enemies under a `CombatEncounterGroup`; connect every trigger to the same entry point.
9. Confirm no `CombatDemoFlowController`, Debug start helper, Legacy battle manager/trigger, or second combat entry point is active.
10. Run the single- and multi-enemy Play Mode checks below before shipping the scene.

## Play Mode validation

Single enemy:

1. Open Dungeon 1 (or the new dungeon) and enter Play Mode.
2. Confirm one active `GameStateMachine`, `GameFlowController`, `RewardService`, `GameInputInstaller`, `GameUIRootController`, `UIScreenRouter`, and `CombatEntryPoint`.
3. Contact an enemy, then repeat using a field attack after resetting the run. Confirm both paths start through the same entry point.
4. Confirm global state becomes `CombatPlanning`, movement/field attack stop, and the planning UI appears.
5. Select a skill/target and complete a turn. Confirm the state visits `CombatResolving` and returns to planning if combat continues.
6. Win and confirm the enemy is inactive, one reward UI appears, and global state is `Reward`.
7. Close reward and confirm global state and exploration input return to `Exploration` exactly once.
8. Reload/change scenes and confirm persistent services are not duplicated and the next dungeon has exactly one dungeon runtime.
9. Duplicate an encounter object, reconnect its trigger to the same runtime, and confirm each enemy starts its own combat without adding a manager.

Multiple enemies:

1. Create a temporary `CombatEncounterGroup` with two active enemy roots as direct children (or an explicit enemy list).
2. Connect one trigger's `encounterGroup` and `entryPoint`; ensure the player field-attack layer mask can hit the group enemies.
3. Start by contact and confirm the start log/session reports two enemies and both appear as planning targets.
4. Reset and start by field attack; confirm the same two-enemy roster.
5. Defeat the group and confirm every defeated member is deactivated and none immediately retriggers combat.
6. Remove the temporary test setup if it is not authored content.

## Common failure symptoms

| Symptom | Check |
|---|---|
| Combat starts but movement remains active | Verify one `GameStateMachine`/`GameFlowController`, state reaches `CombatPlanning`, and exploration input uses `InputRouter`/state guards. Check optional `CombatFieldLock` only for ungated behaviours or physics. |
| Planning UI does not appear | Check `UIScreenRouter`, `GameUIRootController.combatRoot`, the HUD's entry/flow references, active hierarchy, event subscription, panel/list/button references, and that no Demo UI controller hides it. |
| Reward does not appear | Check exactly one binder, binder entry point, `RewardUIPanel`, `RewardService`, `GameFlowController`, reward root routing, and the `OnCombatEnded` event. |
| Reward closes but exploration input does not return | Check binder `restoreExplorationAfterRewardClosed`, `RewardUIPanel.OnClosed`, `GameFlowController.HandleRewardClosed`, and that no competing reward/demo flow owns the close event. |
| Only one enemy enters combat | Check the trigger/group reference, direct-child versus explicit group membership, active enemy roots, HP components, and the start log/session enemy count. |
| Duplicate reward UI | Remove the extra `CombatRewardUIBinder`, duplicate `RewardUIPanel`, or `CombatDemoFlowController`; keep one binder/panel/router path. |
| Duplicate managers after scene load | Do not copy `Systems` or UI bootstrap roots into the dungeon prefab. Inspect persistent roots and ensure exactly one global service set plus one dungeon `CombatEntryPoint`. |

## Verified limitations

- Dungeon 1's single-enemy path is serialized; a multi-enemy group is supported by current code but not authored in the scene and must be validated manually.
- The reusable prefab intentionally leaves scene camera and reward UI/service references empty. Their documented auto-bind/manual connections are the only remaining Inspector work.
- No combat calculation, state machine, resolver, reward-flow, input, or UI-routing code is changed by this setup.
