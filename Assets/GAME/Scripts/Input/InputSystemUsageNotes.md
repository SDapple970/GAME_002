# Input System Usage Notes

## Current runtime paths

- `GameInputInstaller`
  - Creates the generated `GameInput` wrapper once and exposes gameplay events.
  - `OverworldPlayerController` currently reads movement from this instance and subscribes to jump/attack events.
  - `RebindSaveLoad` also depends on this singleton.

- `OverworldInputAdapter`
  - Reads `InputActionReference` assets directly and exposes `MoveX`, `JumpPressed`, and `AttackPressed`.
  - Intended for `PlayerController2D`/`IPlayerInput` style wiring, but it does not currently implement `IPlayerInput`.

- `PlayerInputUnity`
  - Uses the legacy `UnityEngine.Input` API.
  - Implements `IPlayerInput` for `PlayerController2D`.

- `OverworldInputBridge`
  - Uses `PlayerInput` callback methods and calls `PlayerMotor2D`/`OverworldAttack2D` directly.

- `OverworldAttack2D`
  - Can subscribe to an `InputActionReference` itself, and can also be driven externally through `RequestAttack()`.

## Recommended consolidation target

Use `GameInputInstaller` as the single runtime input source for the current project state.

Reasons:

- The generated `GameInput` wrapper already exists and is used by rebinding.
- `OverworldPlayerController` already depends on it for movement, jump, and attack.
- A single owner avoids duplicate action enables and duplicate attack calls.

## Duplicate-risk candidates

- `OverworldInputBridge` and `OverworldPlayerController` should not both drive the same player object.
- `OverworldAttack2D.attack` should be left empty if attack is routed through `OverworldPlayerController`.
- `PlayerController2D` + `PlayerInputUnity` is a separate legacy path and should not be active alongside `OverworldPlayerController`.
- `OverworldInputAdapter` should either implement `IPlayerInput` and replace `PlayerInputUnity`, or remain unused.

## Small follow-up

After confirming the scene uses `OverworldPlayerController`, remove or disable the unused player input components from prefabs/scenes in the Unity editor. This note intentionally does not edit scene or prefab YAML.
