# CBT-R2-08-P2A — Hand/BP 초기화 검증과 P2-B 연결 계획

2026-10-29 KST. **보호 Scene/Prefab/SO 무변경. Production Cutover 미수행.** 코드 기능 검증과 실제 Production 자동 초기화를 구분한다.

## 결과와 한계

기존 `CombatSession.TryEnableHandValidation` / `TryEnableBpValidation`은 Debug 타입 없이 실행 가능한 공통 초기화 구현이다. Hand/BP/Ultimate의 상태 소유권도 이미 Session에 있다. 동일 기능의 새 Runtime API·Manager·initializer를 추가하지 않고, 기존 API를 실제 snapshot/factory와 함께 검증하는 독립 fixture 5개를 추가했다.

**자동 초기화 연결은 아직 없다.** 현재 Production에서 위 API를 호출하는 곳은 Hand/BP Debug opt-in뿐이다. `CombatEntryPoint` → `CombatBootstrapper`의 정식 시작 경로는 Session/StateMachine/MP/Posture/Mental을 만들지만 Hand/BP를 자동으로 설정하지 않는다. 기존 FinalCombatRuntimeConfigSO에는 Hand/BP/Clash authoring 필드가 없다. 따라서 이번 통과는 공통 초기화 기능의 충분성 증거이며, Debug 컴포넌트를 지금 제거해도 모든 기능이 자동 유지된다는 증거가 아니다.

정식 카드 분류/복제 수/용량/seed 정책, BP 초기값/주기/강화와 Ultimate 연결, 반복 합 config·timeout 선택 정책은 미승인 상태다. 이를 fixture 수치로 확정하거나 Debug 이벤트를 정식 Owner로 승격하지 않았다. P2-B의 단순 컴포넌트 제거 전 승인 데이터와 기존 Owner의 호출 연결 계약이 필요하다. 그 자동 연결 구현은 이번에 추가하지 않았으므로 해당 부분은 Contract/Implementation Pending이다.

## 지침·Git·환경

- 적용 지침: 루트 `AGENTS.md`; 중첩 지침 없음.
- Branch `main`, HEAD `6fa26d294472574097d33b884858ba0ee8bdc8b7`. commit/push/PR/새 branch 없음.
- 실제 ProjectVersion/테스트 Editor: Unity 6000.3.23f1. Packages/Input/URP/ProjectSettings 변경 없음.
- 선행 자료: `CBT-R2-08-PreCutoverAudit.md`, `CBT-R2-08-P1-Verification.md`, P1 최종 실패 XML과 로그, 실제 Runtime/Debug/Scene/Prefab/meta.
- 시작 시 기존 변경 및 보호 자산 1,249개 SHA-256을 `Logs/CBT-R2-08-P2A-BeforeHashes.json`에 저장했다.
- 추가: `Assets/GAME/Tests/Editor/CombatHandBpInitializationTests.cs`, Unity가 신규 발급한 해당 `.meta`, 이 보고서. 기존 파일 수정·삭제·이동 없음. Runtime public API/SerializeField/기존 GUID/meta 및 Save schema 변경 없음.

## 초기화 Owner와 호출 순서

| 단계 | 실제 Owner / 계약 |
|---|---|
| 영속 소유/장착 | CharacterSkillRuntime과 CharacterSkillSaveParticipant. persistentKey로 획득·장착 상태 관리 |
| 전투 시작 snapshot | CombatEntryPoint가 PersistentSkillCombatLoadoutBridge.ApplyToRequest를 호출. 장착 Film/Unique와 기존 호환 정책을 ISkill snapshot으로 변환 |
| 캐릭터/전투 구성 | FieldCombatantFactory가 Character ID와 snapshot을 Session 참가자에 매핑. CombatBootstrapper가 Session과 StateMachine 생성 |
| 정식 상태 Owner | CombatSession이 per-actor Hand 사전 및 BP/Ultimate Runtime 소유. StateMachine 구성 시 combat states 초기화 |
| Hand 공통 초기화 | TryEnableHandValidation: 소유한 skill reference, 카드 kind, 용량, seed 검증 → CombatHandRuntime 생성 → Session 전체에서 카드 RuntimeId 할당. 같은 actor 두 번 초기화 거부 |
| BP 공통 초기화 | TryEnableBpValidation: 유효 roster/첫 선언 이전/단일 초기화 검증 → BP ledger 0부터 생성 → Ultimate Runtime과 승인 receipt 이벤트 연결 |
| 현재 Scene 호출자 | OnCombatStarted의 Clash Debug opt-in → Orchestrator.TryEnableClashSeriesValidation → ClashSeriesValidationEnabled → Hand/BP Debug opt-in이 공통 API 호출 |
| 인계/재전투 | 인계는 같은 Session/actor hand를 사용하고 비용·자격만 승인. 다음 전투는 새 snapshot과 새 Session 상태 사용. 새 손패를 인계마다 만들지 않음 |

`Validation`이라는 API 이름과 문서가 있으나 구현은 Debug component/scene object를 참조하지 않는다. 이번에 이름만 변경하거나 forwarding alias를 추가해 정식 자동 연결이 완성된 것처럼 만들지 않았다. `TrySetInitialBpForValidation`은 별도 검증 seed 기능이며 신규 fixture는 이 API 없이 기존 attack-cycle BP 획득을 사용한다.

카드 RuntimeId는 **Session 안의 실행 식별자**다. Skill persistentKey는 영속 장착/저장 식별자이며 서로 대체할 수 없다. 새 전투에서 카드 ID가 다시 시작될 수 있어 command binding/session/exchange version 확인은 계속 필요하다.

## 신규 독립 검사

`CombatHandBpInitializationTests`는 transient SO/GameObject와 실제 CharacterSkillRuntime → Bridge → FieldCombatantFactory → CombatBootstrapper 경로를 사용한다. Debug 컴포넌트를 추가하지 않는다. 테스트의 명시적 초기화 helper는 fixture 역할이며 Production 자동 설치 코드가 아니다.

1. `SnapshotHands_AreIndependentAndIdsAreSessionLocal_WithoutDebugComponents`: Bootstrapper 직후 Hand/BP 미설정도 명시적으로 확인. 기존 API 적용 후 각 actor의 손패가 snapshot과 같고 서로 독립, 카드 ID 중복 없음, persistentKey 보존, Debug component 없음.
2. `Reinitialization_IsRejectedWithoutReplacingHandsOrBp`: 재호출 거부 및 기존 객체 보존. 인계 시 중복 생성 방지 기반 가드 확인.
3. `NewCombat_UsesNewEquippedSnapshotAndFreshHandAndBp`: 장착 변경 후 새 전투 snapshot 갱신, 이전 손패 불변, 새 CompletionId/손패/BP 0/refill 0 확인.
4. `ApprovedSpend_ConsumesOnceChargesUltimateAndRefillsOnlyAtCycleBoundary`: 기존 완료 주기에서 BP 획득 → 승인된 카드/BP 투자 1회 → 재전송 거부, receipt 1개, Ultimate stage 1, 다른 actor 손패 불변, 양쪽 attack cycle 완료 때만 refill 1회.
5. `LegacyPlanning_DoesNotAdoptFinalExchangeHandOrBp`: 기존 LegacyPlanning은 해당 opt-in을 거부하고 초기화 상태 유지.

fixture의 일반 카드 2/합 카드 0, seed 7, BP (3,1,3), skill 21/22 및 p2a.skill.*는 기술 검증 입력이다. Production 기본값/정식 콘텐츠로 추가하지 않았다. draw/discard/refill/MP/BP 계산기를 새로 만들지 않았다.

stale 명령·BP 상한/주기·Ultimate 실행·실제 인계·Item/Film은 기존 관련 회귀로 추가 확인하며, 새 단위 테스트만으로 모두 검증했다고 하지 않는다.

## 여섯 실패와 Debug 참조

| 대상 | 참조·역할 | 제거만 했을 때 문제 |
|---|---|---|
| Dungeon_1_Production / Runtime/Combat/CombatRuntime / CombatHandValidationOptIn | scene-added component fileID 1665839071176935759, script GUID bce1360c580248db935ccf2b6f845871. player/enemy InspectorFixture, SO 1/2 및 11/12, copies 4/6, 용량 2/3, seed 2303/2304 | 손패 생성 caller 자체가 없어지고 기존 선택/소모 기능이 바뀔 수 있음 |
| 같은 Scene / CombatClashSeriesValidationOptIn | fileID 1665839071176935761, GUID 040a0e4f14ef4872ac50661f4ee830da. durability3/3, comparisons3, timeout5초는 현재 fixture | 반복 합 활성화·최종 스킬/timeout 선택 callback도 사라짐 |
| 같은 Scene / CombatBpValidationOptIn | fileID 1665839071176935766, GUID 4bd31fad07c44e9795d18530d37a6855. 현재 initial BP3, max3, 주기2, investment3, Ultimate→Skill_skill2 | BP/Ultimate 생성/배정 caller 사라짐. 초기 BP3을 정식 규칙으로 복제하면 안 됨 |
| CombatRuntime prefab / CombatRuntime / CombatBpValidationOptIn | 원본 prefab GUID 1bfc52cd235e809449d17d48571f5a38, Debug BP fileID 3049129679861795314. skillFixtures의 Skill_skill2 ExtraHits1 및 Ultimate 설정 | prefab 인스턴스 전체에 영향. Scene added BP와 원본 BP의 shadowing/override를 함께 확인해야 함 |
| Dungeon_Template / CombatRuntime 인스턴스 | m_SourcePrefab 1bfc…를 통해 원본 Debug BP 상속 | template만 비활성화해도 Debug 존재 금지 predicate는 계속 실패하며 실제 원인 분리가 아님 |

남은 테스트 이름:

1. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasCanonicalCombatRewardUiCameraAndBootstrapOwners`
2. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasExplicitInteractionOwnersWithoutLegacyInteractionPaths`
3. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasOneCanonicalRepeatableNpcInteraction`
4. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasQuestDerivedCompletionContractWithoutLegacyExitOrFallbackDestination`
5. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_IsCleanEnvironmentMigration`
6. `Game.Tests.Integration.ProductionSceneWiringTests.DungeonTemplate_IsCanonicalSingleOwnerProductionSource`

앞 5개는 Forbidden CombatHandValidationOptIn 예외, 마지막은 forbidden-component predicate의 Expected False/True다. 보호 자산을 수정하지 않아 **Asset Change Required / Existing Confirmed**로 유지한다. 자동 초기화 누락과 콘텐츠 미승인도 별도 선행 조건이다. 새 테스트로 이 gate를 우회/약화하지 않았다.

## P2-B 최소 연결 계획 — 편집 승인 아님

### 진입 전 필수 조건

기존 `CombatEntryPoint.finalExchangeRuntimeConfig`는 `Assets/GAME/Data/Combat/Runtime/FinalCombatRuntimeConfig_V1.asset`을 참조하지만 현재 Hand/BP 작성 필드가 없다. 따라서 **지금 즉시 연결 가능한 정식 대체 참조는 없다.** 실제 API·상태 Owner는 재사용 가능하지만 아래 승인/호출 연결 완료 전 Debug component를 제거하지 않는다.

승인 필요: snapshot별 일반/합 분류와 copies/용량, actor별 seed 정책, BP 0 또는 별도 초기값·상한/주기/투자·강화, Ultimate skill mapping, Clash config/timeout. 정식 데이터가 정의되면 기존 Config/EntryPoint 경계에서 이를 읽고 Session 공통 API에 전달하는 최소 호출 연결을 별도 구현·검증한다. LegacyPlanning은 제외하고, 잘못된 설정은 첫 자동 명령 전 원자적으로 거부하며 부분 Hand/BP 상태가 외부에 노출되지 않아야 한다.

초기화 시점은 equipped snapshot/factory roster 구성 **이후**, 첫 선언/적 AI/Orchestrator pump **이전**이어야 한다. 현재 EntryPoint는 Bootstrapper 결과를 받고 VerifyConstructedRoster → ActiveSession 지정 → command binding → OnCombatStarted를 수행한다. 따라서 단순히 뒤쪽 OnCombatStarted의 임의 신규 subscriber에 넣는 방식은 실행 순서를 다시 검증해야 한다. 이 계획은 새 전투 엔진/Manager를 요구하지 않는다.

### 정확한 자산 변경 목록

1. `Assets/GAME/Prefabs/CombatRuntime.prefab` / `CombatRuntime`: 정식 초기화 연결 검증 후 CombatBpValidationOptIn 제거. EntryPoint/Orchestrator/Director/RewardBinder/WorldLifecycle/FieldLock의 기존 fileID·참조 유지. prefab 자체나 meta를 새로 생성하지 않음.
2. `Assets/GAME/Scenes/Dungeon_1_Production.unity` / `Runtime/Combat/CombatRuntime`: added Hand/ClashSeries/BP Debug component 제거. 원본 prefab Debug BP 제거와 Scene added BP 제거를 혼동하지 않음. 변경된 원본 참조와 Scene override를 각각 확인하며 전체 Apply/Revert 금지.
3. `Assets/GAME/Scenes/Dungeon_Template.unity` / 기존 CombatRuntime prefab instance: 원본 BP 제거 결과와 잔여 added/removed-component override 확인. 새 Production Runtime prefab 복제 금지.
4. `Assets/GAME/Data/Combat/Runtime/FinalCombatRuntimeConfig_V1.asset`: 후속 승인된 작성 계약을 기존 Config에 둘 경우에만 그 필드 작성. 현재는 대상 필드가 없으므로 기존 fixture 값을 그대로 복사하거나 YAML에 미구현 필드를 추가하지 않음. 별도 SO가 꼭 필요한지는 그 구현 단계에서 결정하며 이번에 생성하지 않음.

### Inspector 전후 점검표와 예상 결과

- 편집 전: 원본/instance의 Component 및 Override 목록, 활성 상태, entry/director/orchestrator/config/registry, OnCombatStarted/Clash 이벤트 연결 구조, 모든 목표 파일+meta 해시와 미커밋 diff를 기록한다. UnityEvent 외 C# 구독도 포함한다.
- 편집 후: Missing Script/Reference 없음, 정식 Config에서 Session까지 1회 호출, actor별 Hand 1개/BP ledger 1개, 첫 선언 전에 준비 완료. 기존 이벤트 listener가 제거된 Debug component를 참조하지 않는지 확인한다. Debug script 파일은 테스트용으로 유지한다.
- meta/GUID 유지, 의도한 component 제거 외 YAML 변경 없는지 확인한다. 기존 외부 fileID 참조 및 prefab modifications를 함께 확인하며 Registry/ID·Save schema는 변경하지 않는다.
- 예상 테스트: 위 Scene gate 5개는 첫 Debug 예외를 넘어 전체 validator의 후속 조건까지 평가해야 하며, 다음 실패가 있으면 별도로 기록한다. F8은 template/상속 Debug가 모두 없어질 때 통과 예상. 이번에 해당 결과를 미리 Passed로 처리하지 않는다.
- 후속 회귀: Hand/BP 초기화 및 기존 6개 개별 테스트 → Item/Film P1 → handoff/MP/BP/Ultimate/Stun/AllOut → LegacyPlanning → Reward/Save → 보호 설치 테스트의 안전성 확인 후 전체 회귀.
- 실제 수동 Play Mode: 승인 콘텐츠로 Contact/FieldAttack, 1~3인 snapshot/독립 손패, 승인·stale 비용, draw/discard/refill, 인계, BP 주기/상한/Ultimate, 재전투, Pause/Abort/Scene 전환/Reward 복귀 확인. 05/06A 보류 해제는 별도 사용자 지시가 필요하다.

### 안전한 복구

P2-B 시작 직전 **사용자 미커밋 변경까지 포함한 현재 파일**의 복사본과 해시를 저장한다. 실패 시 이번 승인 편집의 해당 파일/변경 구간만 그 복사본으로 복구한다. HEAD 버전으로 checkout/reset하거나 기존 미커밋 diff를 버리지 않는다. 도중 사용자 변경이 있으면 단순 파일 덮어쓰기를 멈추고 변경 구간을 대조한다. 원본 prefab과 Scene override를 하나의 복구 단위로 확인하고 meta는 원래 파일을 유지한다. 재import/compile/6개 gate·핵심 회귀로 복구를 확인한다.

## 실행 검증 및 보존

독립 초기화 **5/5 PASS** (`Logs/CBT-R2-08-P2A-Initialization.xml`), Unity 종료0. 최종 선택 회귀는 **637 PASS / 6 FAIL / 1 IGNORED**, 총644 (`Logs/CBT-R2-08-P2A-Regression.xml`, `.log`), 종료2는 기존6개 asset gate 실패를 반영한다. P1 최종 XML과 실패 이름을 직접 비교해 동일함을 확인했다. 신규 실패 없음(선택 범위 기준).

필터 `Logs/CBT-R2-08-P2A-TestFilter.txt`는 P1 집중/Legacy 회귀·원래 실패6개·Item/Film에 신규 초기화5개와 PersistentSkillCombatLoadout3개를 추가한 것이다. 이전 P1 선택629/6/1 대비 추가8개 모두 통과했다. Legacy 관련220개, BP 승인/stale·상한/주기·Ultimate, 인계/손패 독립성, Item/Film P1 수정, 읽기 전용 Production 감사도 포함한다. 6개 원문 message/stack은 `Logs/CBT-R2-08-P2A-SixEvidence.md`에 저장했다.

Unity 6000.3.23f1에서 새 C# 컴파일 및 실제 테스트 실행 확인. 이번 로그에서 C# compile error/warning은 발견하지 못했다. 기존 asset gate 예외 및 테스트 runtime 진단은 compiler 오류와 구분한다. Ignored는 기존 `Game.Tests.Integration.PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners` 한 건이다.

보존 결과: snapshot1,249개 파일 전부 SHA-256 동일. Scene/Prefab/SO/기존 meta/GUID/Packages/ProjectSettings 및 기존 dirty/untracked 변경을 유지했다. 새 테스트 파일·신규 Unity 발급 meta·이 보고서만 추가했다. 신규 파일 공백 검사 통과; 저장소 전체의 기존 폰트/Scene 공백 문제는 변경하지 않았다.

전체 회귀는 보호 Scene 설치/저장 코드 경로 때문에 실행하지 않는다. 이전 전체 1,562/8/1 및 P1 선택629/6/1을 이번 전체 결과로 재표기하지 않는다. 실제 보호 Asset 편집/Inspector·정식 Production 수동 Play Mode/Player Build·별도 Editor 재시작 Save 검증도 미실행이다.

## Development tracking

- System: Combat / Hand / BP / Character skill snapshot.
- Requirement: CBT-R2-08-P2A. Production Owner는 EntryPoint/Bootstrapper/Session/Orchestrator 유지.
- Implementation: 공통 초기화 능력 검증 완료, 새 Runtime 구현 없음. **Production 자동 호출/작성 계약은 Pending**, P2-B Asset 작업 미승인.
- Compile/자동 검사: 최종 기록 참조.
- Inspector: 읽기 전용 확인, 보호 자산 무변경.
- Play Mode/Integration: 기존 격리 fixture와 정식 콘텐츠 Production 수동 검증 구분; 후자 보류.
- Legacy/Compatibility: 기존 API·LegacyPlanning·Debug fixture 경로 보존.
- Recommended: **Core Capability Verified — Production Initialization Contract / Asset Migration / Production Validation Pending**.
- P2-A 결과를 Cutover 완료나 6개 실패 해결로 기록하지 않는다.
