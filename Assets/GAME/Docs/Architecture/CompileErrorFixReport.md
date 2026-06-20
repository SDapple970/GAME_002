# Compile Error Fix Report

Date: 2026-06-20

## 1. Git Status Before And After

Before:

```text
?? Assets/GAME/Docs/Architecture/SceneWiringValidationReport.md.meta
```

After:

```text
A  Assets/GAME/Docs/Architecture/CompileErrorFixReport.md
A  Assets/GAME/Docs/Architecture/CompileErrorFixReport.md.meta
A  Assets/GAME/Docs/Architecture/SceneWiringValidationReport.md.meta
 M Assets/GAME/Scripts/Cutscene/Runtime/MissionCompleteCutsceneController.cs
 M Assets/GAME/Scripts/Debugging/Combat/CombatSkillDebugInvoker.cs
 M Assets/GAME/Scripts/Debugging/Combat/CombatTestRunner.cs
 M Assets/GAME/Scripts/Debugging/Combat/InspirationDebugHotkey.cs
 M Assets/GAME/Scripts/Debugging/StoryInteractionDebugHotkey.cs
 M Assets/GAME/Scripts/Debugging/VerticalSliceSceneValidator.cs
 M Assets/GAME/Scripts/DemoMission/Runtime/Ending/DemoRescueNpcEndFlow.cs
 M Assets/GAME/Scripts/NonCombat/Save/SaveManager.cs
 M Assets/GAME/Scripts/Office/OfficeMenuController.cs
 M Assets/GAME/Scripts/Player/PlayerInputUnity.cs
 M Assets/GAME/Scripts/Search/Runtime/SearchableInteractable2D.cs
 M Assets/GAME/Scripts/Search/Runtime/UI/ItemAcquisitionHUD.cs
 M Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionController.cs
 M Assets/GAME/Scripts/Story/Runtime/UI/TimedChoicePanel.cs
 M Assets/GAME/Scripts/Story/Runtime/World/StoryDialogueTrigger2D.cs
```

## 2. Unity Log Availability

Unity Editor log was found at:

```text
C:/Users/eunio/AppData/Local/Unity/Editor/Editor.log
```

The latest available log diagnostics were CS0234 errors caused by legacy `Input.*` calls resolving against the new `Game.Input` namespace.

## 3. Compile Command Used

Unity batchmode command attempted:

```text
Unity.exe -batchmode -quit -projectPath C:/Users/eunio/OneDrive/문서/GitHub/GAME_002 -logFile Logs/CodexUnityCompile.log
```

Result: blocked because another Unity instance already had the project open.

Static Unity compiler response-file check used:

```text
mono.exe csc.exe @Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp.rsp -out:Temp/CodexCompile/Assembly-CSharp.dll -refout:Temp/CodexCompile/Assembly-CSharp.ref.dll
```

## 4. Compile Result

The response-file compile completed with exit code `0` and no C# errors.

`dotnet build Assembly-CSharp.csproj` was attempted, but the generated Unity project failed without actionable compiler diagnostics. It was not used as the authoritative check.

## 5. Errors Found

Unity log errors were all variants of:

```text
CS0234: The type or namespace name 'GetKeyDown' does not exist in the namespace 'Game.Input'
CS0234: The type or namespace name 'GetMouseButtonDown' does not exist in the namespace 'Game.Input'
CS0234: The type or namespace name 'mousePosition' does not exist in the namespace 'Game.Input'
CS0234: The type or namespace name 'GetAxisRaw' does not exist in the namespace 'Game.Input'
CS0234: The type or namespace name 'GetButton' does not exist in the namespace 'Game.Input'
CS0234: The type or namespace name 'GetButtonDown' does not exist in the namespace 'Game.Input'
```

Cause: files under `namespace Game.*` used unqualified legacy `Input.*` calls after the new `Game.Input` namespace was introduced.

## 6. Files Changed

Changed compile-fix files:

- `Assets/GAME/Scripts/Cutscene/Runtime/MissionCompleteCutsceneController.cs`
- `Assets/GAME/Scripts/Debugging/Combat/CombatSkillDebugInvoker.cs`
- `Assets/GAME/Scripts/Debugging/Combat/CombatTestRunner.cs`
- `Assets/GAME/Scripts/Debugging/Combat/InspirationDebugHotkey.cs`
- `Assets/GAME/Scripts/Debugging/StoryInteractionDebugHotkey.cs`
- `Assets/GAME/Scripts/Debugging/VerticalSliceSceneValidator.cs`
- `Assets/GAME/Scripts/DemoMission/Runtime/Ending/DemoRescueNpcEndFlow.cs`
- `Assets/GAME/Scripts/NonCombat/Save/SaveManager.cs`
- `Assets/GAME/Scripts/Office/OfficeMenuController.cs`
- `Assets/GAME/Scripts/Player/PlayerInputUnity.cs`
- `Assets/GAME/Scripts/Search/Runtime/SearchableInteractable2D.cs`
- `Assets/GAME/Scripts/Search/Runtime/UI/ItemAcquisitionHUD.cs`
- `Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionController.cs`
- `Assets/GAME/Scripts/Story/Runtime/UI/TimedChoicePanel.cs`
- `Assets/GAME/Scripts/Story/Runtime/World/StoryDialogueTrigger2D.cs`

Meta/doc files:

- `Assets/GAME/Docs/Architecture/SceneWiringValidationReport.md.meta`
- `Assets/GAME/Docs/Architecture/CompileErrorFixReport.md`
- `Assets/GAME/Docs/Architecture/CompileErrorFixReport.md.meta`

## 7. Fix Applied Per Error

All affected legacy input calls were changed from unqualified `Input.*` to `UnityEngine.Input.*`.

No architecture refactor was performed. No systems were renamed, deleted, moved, or split.

## 8. .meta Files Handled

`Assets/GAME/Docs/Architecture/SceneWiringValidationReport.md.meta` belongs to an existing tracked markdown asset and was staged as a valid Unity meta file.

`CompileErrorFixReport.md.meta` was created with the new report so the new Unity doc asset has a paired meta file.

No temporary upload `.meta` paths were found.

## 9. Remaining Warnings

Static compile warnings:

- Unity source-generator analyzer host warnings when running Roslyn standalone through Mono.
- `TimedChoiceDialoguePanel.cs`: obsolete `TMP_Text.enableWordWrapping`.
- `RescueNpcActor.cs`: `interactKey` assigned but not used.

`git diff --check` reported no whitespace errors. It did report Git line-ending normalization warnings for several touched files.

## 10. Remaining Unity Editor Checks

Open Unity Editor or close the existing editor instance and rerun batchmode compile. The previous batchmode attempt could not run because the project was already open in another Unity process.

## 11. Errors Not Fixed And Why

No known C# compile errors remain from static compiler validation.

The stale global Unity Editor log still contains the old CS0234 errors because batchmode could not open the project and the log was not refreshed after these edits.

## 12. Recommended Next Task

Run a Unity Editor compile after closing duplicate project instances, then address non-blocking warnings separately. Keep any follow-up runtime wiring or save/load work out of this compile-fix task.
