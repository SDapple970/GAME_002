# Scene and Prefab Wiring Risk Report

## Method and limits

All `.unity`, `.prefab`, and `.asset` YAML under `Assets/GAME` was scanned read-only. Script GUIDs were resolved against GAME and package `.meta` files. Every `m_Script` GUID resolved; no missing-script GUID was found by text scan. Unity did not complete an Editor load because Package Manager IPC failed, so broken object fileIDs, UnityEvent method signatures, and Inspector warnings remain Editor-validation items.

## High-risk scene wiring

- `Dungeon 1.unity` is currently modified before this audit and contains the densest live stack: CombatEntryPoint, CombatFlowOrchestrator, CombatDirector, CombatPlanningHUD, CombatRewardUIBinder, encounter trigger, player runtime stack, DemoMission runtime/end flow, QuestManager/Tracker, Story runner/interaction, reward/global UI, and input/core services. It must be migrated last or in narrowly verified batches.
- `Demo.unity`, `InGame.unity`, and `Test.unity` contain overlapping old/new combat, player, UI, and state components. `Test.unity` intentionally includes debug bypasses.
- `New_Dungeeon_Manual.unity` is untracked and contains the newer QuestRuntime/QuestObjectiveTracker/QuestCompletionFlow plus inventory, currency, daily, and office flows. Do not treat its references as committed baseline.
- `CombatTest.unity` intentionally wires `CombatTestRunner` and must remain isolated from production entry rules.
- `Dungeon 4.unity` and its meta were already deleted before audit. No conclusion about its former references is possible from the working tree.

## Serialized field risks by system

Combat: `CombatEntryPoint.director`, `flowOrchestrator`, `skillDefinitions`, outcome flags; `CombatStartRequest` runtime GameObject lists; encounter trigger entry/player/group/opening effect/reason; formation anchors; director animation/effect objects; planning button prefab and content roots; widget prefab; reward binder panel/service; camera and field-lock behaviour lists. Skill and opening ScriptableObjects are GUID-bound and must not be recreated casually.

Input: `OverworldInputAdapter` action references, `CombatFieldCallDebug` F1/F2/F3 references, `CombatStartSmokeTest.debugStep`, `MissionCompleteCutsceneController.skipAction`, and `OverworldInputBridge.playerInput`. Generated action name `Gameplay` and action identifiers Move/Jump/Attack/Parry/Interact/Pause are string/API contracts.

UI: all GameUIRootController root GameObjects; CombatUIRootController combat/planning/widget/overworld/reward roots; RewardUIPanel close button, row root/prefab, TMP/legacy texts, RewardApplier; Dialogue/Choice button prefabs and containers; mission/end panels; CanvasGroups. Button `onClick` and other UnityEvents can reference methods only visible in YAML by target fileID/method name and require Inspector verification after any type/method move.

Mission/Quest: DemoMissionDefinitionSO, RescueNpcDefinitionSO, QuestDataSO, QuestDefinitionSO arrays, MissionDefinitionSO, rescue actor sprite/interaction references, completion panel/controller links, behaviour lists disabled on completion, quest IDs/objective IDs stored as strings, and bridge flags. Migrating asset types without conversion would lose serialized data.

World/scene flow: scene-name strings in SceneFlowController callers, SceneTravelService/events, MissionCompletePanel, tutorial return flow, title/demo end controllers, and mission definitions. Validate every name against Build Settings before renaming scenes. Spawn point IDs and story event/flag IDs are also string contracts.

Prefabs directly identified: `Assets/GAME/Prefabs/RewardItem.prefab` and `Assets/GAME/Prefabs/UI/Combat/RewarItemUI.prefab` use `RewardItemUI`; `Assets/GAME/Scripts/Combat/Data/CombatantWidget/CombatantWidget.prefab` uses `CombatantWidget`; combat/choice button prefabs carry Unity UI package components and are referenced by serialized prefab fields.

## Required safeguards per refactor

1. Record component instance IDs and serialized fields in the Inspector before changing a type or field.
2. Never move/rename a script without its `.meta`; namespace/type renames require explicit migration.
3. Do not edit YAML automatically. Change component references through Unity and review the diff.
4. Open every affected scene/prefab, check Missing Script and broken UnityEvents, enter Play Mode, then save intentionally.
5. Compare `git diff -- Assets/GAME` and verify no unexpected GUID/fileID churn.

## Baseline integrity

The audit adds Markdown only. Existing scene modifications/deletions are user-owned baseline changes. No scene, prefab, ScriptableObject, runtime C#, or `.meta` file is intentionally changed by this audit.

