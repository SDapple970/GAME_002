# Combat Entry State/Input/UI Validation Report - 2026-07-03

## 1. Summary

Post-refactor combat entry was validated around state, input lock, and combat UI routing. `CombatEntryPoint.StartCombat(CombatStartRequest)` now owns the production transition to `GameState.CombatPlanning` before it raises `OnCombatStarted`, so input guards and UI routers see combat state immediately.

Scope was limited to combat state/input/UI routing. Quest, Reward, Daily, Social, Shop, combat damage, and combat resolution rules were not refactored.

## 2. Files Changed

- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatEntryPoint.cs`
- `Assets/GAME/Scripts/UI/UIScreenRouter.cs`
- `Assets/GAME/Docs/Architecture/CombatEntryStateInputUIValidationReport_2026-07-03.md`

## 3. CombatEntryPoint State Flow

Current production start flow:

```text
FieldEnemy / CombatEncounterTrigger2D / PlayerFieldAttackController / TutorialBattleStartInteractionEventSO
-> CombatStartRequest
-> CombatEntryPoint.StartCombat(...)
-> CombatBootstrapper.StartCombat(...)
-> GameStateMachine.SetState(GameState.CombatPlanning)
-> CombatEntryPoint.OnCombatStarted
```

`CombatEntryPoint` now sets `GameState.CombatPlanning` directly after the combat session/state machine is created and before `OnCombatStarted` is raised.

If `GameStateMachine` is missing, combat can still start, but `CombatEntryPoint` logs a warning because GameState-based input/UI locking cannot be guaranteed.

## 4. Input Lock Flow

Exploration input is blocked by existing GameState guards once `CombatPlanning` is active:

- `GameInputInstaller` only emits move/jump/attack/parry when `InputRouter.AllowsExplorationInput()` allows it.
- `InputRouter.AllowsExplorationInput()` delegates to `GameStateMachine.AllowsExplorationInput()`.
- `GameStateMachine.AllowsExplorationInput(GameState state)` only allows `GameState.Exploration`.
- `PlayerInputController`, `PlayerFieldAttackController`, `OverworldPlayerController`, `PlayerController2D`, `OverworldAttack2D`, and `FieldSkillCaster` already guard exploration actions against non-exploration state.

No player input script required a behavior change in this task.

## 5. Combat UI Show/Hide Flow

Combat start UI routes currently include:

- `UIScreenRouter`: reacts to `GameState.CombatPlanning` and asks `GameUIRootController` to show the combat root.
- `GameUIRootController`: toggles configured root GameObjects, including `combatRoot`.
- `CombatUIRootController`: listens to `CombatEntryPoint.OnCombatStarted` / `OnCombatEnded` and toggles combat HUD roots.
- `CombatPlanningHUD`: listens to `CombatEntryPoint.OnCombatStarted` / `OnCombatEnded`, binds the session, and shows/hides the planning panel.
- `CombatDemoFlowController`: still directly toggles `combatCanvasRoot`, `CombatPlanningHUD`, camera, field lock, and reward UI for demo flow compatibility.
- `CombatRewardUIBinder` and `RewardUIPanel`: handle result/reward UI after combat end.

Only the safest routing fix was made: `CombatEntryPoint` now sets combat GameState itself, and `UIScreenRouter` now warns once if `GameUIRootController` or `GameStateMachine` is missing.

## 6. Duplicate Combat Prevention

`CombatEntryPoint.StartCombat(...)` still blocks starts when:

- `_startingCombat` is true
- `ActiveSession` is not null
- `ActiveStateMachine` is not null
- `GameStateMachine` exists and the current state is not `Exploration`

A minimal warning now reports blocked duplicate/invalid starts once per active block window.

Field-level duplicate guards remain in:

- `FieldEnemy`
- `CombatEncounterTrigger2D`
- `PlayerFieldAttackController`

## 7. Scene/Inspector Wiring Notes

No serialized fields were renamed or removed.

Important scene wiring to verify in Unity:

- A `GameStateMachine` exists in the scene or is created by `RuntimeBootstrapper`.
- `UIScreenRouter` can resolve a `GameUIRootController`.
- `GameUIRootController.combatRoot` points to the intended combat UI root.
- `CombatUIRootController.entryPoint` and `CombatPlanningHUD.entryPoint` are assigned or discoverable.
- `CombatPlanningHUD.panelPlanning`, `buttonPrefab`, `confirmButton`, `skillListRoot`, and `targetListRoot` remain wired.
- If `CombatDemoFlowController` is present, confirm its direct `combatCanvasRoot` routing does not fight the root router in the active scene.

## 8. Remaining Risks

- Unity Play Mode validation was not run in this environment.
- `CombatDemoFlowController`, `CombatUIRootController`, `CombatPlanningHUD`, and `UIScreenRouter` can all affect combat UI visibility. This is documented but not fully centralized in this task.
- Legacy `BattleTrigger2D`, `BattleTransitionController`, and `SeamlessBattleManager` remain intact and are not treated as production combat entry.
- Debug tools still have direct test paths and are intentionally left unchanged.

## 9. Unity Play Mode Test Procedure

1. Open the main field/dungeon scene in Unity.
2. Press Play.
3. Confirm the scene has an active `GameStateMachine`.
4. Confirm the initial state is `Exploration`.
5. Start combat through a `FieldEnemy` or `CombatEncounterTrigger2D`.
6. Confirm `GameStateMachine.Current` becomes `CombatPlanning`.
7. Confirm player movement and field attack input stop during combat.
8. Confirm the combat root and `CombatPlanningHUD` appear.
9. Try to retrigger the same enemy/trigger while combat is active and confirm a second combat does not start.
10. End combat and confirm the existing reward/result UI appears.
11. Close reward UI and confirm the existing flow returns to `Exploration`.
12. Confirm debug combat start tools still work where they existed before.

Build command run:

```text
dotnet build Assembly-CSharp.csproj --no-restore --nologo
```

Build result:

```text
Exit code: 1
0 warnings
0 errors
```

This matches the previous Unity-generated project behavior in this repository: the available `dotnet` check reports no C# warnings or errors, but exits nonzero. Unity is not available on PATH here, so Unity Play Mode validation must be completed manually.

## 10. Completion Criteria

- Production combat starts route through `CombatEntryPoint`.
- `CombatEntryPoint.StartCombat(CombatStartRequest)` sets `GameState.CombatPlanning` when `GameStateMachine` exists.
- Exploration movement and field attack input are blocked by existing GameState/InputRouter guards during combat.
- Combat UI is activated through the existing GameState/UI event routing.
- Duplicate combat start is blocked while a combat session/state machine is active.
- Existing reward/result/exploration return flow is left intact.
- Existing debug and legacy combat tools are documented and left intact.
