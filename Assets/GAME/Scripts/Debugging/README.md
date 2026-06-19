# Debugging Scripts

This folder contains optional development tools, debug hotkeys, smoke tests, and scene validators.

Runtime gameplay must not depend on `Game.Debugging` scripts. Debug roots should be removable from a scene without disabling the normal game loop.

Combat debug scripts were moved here from `Assets/GAME/Scripts/Combat/Debugging` and `Assets/GAME/Scripts/Combat/CombatSkillDebugInvoker.cs` with their `.meta` files preserved.
