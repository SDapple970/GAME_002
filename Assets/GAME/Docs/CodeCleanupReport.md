# Code Cleanup Report Draft

작성일: 2026-05-22

범위: `Assets/GAME/Scripts/Battle`, `Combat`, `Player`, `Input`, `UI`, `Common/Damage`

주의: 이 문서는 감사 초안이다. 실제 코드 삭제, 이동, namespace 변경, API 제거는 아직 수행하지 않았다.

## 요약

현재 프로젝트에는 신규 심리스 전투 루트와 초기 프로토타입 루트가 함께 남아 있다. 실사용 방향은 `CombatEntryPoint.StartCombatFromField(...)` 중심으로 정리하는 것이 맞다.

입력은 `GameInputInstaller + OverworldPlayerController + PlayerMotor2D + OverworldAttack2D` 조합이 현재 우선 루트다. 다만 `OverworldAttack2D`가 직접 `InputActionReference`를 구독할 수 있어, 같은 오브젝트에서 `OverworldPlayerController`가 `RequestAttack()`도 호출하면 공격 중복 가능성이 있다.

전투 UI는 `CombatEntryPoint.OnCombatStarted/OnCombatEnded` 구독 기반으로 대체로 정리되어 있다. `CombatStateSyncer`는 전투 시작 시 `GameState.Combat` 전환만 담당하고, 종료 시 `Exploration`으로 돌리지 않도록 이미 주석 처리되어 있어 현재 방향과 맞다.

디버그/테스트 클래스 일부가 `Combat/Runtime/Core`에 남아 있고, 테스트 씬에서 실제 참조 중이다. 즉시 삭제하지 말고 `Combat/Debugging` 또는 `Legacy`로 이동하거나 `UNITY_EDITOR` 보호를 적용하는 것이 안전하다.

## 유지한 코드 목록

다음 파일은 현재 방향과 맞으므로 유지 후보다.

- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatEntryPoint.cs`
  - 최종 전투 시작 API인 `StartCombatFromField(...)`를 제공한다.
  - `OnCombatStarted`, `OnCombatEnded` 이벤트의 중심이다.
  - F9/F10 강제 종료 코드는 이미 `#if UNITY_EDITOR` 안에 있다.

- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatStateMachine.cs`
  - 전투 페이즈 진행 책임을 가진다.

- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatTurnResolver.cs`
  - 턴 해석 책임을 가진다.

- `Assets/GAME/Scripts/Combat/Runtime/Effects/CombatDirector.cs`
  - 실제 필드 오브젝트 연출을 담당한다.
  - 현재는 상세 로그가 많으므로 추후 로그 레벨 정리 후보지만 구조는 유지.

- `Assets/GAME/Scripts/Combat/Runtime/Adapters/FieldCombatantAdapter.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Adapters/FieldCombatantFactory.cs`
  - 필드 오브젝트와 전투 데이터 연결 책임을 가진다.

- `Assets/GAME/Scripts/Combat/Runtime/UI/CombatPlanningHUD.cs`
  - 전투 시작/종료 이벤트를 구독해 Planning UI를 표시한다.
  - `OnEnable`에서 기존 구독을 제거한 뒤 다시 구독하므로 중복 구독 방어가 이미 있다.

- `Assets/GAME/Scripts/Combat/Runtime/UI/CombatWidgetManager.cs`
  - 전투 시작 시 위젯 생성, 종료 시 정리 책임을 가진다.

- `Assets/GAME/Scripts/Combat/Runtime/UI/CombatUIRootController.cs`
  - Combat UI root 표시/숨김을 `CombatEntryPoint` 이벤트 기반으로 처리한다.

- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatStateSyncer.cs`
  - 전투 시작 시 `GameState.Combat` 전환을 담당한다.
  - 종료 시 `Exploration`으로 강제 복귀하지 않도록 되어 있어 Reward UI 흐름과 충돌하지 않는다.

- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatEncounterGroup.cs`
  - 다수 적 조우 목록 수집용으로 유지 가능하다.

- `Assets/GAME/Scripts/Player/OverworldPlayerController.cs`
  - 현재 우선 입력 루트다.
  - `GameInputInstaller`에서 Jump/Attack 이벤트를 받고 이동값을 읽는다.

- `Assets/GAME/Scripts/Player/PlayerMotor2D.cs`
  - 이동/점프 실행 책임을 가진다.

- `Assets/GAME/Scripts/Player/PlayerAnimator2D.cs`
  - 유지 후보. 이번 감사에서 중복 책임은 확인되지 않았다.

- `Assets/GAME/Scripts/Player/Overworld/OverworldAttack2D.cs`
  - 공격 실행 컴포넌트로 유지하되 수정 필요.

- `Assets/GAME/Scripts/Input/GameInputInstaller.cs`
  - 현재 단일 입력 루트 후보.
  - `RebindSaveLoad`도 이 싱글톤에 의존한다.

- `Assets/GAME/Scripts/UI/RewardUIPanel.cs`
  - 전투 승리 후 `GameState.UIOnly`로 전환하고 보상 선택 후 `Exploration`으로 복귀한다.
  - 현재 `CombatStateSyncer`와 책임이 분리되어 있다.

## 수정한 코드 목록

### 1차 안전 정리 적용

- `Assets/GAME/Scripts/Combat/Runtime/UI/CombatPlanningHUD.cs`
  - `CombatEntryPoint` 이벤트 구독 상태를 `_subscribedToEntryPoint`로 추적하도록 보강.
  - `OnEnable`에서 중복 구독을 방지하고, `OnDisable`에서 `OnCombatStarted` / `OnCombatEnded`를 확실히 해제하도록 분리.
  - 슬롯/확정 버튼 리스너 등록/해제를 별도 메서드로 분리하고 `RemoveListener` 기반을 유지.
  - `Confirm()`에서 `_session.CurrentTurn == null`일 때 경고 후 중단하는 null 방어 추가.

- `Assets/GAME/Scripts/Player/Overworld/OverworldAttack2D.cs`
  - 기존 동작 보존을 위해 직접 `InputActionReference` 구독은 기본 활성 상태로 유지.
  - `GameInputInstaller`가 존재하는 상태에서 직접 입력 구독도 활성화되어 있으면 중복 공격 가능성 경고를 출력하도록 추가.
  - `RequestAttack()`이 권장 런타임 경로임을 주석으로 명시.

- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatEntryPoint.cs`
  - F9/F10 강제 승리/패배 호출부에 이어 `ForceFinishCombat(...)` 메서드 자체도 `#if UNITY_EDITOR` 안으로 제한.

- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatStateSyncer.cs`
  - `CombatEntryPoint` 자동 바인딩과 이벤트 중복 구독 방어 추가.
  - 전투 종료 시 `Exploration`으로 직접 복귀하지 않는 정책을 주석으로 명확화.

## 수정 후보

- `Assets/GAME/Scripts/Player/Overworld/OverworldAttack2D.cs`
  - 현재 `InputActionReference attack`을 직접 구독하고, `OverworldPlayerController`에서도 `RequestAttack()`을 호출한다.
  - 같은 Player에 둘 다 활성화되면 공격이 중복 실행될 수 있다.
  - 권장 수정: `RequestAttack()` 중심으로 유지하고 직접 입력 구독은 제거하거나 `useLegacyInputSubscription` 같은 기본 false 옵션으로 격리한다.
  - 공격 실행 전 `GameState.Exploration` 확인을 이 컴포넌트 안에도 넣으면 호출자 실수에 더 안전하다.

- `Assets/GAME/Scripts/Player/OverworldPlayerController.cs`
  - `OnGUI` 상태 표시가 Runtime 플로우에 남아 있다.
  - 권장 수정: `#if UNITY_EDITOR`로 보호하거나 Debugging 전용 컴포넌트로 분리한다.

- `Assets/GAME/Scripts/Combat/Runtime/UI/CombatPlanningHUD.cs`
  - 구독 중복 방어와 버튼 리스너 제거는 이미 되어 있다.
  - 다음 단계에서 null `entryPoint`일 때 `Debug.LogError` 반복 가능성을 줄이고, `AutoBindReferences()` 실패 시 한 번만 경고하도록 정리 가능.

- `Assets/GAME/Scripts/Combat/Runtime/UI/CombatWidgetManager.cs`
  - `OnEnable`에서 중복 구독 제거 없이 바로 `+=` 한다.
  - Unity enable/disable 흐름에서는 보통 문제 없지만, `CombatPlanningHUD`처럼 `-=` 후 `+=` 패턴으로 통일하면 더 안전하다.

- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatStateSyncer.cs`
  - namespace가 `Game.Integration`이다. 파일 위치는 `Game.Combat.Integration` 계열이므로 혼동 여지가 있다.
  - 단, namespace 변경은 참조 파손 위험이 있으므로 이번 정리에서는 변경하지 않는다.

- `Assets/GAME/Scripts/Combat/Runtime/Integration/EncounterAdvantageApplier.cs`
  - `BattleTransitionController.LastEncounterRequest`에 의존한다.
  - 신규 루트인 `CombatEntryPoint.StartCombatFromField(...)`의 `StartReason`, `initiativeSide`, `OpeningEffectSO` 구조와 중복된다.
  - 권장 수정: 구 `BattleTransitionRequest.Advantage` 의존을 제거하고 `CombatSession.StartReason` / `InitiativeSide` 기반으로 판단하거나 Legacy 후보로 분류한다.

- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatEncounterTrigger2D.cs`
  - `StartCombatFromField(...)`를 직접 호출하는 실사용 후보.
  - `FieldEnemy`와 동시에 같은 적 오브젝트에 붙으면 전투가 두 번 시작될 수 있다.
  - 권장 방향: 최종 진입 루트를 `CombatEncounterTrigger2D` 또는 `FieldEnemy` 중 하나로 통일한다.

- `Assets/GAME/Scripts/Combat/FieldEnemy.cs`
  - `StartCombatFromField(...)`를 직접 호출하고, 접촉/피격 양쪽 시작을 모두 처리한다.
  - `CombatEncounterTrigger2D`와 책임이 겹친다.
  - 권장 방향: 적 AI/피격 선공 처리까지 필요하면 `FieldEnemy`를 유지하고 `CombatEncounterTrigger2D`는 단순 트리거용으로만 제한한다. 반대로 조우 데이터 구성이 우선이면 `CombatEncounterTrigger2D`를 표준으로 삼고 `FieldEnemy`의 전투 시작 책임은 제거한다.

- `Assets/GAME/Scripts/UI/RewardUIPanel.cs`
  - `HandleCombatEnded` 초반에 `if (!result.IsWin) return;` 후 아래에서 다시 `if (!result.IsWin)`를 검사하는 중복 코드가 있다.
  - 상태 흐름은 맞지만 로그와 검증 순서 정리는 가능하다.

## Legacy 이동 후보

다음 파일은 삭제보다 `Assets/GAME/Scripts/Legacy/` 또는 테스트 전용 폴더로 이동하는 것이 안전하다. 일부는 씬 참조가 확인되었다.

- `Assets/GAME/Scripts/Battle/BattleTrigger2D.cs`
  - `BattleTrigger2D.OnBattleRequested` 이벤트만 발행하고 `CombatEntryPoint.StartCombatFromField(...)`를 호출하지 않는다.
  - 신규 심리스 전투 루트와 별개인 구 전투 진입 방식이다.
  - guid 기반 씬/프리팹 참조는 이번 검색에서 발견되지 않았다.

- `Assets/GAME/Scripts/Battle/SeamlessBattleManager.cs`
  - `BattleTrigger2D.OnBattleRequested`를 받아 `GameState.Combat`로 바꾸고 `combatUIRoot.SetActive(true)`만 수행한다.
  - 실제 `CombatEntryPoint`를 시작하지 않는다.
  - 이름 기준으로 `Assets/GAME/Scenes/Test.unity`에 오브젝트가 남아 있다. guid 직접 참조는 검색되지 않았지만 즉시 삭제는 비권장.

- `Assets/GAME/Scripts/UI/BattleTransitionController.cs`
  - `BattleTrigger2D.OnBattleRequested`를 받아 `CombatTransition`/씬 로드/`Combat` 전환을 처리한다.
  - `CombatEntryPoint`를 거치지 않는다.
  - `Assets/GAME/Scenes/Test.unity`에서 guid 참조가 확인되었다.
  - `LastEncounterRequest`는 `EncounterAdvantageApplier`가 참조 중이므로 바로 삭제하면 안 된다.

- `Assets/GAME/Scripts/Player/PlayerController2D.cs`
  - `IPlayerInput` 기반 구 컨트롤러.
  - 현재 우선 루트인 `OverworldPlayerController`와 동시에 쓰면 이동/점프 중복 가능성이 있다.
  - guid 씬/프리팹 참조는 이번 검색에서 발견되지 않았다.

- `Assets/GAME/Scripts/Player/PlayerInputUnity.cs`
  - `UnityEngine.Input` 기반 구 입력.
  - Input System 기준과 충돌 가능성이 있다.
  - `PlayerController2D`와 세트로 Legacy 후보.

- `Assets/GAME/Scripts/Input/OverworldInputAdapter.cs`
  - `InputActionReference`를 직접 구독하지만 `IPlayerInput`을 구현하지 않는다.
  - 현재 우선 루트와 병행하면 중복 입력 경로가 된다.

- `Assets/GAME/Scripts/Player/Overworld/OverworldInputBridge.cs`
  - `PlayerInput` Send Messages 방식 콜백으로 `PlayerMotor2D`와 `OverworldAttack2D`를 직접 호출한다.
  - `OverworldPlayerController`와 동시에 붙으면 이동/공격 중복 가능성이 있다.

- `Assets/GAME/Scripts/Player/Overworld/OverworldPlayerGlue.cs`
  - reflection 기반 임시 글루.
  - namespace가 `GAME.Player.Overworld`로 기존 `Game.Player`와도 다르다.
  - Runtime 표준 경로로 유지하기에는 위험하다.

- `Assets/GAME/Scripts/Player/Overworld/OverworldPlayerDriver.cs`
  - 비어 있는 컴파일 방지용 클래스다.
  - guid 씬/프리팹 참조는 이번 검색에서 발견되지 않았다.

- `Assets/GAME/Scripts/Common/Damage/SimpleDamageable.cs`
  - `IDamageable` 테스트 구현으로 보인다.
  - `OverworldAttack2D`의 타격 테스트에는 유용하지만 실제 적은 `FieldEnemy`가 `IDamageable`을 구현한다.
  - 씬/프리팹 guid 참조는 이번 검색에서 발견되지 않았다. 삭제 전 한 번 더 확인 필요.

## Debugging 이동 또는 UNITY_EDITOR 보호 후보

- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatFieldCallDebug.cs`
  - F1/F2/F3 입력으로 `StartCombatFromField(...)`를 직접 호출한다.
  - `Assets/GAME/Scenes/Test.unity`에서 guid 참조가 확인되었다.
  - `Assets/GAME/Scripts/Combat/Debugging/`로 이동하거나 `#if UNITY_EDITOR` 보호 권장.

- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatStartSmokeTest.cs`
  - `Start()`에서 더미 오브젝트를 만들고 전투를 자동 시작한다.
  - Runtime 씬에 남으면 실제 플로우를 오염시킨다.
  - `Assets/GAME/Scenes/Test.unity`에서 guid 참조가 확인되었다.

- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatTestRunner.cs`
  - 더미 전투 세션을 만들고 Space로 테스트 진행한다.
  - `Assets/GAME/Combat/Tests/Scenes/CombatTest.unity`에서 guid 참조가 확인되었다.
  - 테스트 씬 전용으로 유지하거나 Debugging/Tests 폴더로 이동 권장.

- `Assets/GAME/Scripts/Combat/Runtime/Core/InspirationDebugHotkey.cs`
  - G/H 키로 영감을 직접 조작한다.
  - guid 씬/프리팹 참조는 이번 검색에서 발견되지 않았다.
  - Debugging 이동 또는 `#if UNITY_EDITOR` 보호 권장.

- `Assets/GAME/Scripts/Combat/CombatSkillDebugInvoker.cs`
  - 이미 namespace는 `Game.Combat.Debugging`이나 파일 위치는 `Combat` 루트다.
  - `Combat/Debugging` 폴더로 이동 후보.

- `Assets/GAME/Scripts/Combat/Debugging/CombatAutoPlanner.cs`
  - 이미 Debugging 폴더에 있다.
  - 자동 제출 기능이 있으므로 실제 런타임 씬에 붙지 않도록 Inspector 점검 필요.

## 삭제 후보

이번 감사 초안 단계에서는 즉시 삭제 후보를 확정하지 않는다.

상대적으로 삭제 가능성이 높은 파일:

- `Assets/GAME/Scripts/Player/Overworld/OverworldPlayerDriver.cs`
  - 빈 클래스이며 씬/프리팹 guid 참조가 발견되지 않았다.
  - 다음 단계에서 한 번 더 참조 확인 후 삭제 가능.

그 외 MonoBehaviour 파일은 씬/프리팹 참조 파손 위험이 있으므로 Legacy 이동을 우선한다.

## 중복 책임 설명

### 전투 진입 경로

현재 전투 진입 경로는 세 계열로 나뉜다.

1. 신규 심리스 직접 시작
   - `CombatEncounterTrigger2D`
   - `FieldEnemy`
   - 둘 다 `CombatEntryPoint.StartCombatFromField(...)`를 호출한다.

2. 구 이벤트 기반 Battle 루트
   - `BattleTrigger2D`
   - `BattleTransitionController`
   - `SeamlessBattleManager`
   - 이 루트는 `CombatEntryPoint`를 시작하지 않고 상태/UI/씬 전환만 처리한다.

3. 디버그 직접 시작
   - `CombatFieldCallDebug`
   - `CombatStartSmokeTest`
   - `CombatTestRunner`

최종 루트는 1번으로 통일해야 한다. 2번은 Legacy 후보, 3번은 Debugging/Editor 전용 후보로 분리한다.

### 입력 경로

현재 입력 경로는 다음처럼 중복된다.

- 표준 후보: `GameInputInstaller -> OverworldPlayerController -> PlayerMotor2D / OverworldAttack2D.RequestAttack()`
- 구 Input 후보: `PlayerInputUnity -> PlayerController2D -> PlayerMotor2D`
- InputActionReference 후보: `OverworldInputAdapter`
- PlayerInput Send Messages 후보: `OverworldInputBridge`
- reflection 후보: `OverworldPlayerGlue`
- 공격 자체 구독: `OverworldAttack2D.InputActionReference attack`

최종 입력 루트는 `GameInputInstaller + OverworldPlayerController + PlayerMotor2D + OverworldAttack2D`로 통일하는 것이 맞다.

### UI/상태 전환

- `CombatStateSyncer`: 전투 시작 시 `GameState.Combat` 전환.
- `RewardUIPanel`: 전투 종료 승리 시 `GameState.UIOnly`, 보상 선택 후 `Exploration`.
- `CombatUIRootController`: Combat UI root 표시/숨김.
- `CombatPlanningHUD`: Planning panel 표시 및 버튼 생성.
- `SeamlessBattleManager`: 구 방식으로 `GameState.Combat`와 `combatUIRoot.SetActive(true)`를 직접 수행.
- `BattleTransitionController`: 구 방식으로 `CombatTransition`과 `Combat` 상태 전환.

`SeamlessBattleManager`와 `BattleTransitionController`는 신규 UI/상태 흐름과 충돌 후보다.

## 중복 클래스 선언 검사 결과

실제 `Assets/GAME/Scripts/**/*.cs` 기준으로 다음 클래스/인터페이스 중복 선언은 발견되지 않았다.

- `EncounterAdvantageApplier`
- `CombatStateMachine`
- `SkillRunner`
- `SoSkill`
- `PlayerMotor2D`
- `OverworldAttack2D`
- `IDamageable`
- `SimpleDamageable`
- `GameInputInstaller`

`Assets/GAME/Scripts/MyProject_All_Code.txt`에는 같은 이름들이 포함되어 있으나 문서/집계 파일로 판단하여 중복 선언 대상에서 제외했다.

## 최종 전투 진입 루트 제안

권장 표준:

`CombatEncounterTrigger2D` 또는 `FieldEnemy` 중 하나만 실사용 루트로 정한다.

안전한 선택지는 다음과 같다.

- 단순 접촉/조우 중심: `CombatEncounterTrigger2D -> CombatEntryPoint.StartCombatFromField(...)`
- 적 추적 + 플레이어 공격 선공/피격 후공까지 포함: `FieldEnemy -> CombatEntryPoint.StartCombatFromField(...)`

둘을 같은 적 오브젝트에 동시에 붙이지 않는다. `BattleTrigger2D`, `BattleTransitionController`, `SeamlessBattleManager`는 신규 전투 루트에서 제외한다.

## 최종 입력 루트 제안

권장 표준:

`GameInputInstaller -> OverworldPlayerController -> PlayerMotor2D / OverworldAttack2D.RequestAttack()`

Inspector에서 같은 Player 오브젝트에 다음 컴포넌트가 함께 붙지 않도록 한다.

- `PlayerController2D`
- `PlayerInputUnity`
- `OverworldInputBridge`
- `OverworldInputAdapter`
- `OverworldPlayerGlue`

`OverworldAttack2D.attack` 필드는 비워두거나, 다음 수정 단계에서 직접 구독 기능을 비활성화한다.

## Unity Inspector에서 다시 연결해야 할 항목

다음 단계에서 실제 정리 후 확인할 항목:

- Player 오브젝트에 남길 컴포넌트
  - `GameInputInstaller`는 전역 오브젝트에 1개
  - Player에는 `OverworldPlayerController`, `PlayerMotor2D`, `PlayerAnimator2D`, `OverworldAttack2D`

- Player 오브젝트에서 제거 또는 비활성화할 레거시 후보
  - `PlayerController2D`
  - `PlayerInputUnity`
  - `OverworldInputBridge`
  - `OverworldInputAdapter`
  - `OverworldPlayerGlue`
  - `OverworldPlayerDriver`

- `OverworldAttack2D`
  - `hitOrigin`, `targetMask`, `cooldown`, `damage` 유지 확인
  - 표준 루트에서는 `attack` InputActionReference를 비우거나, 직접 입력 구독 옵션을 꺼야 함
  - `GameInputInstaller + OverworldPlayerController`를 쓰는 씬에서 `OverworldAttack2D` 경고가 뜨면 중복 입력 경로가 남아 있다는 뜻

- Combat UI
  - `CombatPlanningHUD.entryPoint`
  - `CombatWidgetManager.entryPoint`
  - `CombatUIRootController.entryPoint`
  - `CombatInspirationHUD.entryPoint`
  - `CombatLogHUD.entryPoint`

- 상태 동기화
  - `CombatStateSyncer.entryPoint`
  - 전투 종료 후 `CombatStateSyncer`가 `Exploration`으로 직접 복귀하지 않는지 확인

- 전투 연출
  - `CombatDirector`는 `CombatEntryPoint.director` 또는 이벤트 연결 상태 확인

- 전투 진입 트리거
  - 표준으로 정한 트리거에 `CombatEntryPoint` 연결
  - `CombatEncounterTrigger2D`와 `FieldEnemy`가 같은 오브젝트에서 동시에 시작하지 않도록 확인

- Reward
  - `RewardUIPanel.combatEntryPoint`
  - `RewardUIPanel.rewardApplier`
  - `RewardUIPanel.rewardPanelRoot`
  - 보상 UI 닫기 전까지 `GameState.UIOnly`, 선택 후 `Exploration` 복귀 확인

## 남은 TODO

1. 실제 씬/프리팹에서 Player와 적 오브젝트의 컴포넌트 구성을 Unity Inspector로 확인한다.
2. `OverworldAttack2D` 직접 입력 구독을 제거하거나 legacy 옵션으로 비활성화한다.
3. `PlayerController2D`, `PlayerInputUnity`, `OverworldInputBridge`, `OverworldInputAdapter`, `OverworldPlayerGlue`, `OverworldPlayerDriver`의 씬 참조를 재확인한 뒤 Legacy 이동 여부를 확정한다.
4. `BattleTrigger2D`, `SeamlessBattleManager`, `BattleTransitionController`를 Legacy로 이동하기 전에 `EncounterAdvantageApplier`의 `LastEncounterRequest` 의존을 제거한다.
5. `CombatFieldCallDebug`, `CombatStartSmokeTest`, `CombatTestRunner`, `InspirationDebugHotkey`를 Debugging/Tests 폴더로 이동하거나 `UNITY_EDITOR`로 보호한다.
6. `CombatWidgetManager` 이벤트 구독 패턴을 `-=` 후 `+=`로 통일한다.
7. `RewardUIPanel.HandleCombatEnded` 중복 검사와 로그 순서를 정리한다.

## 테스트 절차

실제 수정 단계 후 다음을 실행한다.

### A. 컴파일 테스트

- Unity Console에 compile error 0개.

### B. 탐험 입력 테스트

- `Exploration` 상태에서 이동 가능.
- 점프 가능.
- 공격 가능.
- 공격 버튼 1회 입력 시 공격 1회만 실행.

### C. 전투 진입 테스트

- 적과 접촉 시 전투가 한 번만 시작.
- 플레이어 선공 공격 시 전투가 한 번만 시작.
- `GameState`가 `Combat`으로 변경.
- 전투 중 이동/공격 입력 차단.

### D. 전투 UI 테스트

- `CombatPlanningHUD`가 전투 시작 시 표시.
- 스킬 버튼/타겟 버튼 중복 생성 없음.
- Confirm 후 Resolution으로 진행.
- `CombatWidgetManager`가 위젯을 한 번만 생성.
- 전투 종료 시 위젯 정리.

### E. 보상/종료 테스트

- 전투 종료 시 `RewardUIPanel` 또는 `UIOnly` 상태로 정상 진입.
- 보상 UI 닫은 뒤 `Exploration` 복귀.
- 전투 종료 직후 같은 적에게 즉시 재진입하지 않음.

## 2차 참조 검사 결과

검사일: 2026-05-22

주의: 이 섹션은 참조 검사 결과만 기록한다. 이번 단계에서 파일 삭제, 이동, namespace 변경, 클래스명 변경, 실제 코드 수정은 수행하지 않았다.

검사 방식:

- 클래스 선언 위치: `Assets/GAME/Scripts/**/*.cs`에서 class 이름 검색.
- 다른 스크립트 참조: `Assets/GAME/Scripts/**/*.cs`에서 class 이름 검색.
- 씬/프리팹/에셋 참조: 각 `.cs.meta`의 `guid`를 `Assets/GAME/**/*.unity`, `*.prefab`, `*.asset`에서 검색.
- 보조 확인: 씬/프리팹/에셋 내 class 이름 문자열 검색.

| 대상 | .cs 위치 | 다른 스크립트 참조 | 씬/프리팹/에셋 참조 | 현재 필요성 판단 | 권장 분류 |
| --- | --- | --- | --- | --- | --- |
| `BattleTrigger2D` | `Assets/GAME/Scripts/Battle/BattleTrigger2D.cs` | `BattleTransitionController`, `SeamlessBattleManager`가 `OnBattleRequested` 구독 | guid 참조 없음, 이름 문자열 참조 없음 | 현재 표준 전투 시작 루트인 `CombatEntryPoint.StartCombatFromField(...)`를 호출하지 않음 | 바로 삭제 금지. `BattleTransitionController`/`SeamlessBattleManager` 정리와 함께 Legacy 이동 후보 |
| `SeamlessBattleManager` | `Assets/GAME/Scripts/Battle/SeamlessBattleManager.cs` | 직접 참조 없음. 내부에서 `BattleTrigger2D.OnBattleRequested` 구독 | guid 참조 없음. `Assets/GAME/Scenes/Test.unity`에 `m_Name: SeamlessBattleManager` 존재 | `GameState.Combat` 전환과 `combatUIRoot.SetActive(true)`만 수행하고 `CombatEntryPoint`를 시작하지 않음 | Legacy 이동 후보. Test 씬 오브젝트 정리 후 이동 |
| `BattleTransitionController` | `Assets/GAME/Scripts/UI/BattleTransitionController.cs` | `EncounterAdvantageApplier`가 `BattleTransitionController.LastEncounterRequest` 참조 | `Assets/GAME/Scenes/Test.unity`에서 guid 참조 확인 | 구 `BattleTrigger2D` 기반 상태/씬 전환 루트. 현재 심리스 전투 시작 루트와 분리됨 | 유지 또는 Legacy 이동 후보. 이동 전 `EncounterAdvantageApplier` 의존 제거 필요 |
| `PlayerController2D` | `Assets/GAME/Scripts/Player/PlayerController2D.cs` | 주석/문서성 참조만 확인. 직접 코드 호출 없음 | guid 참조 없음, 이름 문자열 참조 없음 | `IPlayerInput` 기반 구 이동 루트. 현재 표준 입력 루트와 병행하면 중복 위험 | Legacy 이동 후보. 씬 Inspector 최종 확인 후 이동 가능 |
| `PlayerInputUnity` | `Assets/GAME/Scripts/Player/PlayerInputUnity.cs` | `OverworldPlayerGlue` 주석/툴팁에서 언급. 직접 코드 호출 없음 | guid 참조 없음, 이름 문자열 참조 없음 | `UnityEngine.Input` 기반 구 입력 구현 | Legacy 이동 후보. `PlayerController2D`와 세트로 이동 가능 |
| `OverworldInputBridge` | `Assets/GAME/Scripts/Player/Overworld/OverworldInputBridge.cs` | 직접 참조 없음 | guid 참조 없음, 이름 문자열 참조 없음 | `PlayerInput` Send Messages 방식으로 `PlayerMotor2D`/`OverworldAttack2D`를 직접 호출하는 별도 입력 루트 | Legacy 이동 후보. 표준 입력 루트와 동시 사용 금지 |
| `OverworldInputAdapter` | `Assets/GAME/Scripts/Input/OverworldInputAdapter.cs` | `OverworldPlayerGlue` 주석/툴팁에서 언급. 직접 코드 호출 없음 | guid 참조 없음, 이름 문자열 참조 없음 | `InputActionReference` 직접 구독 루트. `IPlayerInput` 미구현 상태 | Legacy 이동 후보 또는 향후 재설계 후보 |
| `OverworldPlayerGlue` | `Assets/GAME/Scripts/Player/Overworld/OverworldPlayerGlue.cs` | 직접 참조 없음 | guid 참조 없음, 이름 문자열 참조 없음 | reflection 기반 임시 연결 코드. namespace도 `GAME.Player.Overworld`로 기존 `Game.Player`와 다름 | Legacy 이동 후보 |
| `OverworldPlayerDriver` | `Assets/GAME/Scripts/Player/Overworld/OverworldPlayerDriver.cs` | 직접 참조 없음 | guid 참조 없음, 이름 문자열 참조 없음 | 빈 컴파일 방지용 클래스 | 삭제 후보. 단, 다음 단계에서도 한 번 더 씬/프리팹 확인 후 삭제 |
| `CombatFieldCallDebug` | `Assets/GAME/Scripts/Combat/Runtime/Core/CombatFieldCallDebug.cs` | 직접 참조 없음 | `Assets/GAME/Scenes/Test.unity`에서 guid 참조 및 이름 문자열 참조 확인 | F1/F2/F3로 `StartCombatFromField(...)`를 직접 호출하는 디버그 진입 루트 | 삭제 금지. Debugging 이동 또는 `UNITY_EDITOR` 보호 후보. Test 씬 참조 정리 필요 |
| `CombatStartSmokeTest` | `Assets/GAME/Scripts/Combat/Runtime/Core/CombatStartSmokeTest.cs` | 직접 참조 없음 | `Assets/GAME/Scenes/Test.unity`에서 guid 참조 확인 | `Start()`에서 더미 전투를 자동 시작하는 smoke test | 삭제 금지. Debugging/Tests 이동 또는 `UNITY_EDITOR` 보호 후보. Test 씬 참조 정리 필요 |
| `CombatTestRunner` | `Assets/GAME/Scripts/Combat/Runtime/Core/CombatTestRunner.cs` | 주석성 참조만 확인 (`DummyCombatantFactory`, `CombatStartSmokeTest`) | `Assets/GAME/Combat/Tests/Scenes/CombatTest.unity`에서 guid 참조 확인 | 테스트 씬 전용 더미 전투 러너 | 유지 또는 Tests/Debugging 이동 후보. 테스트 씬 참조 유지 여부 결정 필요 |
| `InspirationDebugHotkey` | `Assets/GAME/Scripts/Combat/Runtime/Core/InspirationDebugHotkey.cs` | 직접 참조 없음 | guid 참조 없음, 이름 문자열 참조 없음 | G/H 키로 영감을 조작하는 디버그 핫키 | Debugging 이동 또는 `UNITY_EDITOR` 보호 후보. 씬 참조 없으므로 이동 난이도 낮음 |
| `CombatSkillDebugInvoker` | `Assets/GAME/Scripts/Combat/CombatSkillDebugInvoker.cs` | 직접 참조 없음 | `Assets/GAME/Scenes/Test.unity`에서 guid 참조 및 이름 문자열 참조 확인 | 이미 namespace는 `Game.Combat.Debugging`이나 파일 위치가 Combat 루트 | 삭제 금지. `Combat/Debugging` 이동 후보. Test 씬 참조 정리 또는 meta 유지 이동 필요 |

### 2차 검사 결론

- 바로 삭제 가능성이 가장 높은 대상은 `OverworldPlayerDriver` 하나뿐이다. 그래도 public `MonoBehaviour`이므로 실제 삭제 전 Unity Inspector에서 한 번 더 확인한다.
- `PlayerController2D`, `PlayerInputUnity`, `OverworldInputBridge`, `OverworldInputAdapter`, `OverworldPlayerGlue`는 guid 기반 씬/프리팹 참조가 발견되지 않았다. 다음 단계에서 Legacy 이동하기 좋은 후보지만, 입력 루트 교체 금지 원칙 때문에 실제 이동 전 현재 Player 프리팹/씬 구성을 Unity에서 확인해야 한다.
- `BattleTransitionController`, `CombatFieldCallDebug`, `CombatStartSmokeTest`, `CombatTestRunner`, `CombatSkillDebugInvoker`는 씬 참조가 확인되었으므로 삭제하면 안 된다. 이동하려면 `.meta`를 유지한 이동 또는 씬 참조 정리가 필요하다.
- `BattleTrigger2D`는 씬 참조는 없지만 `BattleTransitionController`와 `SeamlessBattleManager`가 구독한다. 이 계열은 한 번에 Legacy 처리해야 한다.
- `SeamlessBattleManager`는 guid 참조는 없지만 `Test.unity`에 같은 이름의 오브젝트가 남아 있다. 컴포넌트가 붙어 있지 않은 빈 오브젝트일 수 있으므로 Unity Inspector 확인이 필요하다.

### 2차 정리 전 권장 순서

1. `Test.unity`에서 `BattleTransitionController`, `CombatFieldCallDebug`, `CombatStartSmokeTest`, `CombatSkillDebugInvoker`가 붙은 오브젝트를 확인한다.
2. `CombatTest.unity`에서 `CombatTestRunner`가 테스트 씬 전용인지 확인한다.
3. 현재 플레이어 오브젝트/프리팹에 레거시 입력 컴포넌트가 없는지 Inspector에서 확인한다.
4. `EncounterAdvantageApplier`의 `BattleTransitionController.LastEncounterRequest` 의존 제거 계획을 먼저 세운다.
5. 그 다음 `BattleTrigger2D` 계열과 입력 레거시 계열을 Legacy 이동 또는 삭제 대상으로 분리한다.
