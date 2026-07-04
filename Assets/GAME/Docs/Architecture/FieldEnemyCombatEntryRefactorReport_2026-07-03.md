# FieldEnemy Combat Entry Refactor Report - 2026-07-03

## 1. Summary

Field enemy combat start ownership was narrowed so field objects no longer start production combat through legacy battle events or list-based entry calls. Production field/tactical start sources now build a `CombatStartRequest` and pass it to `CombatEntryPoint.StartCombat(...)`.

No files were moved, renamed, or deleted. Existing serialized fields on `FieldEnemy` were preserved.

`EncounterRequest` was not found as a distinct runtime type. The existing combat request type, `CombatStartRequest`, remains the single request model for this step.

## 2. Files Changed

- `Assets/GAME/Scripts/Combat/FieldEnemy.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatEntryPoint.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatEncounterTrigger2D.cs`
- `Assets/GAME/Scripts/Player/Runtime/PlayerFieldAttackController.cs`
- `Assets/GAME/Scripts/Tutorial/TutorialBattleStartInteractionEventSO.cs`

## 3. Existing Combat-Start Paths Found

- Production/direct field paths:
  - `FieldEnemy` triggered combat directly through `CombatEntryPoint.StartCombatFromField(...)`.
  - `CombatEncounterTrigger2D` triggered combat directly through `CombatEntryPoint.StartCombatFromField(...)`.
  - `PlayerFieldAttackController` triggered combat directly through `CombatEntryPoint.StartCombatFromField(...)`.
  - `TutorialBattleStartInteractionEventSO` triggered combat directly through `CombatEntryPoint.StartCombatFromField(...)`.
- Legacy path:
  - `BattleTrigger2D -> BattleTransitionController`
  - `BattleTrigger2D -> SeamlessBattleManager`
- Debug/test paths:
  - `CombatFieldCallDebug -> CombatEntryPoint.StartCombatFromField(...)`
  - `CombatStartSmokeTest -> CombatBootstrapper.StartCombat(...)`

## 4. Final Production Combat-Start Path

Field/contact route:

```text
FieldEnemy / CombatEncounterTrigger2D
-> CombatStartRequest
-> CombatEntryPoint.StartCombat(...)
-> CombatBootstrapper.StartCombat(...)
```

Field attack/tutorial route:

```text
PlayerFieldAttackController / TutorialBattleStartInteractionEventSO
-> CombatStartRequest
-> CombatEntryPoint.StartCombat(...)
-> CombatBootstrapper.StartCombat(...)
```

`CombatEntryPoint.StartCombatFromField(...)` remains as a compatibility wrapper and delegates to `StartCombat(CombatStartRequest)`.

## 5. Debug / Legacy Paths Left Intact

- `CombatFieldCallDebug` still uses `StartCombatFromField(...)` as a debug helper.
- `CombatStartSmokeTest` still calls `CombatBootstrapper.StartCombat(...)` directly as an isolated debug/bootstrap smoke test.
- `BattleTrigger2D`, `BattleTransitionController`, and `SeamlessBattleManager` remain unchanged as legacy compatibility paths.
- `FieldEnemy.OnBattleRequested` remains declared for compatibility but is no longer invoked by production `FieldEnemy` combat start.

## 6. Inspector Changes Required

None expected.

Existing `FieldEnemy` serialized fields were preserved:

- `combatEntryPoint`
- `playerTag`
- `battleSceneName`
- `openingEffectOrNull`
- `touchStartReason`
- `hitStartReason`
- `playerTarget`
- `aggroRange`
- `moveSpeed`
- `countForDemoMission`
- `demoMissionRuntime`

`CombatEntryPoint` is still optional on field enemies because fallback lookup remains in place.

## 7. Risks / Scene-Prefab Reference Notes

- `FieldEnemy` no longer subscribes to `CombatEntryPoint.OnCombatEnded` and no longer registers demo mission enemy defeat from combat results or disable-time HP checks. Existing `RegisterDemoMissionDefeat()` remains public for compatibility. Current combat-end demo mission defeat registration already exists in `CombatRewardUIBinder`.
- `CombatStartRequest` itself was not modified because the `Adapters` folder was not writable in this session. Callers use its existing constructor with `0, -1` sentinel values, and `CombatEntryPoint.StartCombat(...)` resolves those to the entry point's configured inspiration defaults.
- `CombatEntryPoint.StartCombat(...)` currently preserves the existing first-field-object behavior by normalizing the request through the same first-object copy logic used by `StartCombatFromField(...)`.
- No scene or prefab reference should break because script paths, class names, namespaces, and serialized field names were preserved.

## 8. Test Procedure

1. Run `dotnet build Assembly-CSharp.csproj --no-restore --nologo`.
2. Confirm the build reports `0 warnings` and `0 errors`.
3. In Unity, open the main field/dungeon scene.
4. Press Play.
5. Approach or collide with a `FieldEnemy` / `CombatEncounterTrigger2D`.
6. Confirm combat starts through `CombatEntryPoint.StartCombat(...)`.
7. Confirm combat UI still appears.
8. Confirm player exploration movement is locked during combat.
9. Confirm duplicate combat does not start repeatedly from the same enemy.
10. Confirm existing debug combat start tools still work if they existed before.

Build result from this session:

```text
dotnet build Assembly-CSharp.csproj --no-restore --nologo
Exit code: 1
0 warnings
0 errors
```

This matches the earlier audit behavior where Unity-generated project/tooling returned nonzero while reporting no C# warnings or errors. Unity editor/batchmode compile was not run because Unity is not on PATH in this environment.

## 9. Completion Criteria

- Project reports `0` C# compile errors through the available `dotnet build` check.
- `FieldEnemy` no longer invokes legacy battle request events for production combat start.
- `FieldEnemy` creates a `CombatStartRequest` and forwards it to `CombatEntryPoint.StartCombat(...)`.
- `CombatEncounterTrigger2D`, `PlayerFieldAttackController`, and tutorial combat start now use the same request-based entry point.
- `CombatEntryPoint` remains the production combat entry owner.
- Debug and legacy paths are left intact and documented.
- No scene/prefab-breaking structural changes were made.
