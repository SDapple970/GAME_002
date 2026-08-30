# Production Dungeon Template Setup

## Purpose and canonical copy workflow

`Assets/GAME/Scenes/Dungeon_Template.unity` is the clean Production base for a
field dungeon. Duplicate it in Unity's Project window so the original scene
GUID remains unchanged, rename the copy, and add the new scene to Build
Settings or the existing content selection path. Never duplicate Dungeon 1's
`Systems`, Demo, Debug, Legacy, quest-content, or narrative-content hierarchy.

The repository's current title flow loads `Dungeon 1` by name. To test a copied
scene through the canonical Production route, temporarily point the authored
title/mission destination to the copied scene or add an equivalent existing
scene-flow destination. Direct Play from the template is supported by
`RuntimeBootstrapper.AutoBootstrapLoadedScene`, but the title-to-dungeon route
must also be tested before shipping.

## Final hierarchy

```text
Runtime
├── Core                 (dungeon-local extension slot)
├── Combat
│   └── CombatRuntime    (one instance)
├── Narrative            (dungeon-local extension slot)
├── Quest                (dungeon-local extension slot)
├── Save                 (dungeon-local extension slot)
└── UI
    └── ProductionDungeonUI (one instance)
World
├── Environment
├── Collision
│   └── Floor
├── SpawnPoints
│   └── PlayerSpawn
└── Encounters
    └── TestEncounter_01
        └── EnemyRoot
            ├── ContactTrigger
            ├── PatrolLeft
            └── PatrolRight
Actors
├── Player
│   └── PlayerRoot (Player prefab instance)
└── NPCs
Main Camera
```

All organization roots use identity rotation and scale. `Runtime` is at the
scene origin. Empty subsystem roots are placement slots, not missing managers.

## Exactly-once ownership

Each loaded dungeon has exactly one `CombatRuntime`, `CombatEntryPoint`,
`CombatFlowOrchestrator`, `CombatDirector`, `CombatRewardUIBinder`,
`CombatPlanningHUD`, `RewardUIPanel`, `GameUIRootController`,
`UIScreenRouter`, and EventSystem. The runtime prefab is
`Assets/GAME/Prefabs/CombatRuntime.prefab`; the UI prefab is
`Assets/GAME/Prefabs/UI/ProductionDungeonUI.prefab`.

`RuntimeBootstrapper` supplies or reuses `GameStateMachine`,
`GameFlowController`, `SceneFlowController`, `SaveLoadService`,
`GameInputInstaller`, `RewardService`, `GameUIRootController`, and
`UIScreenRouter`. Do not add those persistent services under the empty
dungeon-local roots. `InputService` and `InputRouter` are owned by
`GameInputInstaller`, not by the player or HUD.

`Actors/Player/PlayerRoot` owns exactly one `InteractionController` as a scene-added prefab-instance component. Keep it on the moving player root because target selection measures from the controller transform. Do not apply it to `Player.prefab`: Demo and mixed compatibility scenes keep their own scene-local controllers. The canonical `Gameplay/Interact` keyboard binding is `F`, and authored Production prompts must use `F` until a binding-display integration is added.

The controller's `promptUI` reference is explicitly connected to `ProductionDungeonUI/FieldRoot/InteractionPromptHost`. The host stays active and owns a small field overlay Canvas plus `InteractionPromptUI`; only its child `InteractionPromptRoot` is toggled. `PromptText` uses `UI.Text` for compatibility with the existing serialized prompt API. This UI displays prompt text only and does not own input, `GameState`, target selection, or interaction execution.

Never copy `Systems`, `RuntimeBootstrapper`, a second UI router, another reward
binder, `CombatStateSyncer`, `CombatDemoFlowController`, debug start/smoke-test
tools, auto planners, `SeamlessBattleManager`, or Legacy battle triggers into a
dungeon.

## Environment, collision, spawn, and camera

Replace content below `World/Environment`. Replace colliders below
`World/Collision`, retaining the project Ground layer for walkable surfaces.
Move `World/SpawnPoints/PlayerSpawn` to the intended entry position and place
the Player wrapper at that position. The current scene-flow layer has no
spawn-ID routing for this marker, so it is an authored marker until an existing
stage/spawn integration explicitly consumes it; do not add a spawn manager.

The Main Camera uses `CameraFollow2D` and follows the Player wrapper.
`CombatCameraController` is attached as a scene override to `CombatRuntime`,
targets the Main Camera, suspends the follow component in combat, and restores
the exploration camera state afterward.

## Encounters

For another single-enemy encounter, duplicate `TestEncounter_01`, give it a
stable unique `encounterId`, replace `EnemyRoot` visuals/data, and retain this
shape:

```text
Encounter [CombatEncounterGroup, autoCollectChildren=true]
└── EnemyRoot
    ├── ContactTrigger
    ├── PatrolLeft
    └── PatrolRight
```

Only actual enemy roots may be direct children of an auto-collected group.
Connect `ContactTrigger.enemyObject` to its `EnemyRoot`,
`ContactTrigger.encounterGroup` to the encounter root, and
`ContactTrigger.entryPoint` to the scene's sole `CombatEntryPoint`. Set valid
patrol references, enemy tag/layer, physical and trigger colliders, HP,
loadout/fallback skill path, motor, and patrol AI.

For a multi-enemy encounter, add each enemy root directly below the group and
keep helpers below their enemy. Alternatively disable `autoCollectChildren` and
serialize the exact enemy roots. Every contact trigger uses the same group and
entry point. The Player prefab instance's `PlayerFieldAttackController` also
targets that same entry point; its parent-group resolution includes all active
members. Never adapt patrol points, trigger helpers, or visual-only objects as
combatants.

## Combat and reward UI

`ProductionDungeonUI` contains one interactive `CombatPlanningHUD`, one
`RewardUIPanel`, and one Input System EventSystem. The HUD is connected in the
scene to the local entry point and flow orchestrator. The runtime's sole
`CombatRewardUIBinder` is connected to the reward panel and resolves the
singleton `RewardService`. Runtime-created `GameUIRootController` and
`UIScreenRouter` auto-bind the unique HUD/panel and route them from
`GameState`.

If a future bootstrap scene supplies this UI persistently, remove the
dungeon-local `ProductionDungeonUI` instance only after verifying one HUD,
panel, router, root controller, binder, and EventSystem remain after scene
changes.

Narrative, Quest, Save, and dungeon-local UI roots may later receive explicit
dungeon integrations. They must call the existing Production flow/services and
must not become alternate owners. Authored quest, dialogue, choice, and save
data are intentionally absent from this template.

## Manual Play Mode validation

1. Start from `TitleScene` and enter a copy through `SceneFlowController`;
   separately test direct Play from the copied scene.
2. Confirm one global service set, one EventSystem, and one dungeon entry point.
3. Confirm Player spawn, movement, grounding, field attack, camera follow,
   enemy patrol, detection, and chase.
4. Contact the enemy. Confirm `CombatPlanning`, movement/attack lock, planning
   UI, skill and target selection, `CombatResolving`, and presentation.
5. Win. Confirm the enemy becomes inactive, Reward appears once, closing it
   requests Exploration once, and movement/attack/camera restore.
6. Reload/change scenes and confirm global services do not duplicate.
7. Reset and repeat using Player field attack.
8. Temporarily add a second valid enemy root to the group and verify both
   contact and field attack create a two-enemy session.

## Known limitations

- The template scene is not enabled in Build Settings; a duplicated production
  scene must be added or referenced by an existing content route.
- `PlayerSpawn` is an authored marker because current `SceneFlowController`
  does not route by spawn ID.
- Multi-enemy support is implemented and covered by EditMode logic tests, but
  every authored multi-enemy scene still needs Play Mode validation.
- Dungeon 1 retains its existing mixed compatibility content and is not a
  source hierarchy to copy.
