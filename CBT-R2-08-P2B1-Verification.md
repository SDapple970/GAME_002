# CBT-R2-08-P2B1 — Production Hand/BP 초기화 연결

2026-10-29 KST. **코드 연결 구현 완료, 실제 Production 활성화는 미수행.** 보호 Scene/Prefab/SO 및 기존 Debug 컴포넌트를 보존했다. P2-B3 Cutover, 정식 콘텐츠 승인, 05/06A Production 수동 검증 보류를 유지한다.

## 구현 결과·초기화 흐름

`CombatEntryPoint.StartCombat` → 기존 장착 bridge/정규화/roster 검증 → 기존 CombatBootstrapper의 참가자/StateMachine 구성 → **EntryPoint가 제공하는 내부 초기화 hook** → 모든 참가자/설정 검증 → Hand → BP/Ultimate → Opening Effect/첫 Tick → ActiveSession 및 World/Flow/Director/Enemy command 연결 → 시작 이벤트/UI 명령 순서다.

Bootstrapper의 기존 공개 3인자 API와 optional factory 계약은 그대로 유지했다. 4인자 overload는 internal이며 같은 시작 구현의 사전 초기화 hook이다. 새 전투 Owner/Manager/별도 시작 경로는 없다. 상태 Owner는 기존 CombatSession, 카드·BP·Ultimate 계산은 기존 Runtime/API다.

Contact와 FieldAttack은 모두 같은 EntryPoint 계약을 이용한다. 새 fixture에서는 두 StartReason과 1/2/3인 요청을 검증했고, 기존 field entry delegation/physics fixture도 선택 회귀에 포함했다. 정식 Asset을 활성화한 실제 접촉/키 입력 통합 검증은 별도다.

## 지침·Git·환경

- 지침: 루트 `AGENTS.md`; 중첩 AGENTS/override 없음.
- Branch `main`, HEAD `6fa26d294472574097d33b884858ba0ee8bdc8b7`. commit/push/PR/새 branch 없음.
- ProjectVersion 및 실제 테스트 Editor: Unity 6000.3.23f1. Packages/Input/URP 버전 변경 없음.
- P2-A 보고서/연결 계획, P1·사전 감사 및 실제 Runtime/Debug/자산 참조를 대조했다.
- 시작 시 코드·기존 변경·보호 자산의 해시를 `Logs/CBT-R2-08-P2B1-BeforeHashes.json`에 저장했다. 기존 미커밋 파일은 원래 내용 위에 필요한 코드만 확장했다.

## 새 Config 작성 계약과 기존 자산 영향

기존 `FinalCombatRuntimeConfigSO`에 다음 필드만 추가했다. **기존 SO 자산을 저장/수정하지 않았다.** 미기재 활성화 값은 false라 기존 Asset/Legacy/Debug 시작 동작을 유지한다.

| 필드 | 계약 |
|---|---|
| `enableProductionHandBp` | 기본 false. 명시적 opt-in일 때만 정식 초기화. false/null config의 기존 처리와 LegacyPlanning 동작은 유지 |
| `normalHandCapacity`, `clashHandCapacity` | authored 비음수, 합계는 0보다 커야 함. 특정 참가자의 규칙이 전혀 draw될 수 없으면 오류. 승인 슬롯 기본값을 넣지 않음 |
| `handSeed` | authored seed에서 roster 순서별 offset으로 독립 손패를 만듦. 기존 HandRuntime shuffle 사용 |
| `handCardRules[]` | 각 entry의 `skill`, `kind`(Normal/Clash), `copies`. 등록된 Skill SO의 명시적 규칙이며 copies>0, kind 유효, persistentKey 누락/중복 금지. 모든 참가자 최종 snapshot 스킬에 규칙 필요 |
| `bpMaximum`, `bpRoundCyclesPerGain`, `bpMaximumInvestment` | 모두 authored 양수. 기존 CombatBpValidationConfig/BP Runtime 사용. 기존 core 정책대로 초기 balance=0. Debug의 initialPlayerBp=3을 복사하지 않음 |
| `ultimateSkills[]` | 선택적 registry SO 목록. 참가자가 실제 보유한 일치 스킬만 배정하며 참가자당 하나. 누락/중복/미등록 reference는 거부. 비어 있으면 gauge만 존재하고 실행 스킬은 미배정 |

BP enhancement는 기존 SkillDefinitionSO의 `BpEnhancementKind`/`BpEnhancementPerPoint`를 사용한다. 별도 효과 엔진이나 강화 기본값을 만들지 않았다. Skill snapshot의 SoSkill.Definition이 명시적 rule/registry와 일치해야 한다. 자동 fixture 검색·고정 skill ID fallback·Debug Inspector 데이터 복사는 없다. 테스트의 transient 작성값은 테스트 안에서만 설정한다.

활성화한 정식 경로에서는 아군마다 unique Character ID와 비어 있지 않은 장착 loadout snapshot을 요구한다. 기존 미설정 호환 캐릭터를 임의 ID/스킬로 채우지 않는다. Enemy는 Factory가 완성한 스킬 목록을 동일 규칙으로 검증한다. 카드는 Session 전체 RuntimeId로 구분하고, persistentKey를 카드 인스턴스 ID로 재사용하지 않는다. draw/discard/refill 및 인계 시 같은 Hand를 유지하는 기존 정책은 그대로다.

이 Flag는 **Hand/BP/Ultimate 준비만** 담당한다. 반복 합의 정식 config·timeout·최종 스킬 선택 정책을 새로 승인/구현한 것은 아니다. 활성 세션에서 Clash Debug fixture도 주입되지 않으므로 반복 합 정식 설정은 P2-B2의 별도 선행 조건이다.

## 실패·중복·수명 계약

1. EntryPoint의 `_startingCombat`/ActiveSession guard가 중복 시작을 거부한다. 초기화는 첫 선언 이전의 새로운 FinalExchange 세션만 허용한다.
2. roster 구성 직후 예상 field object/side/Combatant ID를 확인하고, 아군 Character ID 누락/중복/binding·snapshot 누락, rule 및 BP/Ultimate 구성을 검사한다. 모든 참가자의 규칙을 검증한 뒤 Hand/BP를 적용한다.
3. 성공 전에는 ActiveSession을 공개하거나 Flow/UI/AI 명령을 bind하지 않는다. 예측 가능한 설정 오류는 상태 적용 전에 거부된다. 초기화/첫 시작 처리에서 예외가 발생하면 `CombatSession.AbortStartup`이 종료 표시·receipt 구독/Ultimate 해제·Hand/BP 제거를 수행한다. 기존 EntryPoint rollback으로 global state와 binding을 정리한다.
4. Opening Effect 이전에 초기화하므로 설정 실패 시 inspiration/stagger 등 opening 효과가 적용되지 않는다. 신규 실패 fixture에서 Opening Effect 미소비와 부분 Runtime 제거도 검증했다.
5. `HasProductionHandBpInitialization`은 성공한 Session의 비직렬화 표식이다. Hand/BP Debug TryApply는 이 세션에 false를 반환하고, 자동 이벤트는 건너뛴다. Clash Debug도 건너뛴다. Debug 타입이 Production Runtime을 소유하거나 재초기화하지 못한다.
6. Flag가 false인 기존 자산에서는 표식이 없으므로 기존 opt-in 및 fixture 이벤트 순서를 유지한다. Debug 스크립트나 직렬화 컴포넌트는 삭제하지 않았다.
7. 일반 종료는 기존 EntryPoint/StateMachine의 수명 정리 계약을 사용한다. 다음 전투는 새 snapshot/Session/Hand/BP를 구성한다. 종료된 세션에서는 Hand/BP 초기화 API가 거부된다.

## 수정·추가 파일과 API 영향

수정 7개:

- `Assets/GAME/Scripts/Combat/Data/FinalCombatRuntimeConfigSO.cs` — 명시적 작성 필드와 getter/serializable card rule. SO는 데이터만 보관.
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatEntryPoint.cs` — roster 뒤, 공개/bind 전 설정 검증·기존 API 실행 및 rollback 정리.
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatBootstrapper.cs` — 내부 사전 초기화 hook 및 실패 폐기. 기존 공개 signature 유지. 원본 파일이 UTF-8이 아니어서 ASCII 변경만 원래 바이트 인코딩을 유지해 적용했으며 기존 주석을 재인코딩하지 않음.
- `Assets/GAME/Scripts/Combat/Runtime/Model/CombatSession.cs` — 성공 표식, 실패 Runtime 정리, 종료 후 초기화 거부.
- `Assets/GAME/Scripts/Combat/Debugging/CombatHandValidationOptIn.cs`
- `Assets/GAME/Scripts/Combat/Debugging/CombatBpValidationOptIn.cs`
- `Assets/GAME/Scripts/Combat/Debugging/CombatClashSeriesValidationOptIn.cs` — Production 성공 표식에 대한 비중복 guard만 추가.

추가: `Assets/GAME/Tests/Editor/CombatProductionHandBpStartupTests.cs`, Unity 신규 발급 `.meta`, 이 보고서. 삭제/이동 없음. 기존 public API 파괴·SerializeField 이름/타입 변경·기존 GUID/meta 변경 없음. 추가 API는 Config 작성 getter/중첩 rule 및 읽기 전용 Session 성공 표식이다. 저장 schema·Quest/Reward 정책·Input 소유권 변경 없음.

## 자동 검증

- 신규 1차: **10 PASS / 1 FAIL**. EditMode가 종료 프레임을 실행하지 않아 재전투 fixture가 너무 일찍 요청함을 확인. 기존 EntryPoint.Update로 완료를 발행하도록 테스트를 보정했으며 Runtime 종료 정책을 바꾸지 않았다.
- 신규 2차: **16/16 PASS**, 초기화 실패/부분 상태 정리/BP 승인 소비 등 추가 포함.
- 신규 최종: **18/18 PASS**, `Logs/CBT-R2-08-P2B1-Startup3.xml`, 종료0. unsaved 실제 Editor Play Mode에서 2인 초기화/Abort 정리를 포함.
- 최종 선택 회귀: **655 PASS / 6 FAIL / 1 IGNORED**, 총662. `Logs/CBT-R2-08-P2B1-Regression.xml`, `.log`, 종료2는 기존6개 asset gate 실패를 반영. P2-A 637/6/1 대비 신규18개 모두 통과. 실패 이름 직접 비교 차이 없음.
- 포함: 1/2/3인 및 두 시작 사유, snapshot→Hand/BP→첫 명령 순서, 잘못된 Character ID/rule/분류/용량/BP 설정, 중복 시작/초기화, 다음 전투 새 상태, BP/MP/카드 1회 소비·Ultimate receipt, Debug 혼합 방지, disabled config/LegacyPlanning, P2-A core 초기화·snapshot, 인계·Ultimate, Item/Film P1, Legacy 관련220개와 읽기 전용 Production 감사.
- Unity C# compile error 없음. 재컴파일에서 기존 compiler warning4건: FieldEnemy CS0067, RescueNpcActor 1건 및 StoryInteractionController 2건 CS0414. 실행 중 기존 Scene validation 예외는 compiler error와 구분한다.
- Ignored: 기존 `PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners`. 별도 Editor 재시작 단계는 실행하지 않음.

### 기존 실패6건 — 전부 Asset Change Required

1. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasCanonicalCombatRewardUiCameraAndBootstrapOwners`
2. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasExplicitInteractionOwnersWithoutLegacyInteractionPaths`
3. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasOneCanonicalRepeatableNpcInteraction`
4. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasQuestDerivedCompletionContractWithoutLegacyExitOrFallbackDestination`
5. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_IsCleanEnvironmentMigration`
6. `Game.Tests.Integration.ProductionSceneWiringTests.DungeonTemplate_IsCanonicalSingleOwnerProductionSource`

앞5개는 기존 Hand Debug component의 clean-scene gate 예외, 마지막은 template의 BP Debug 존재 검사다. 이번 코드로 Serialized Debug reference를 숨기거나 validator를 약화하지 않았으므로 그대로 실패한다.

전체 회귀는 보호 자산 설치/저장 함수를 호출하는 검사 때문에 실행하지 않았다. **1,562/8/1은 이전 전체 기준선**이며 이번 결과가 아니다. 실제 Production Asset 활성화/Inspector·정식 콘텐츠 수동 Play Mode·Player Build·Editor 완전 재시작 Save 검증도 미실행이다.

## Inspector·콘텐츠 Pending 및 P2-B2/B3

현재 Production 자동 초기화 활성화: **아니오**. `Dungeon_1_Production/Runtime/Combat/CombatRuntime` 및 기존 `Prefabs/CombatRuntime.prefab`의 `CombatEntryPoint.finalExchangeRuntimeConfig` 참조 자체는 유지하며, 해당 `Data/Combat/Runtime/FinalCombatRuntimeConfig_V1.asset`을 이번에 변경하지 않았다.

P2-B2에서는 승인된 공식 Character ID/시작·성장, Skill/Enemy snapshot, 모든 스킬의 Normal/Clash/copies/용량/seed, BP 주기/상한/강화와 Ultimate mapping을 준비한다. 필요하면 기존 Config에 위 필드를 작성하는 별도 승인을 받고, enableProductionHandBp는 전환 계획이 준비될 때만 설정한다. 누락 규칙은 구체적인 startup error로 거부되므로 임의 기본값으로 메우지 않는다. 반복 합 정식 config/callback도 별도 준비해야 한다.

P2-B3에서만 P2-A 계획에 따라 원본 prefab과 Dungeon Scene added/override의 Hand/Clash/BP Debug 참조를 제거하고 활성화한다. Template 검사/Scene gate6건, Missing Script/Reference, registry·Director·World/Reward binding 및 YAML diff를 재검증한다. 기존 미커밋 상태를 포함한 사본으로 지정 변경만 복구할 수 있도록 준비한다. 이번 지시는 해당 자산 편집 승인이 아니다.

수동 통과 기준: 실제 Contact와 FieldAttack 모두 첫 UI/적 명령 전에 참가자별 Hand/BP 준비, 1~3인 snapshot/인계에서 같은 Hand 유지, 승인 소비·stale 거부, 완료 cycle refill/BP 누적, Ultimate 배정/단계, 재전투 reset, 잘못된 설정은 전역 상태/효과/자원 무변경 거부, Pause/Abort/Scene 전환/Reward/Continue의 기존 복원 계약. 05/06A 보류를 해제하지 않고 완료로 기록하지 않는다.

## Development tracking

- System/Requirement: Combat Hand/BP Startup / CBT-R2-08-P2B1.
- Owner: 기존 EntryPoint → Bootstrapper → Session; 기존 Hand/BP/Ultimate Runtime 유지.
- Implementation: **Implementation Complete — opt-in 코드 연결 및 작성 계약**.
- Compile: Verified. Tests: 18/18 신규 + 선택655/6/1(기존 asset gate).
- Inspector/Serialized assets: 보호 자산 무변경, 실제 작성/활성화 Pending.
- Play Mode: unsaved transient fixture Verified; 실제 Production 콘텐츠 통합/수동 검증 Pending.
- Recommended: **Code Connection Complete — Production Activation / Content / Asset Migration Pending**.
- Cutover/전체 전투 시스템 완료 아님.

## 최종 보존 점검

슬래시를 정규화해 중복을 제외한 시작 시 1,795개 경로 중 위 의도한 코드7개만 변경됐고, 나머지 **1,788개는 SHA-256 동일**이다. Scene/Prefab/SO/기존 meta/GUID/Packages/ProjectSettings, 관련 없는 기존 dirty/untracked 파일은 보존됐다. 변경된 기존 전투 코드도 시작 당시 상태를 기반으로 최소 확장했다. 새 테스트/Unity 발급 meta/이 보고서만 추가했고 삭제·이동은 없다.

수정 범위 `git diff --check` 및 신규 테스트/보고서 공백 검사 통과. Git의 LF/CRLF 안내는 공백 오류와 구분한다. 저장소 전체의 기존 폰트/Scene 공백 문제는 수정하지 않았다. 기존6개 실패 원문은 `Logs/CBT-R2-08-P2B1-SixEvidence.md`에 보존했다.
