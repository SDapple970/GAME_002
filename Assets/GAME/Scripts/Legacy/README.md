# Legacy Scripts

This folder contains compatibility scripts that are no longer official production entry points.

- `Battle/` is the old battle request path. New combat starts should use `Game.Combat.Core.CombatEntryPoint.StartCombatFromField(...)` through field encounter adapters.
- `StoryDeprecated/` contains replaced story flag code kept for reference and safe Unity migration.

Do not add new runtime dependencies on this folder. If a scene still references these scripts, migrate the scene to the production owner first, then remove the legacy component.
