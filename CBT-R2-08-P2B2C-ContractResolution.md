# CBT-R2-08-P2B2C — 초기화 계약 충돌 해소 설계

2026-10-29 KST. **설계 및 현행 기능 검증. 정책 변경·Production 활성화·보호 Asset 편집 없음.**

## 결론과 승인 경계

확정 가능한 기술 경계는 기존 Owner 및 API 재사용, 영속 스킬 소유 분류와 전투 카드 종류의 분리, 첫 명령 이전 검증/초기화 및 실패 정리다. Enemy Hand의 적용 범위, 동일 Skill의 양쪽 배치, 정식 Clash 활성화·설정/선택 정책은 아직 승인되지 않았다. 아래 필드명/분기는 **후속 구현 제안**이며 이번에 추가하지 않았다.

현재 작업문은 Enemy Hand 선택 적용을 권장하되 승인 확인 후 변경하라고 명시한다. P2B2에서 대조한 인계 D15는 제안이며 별도의 사용자 확정 기록은 없다. 양쪽 배치도 필요 여부를 결정하라는 요청이지 승인된 동작이 아니다. 따라서 어떤 제안도 이번에 Production에 적용하지 않는다.

## 조사 지침·Git·구현 근거

- 지침: 루트 `AGENTS.md`; 중첩 AGENTS/override 없음. Branch `main`, HEAD `6fa26d294472574097d33b884858ba0ee8bdc8b7`. Unity ProjectVersion 및 테스트 Editor 6000.3.23f1.
- 선행: `CBT-R2-08-P2B1-Verification.md`, `CBT-R2-08-P2B2-Contract.md`. P2B2에서 직접 확인한 CURRENT_HANDOFF D04/D08/D12~D15 및 기능구현 문서의 제안/승인 구분을 사용한다. 이번에 원격 문서를 수정하지 않았다.
- 코드: `FinalCombatRuntimeConfigSO`, `CombatEntryPoint.InitializeProductionHandBp`, `CombatBootstrapper`, `CombatSession`, `CombatHandRuntime`, `CombatExchangeCommitPolicy`, `CharacterSkillRuntime`, `PersistentSkillCombatLoadoutBridge`, `SkillDefinitionSO`, `FinalCombatEnemyCommandController`, `FirstValidFinalCombatEnemyDecisionPolicy`, `ConditionalFinalCombatEnemyDecisionPolicy`, `CombatFlowOrchestrator.TryEnableClashSeriesValidation`, `CombatClashSeriesConfig/State`, Hand/BP/Clash Debug opt-in.
- 자산: 기존 `Data/Combat/Runtime/FinalCombatRuntimeConfig_V1.asset`, `Prefabs/CombatRuntime.prefab`, `Scenes/Dungeon_1_Production.unity`, `Scenes/Dungeon_Template.unity` 및 meta. 현 Config의 활성화 값 미작성=false, Scene/prefab의 Debug 참조 유지.
- 이번 코드 수정·삭제·이동·새 필드 없음. 이 보고서 및 무시된 Logs 검증 기록만 추가한다. 기존 API/SerializeField/Guid/meta/Save/Quest/Reward/Unity/Package 변경 없음. commit/push/PR 없음.

## 1. Enemy Hand: 선택 정책의 설계와 영향

### 현행 구현

P2B1 초기화는 `session.Allies`와 `session.Enemies` 전체를 합쳐 rule 검사와 Hand 생성을 수행한다. 따라서 현재 활성 경로에서는 Enemy Hand가 필수다.

반면 적 명령의 기존 정책에는 이미 두 실행 분기가 있다. `FirstValidFinalCombatEnemyDecisionPolicy`와 `ConditionalFinalCombatEnemyDecisionPolicy`는 actor가 Hand를 가지고 있으면 해당 종류 카드 존재 여부까지 검사하고, 없으면 기존 skill/MP/target/상태 조건을 검사한다. Controller는 같은 Orchestrator의 가용성/승인 경계를 사용한다. 별도 Player UI에 의존하거나 새로운 Enemy 피해 실행기를 만들 필요가 없다.

### 권장 최소 구현안 — 승인 후만

- 기존 Config에 Enemy Hand 적용 여부를 나타내는 명시적 선택을 추가한다(예: `initializeEnemyHands`). **기존 활성 Config의 의미는 모두 적용이므로 누락/이전 자산은 기존 적용 동작을 보존하는 migration이 필요**하다. false를 새 암묵 기본값으로 적용하지 않는다. 실제 필드 형식과 기본값·migration은 승인 후 기존 SO fixture로 입증한다.
- 아군 Hand 초기화는 기존대로 유지. Enemy Hand를 적용하는 경우에만 적 card rule·drawable 상태를 검사하고 생성한다. BP는 Enemy Hand 선택과 무관하게 기존 세션 정책을 유지한다.
- 적 스킬 snapshot의 소유·등록·유효성 검증과 Hand rule 검증을 분리한다. Enemy Hand off 때문에 적 스킬/목표/MP/Panic/Stun 검증까지 생략하면 안 된다. Hand off인 적은 Hand 전용 rule 누락 때문에 거부하지 않되 BP enhancement/Ultimate mapping 검사는 계속 수행한다.
- 기존 정책/Controller의 Hand 유무 분기를 재사용한다. No Hand를 빈 Hand로 만들지 않는다. 빈 Hand는 자원 제약 상태이고 No Hand는 카드 제한 비적용 상태여서 의미가 다르다.
- 적용 범위는 **모든 적 공통 opt-in**과 **Enemy Profile별 opt-in** 중 결정이 필요하다. 최소 제안은 Config의 전원 선택이며, 개체별 선택은 별도 필요성이 승인될 때 확장한다. 임의 적 종류별 기본값은 만들지 않는다.

영향 범위: Config 코드, EntryPoint 초기화의 validation/build 대상 분리, 기존 Enemy policy/command 회귀와 신규 on/off 통합 fixture. Enemy policy 자체의 새 분기나 새로운 Manager는 기본안에 필요하지 않다. Save/CharacterSkillRuntime 변경도 필요하지 않다.

**승인 필요:** 적용 범위, 이전 Config 기본 의미 보존 방법, Enemy Hand off에서 허용할 카드 기반 강화/특수행동 제한. 이번 Production Config on/off 선택 기능은 미구현이므로 그 전환 테스트는 미검증이다. 기존 core의 Hand 유무 테스트 통과와 구분한다.

## 2. Skill 소유 분류와 전투 카드 종류

| 식별/분류 | 실제 Owner / 수명 | 이번 판단 |
|---|---|---|
| Film / Unique / Compatibility | SkillDefinitionSO.ownershipCategory, Unique owner; CharacterSkillRuntime의 획득·장착/Save | 소유·장착 자격 분류. Normal/Clash를 자동 결정하지 않음 |
| Normal / Clash | Config HandCardRule.kind → CombatHandEntry → CombatHandCard | 전투에서 어느 명령에 낼 수 있는 카드인지 결정. 같은 snapshot skill이라도 명시적 제작 규칙이 필요 |
| persistentKey | SkillDefinitionSO와 영속 장착 참조 | 저장 정체성. 카드를 여러 장 만든다고 새 key/Skill SO를 복제하지 않음 |
| SkillId | Combat registry/ISkill 런타임 식별 | registry 안에서 유일. 카드 instance 식별과 분리 |
| RuntimeId | CombatSession이 생성하는 개별 카드 번호 | 복수 copies도 각각 고유 ID. 세션 범위이며 stale 방어에는 세션/버전/token도 필요 |

현재 Config는 Skill SO당 한 rule만 허용하고 persistentKey 중복을 거부한다. `copies`로 같은 종류의 여러 카드는 만들 수 있다. 비용은 선택된 instance를 기준으로 MP/카드/BP를 함께 검증·승인하고 한 장을 discard한다. 인계는 같은 actor Hand를 유지하며 장착 변경은 다음 전투 snapshot에 반영한다. 이 계약은 그대로 보존한다.

양쪽 동시 배치는 **Contract Pending**이다. 먼저 실제로 필요한지 결정한다. 필요하다면 후속 제안은 rule의 유일성을 Skill 단독에서 `(Skill 정체성, Kind)`로 확장하되 기존 단일 rule은 그대로 읽는 방식이다. 기존 필드 삭제/이름·타입 변경 없이 배열의 의미를 확장할 수 있는지 검토한다. 이 제안은 이번에 구현하지 않았다.

양쪽 배치 구현 전 위험 검사:

1. 동일 스킬의 Normal/Clash 두 instance는 서로 다른 RuntimeId를 가져야 하며, 일반 공격이 Clash 카드나 다른 copies를 임의 소비하면 안 된다.
2. 수량은 종류별 draw/discard/refill에 남아야 한다. 같은 스킬을 두 카드로 배치했다고 MP·BP·Ultimate receipt가 두 번 발생하면 안 된다.
3. Skill 참조만으로 첫 카드를 고르는 API는 현재 요청 종류와 optional RuntimeId를 함께 검사해야 한다. 비용 승인 및 enemy 결정 경로도 동일해야 한다.
4. Save에는 카드 배치 instance를 영속 Skill 소유 항목으로 추가하지 않는다. Film 획득·장착 중복, Unique 소유/해금, snapshot 중복 SkillId 정리는 기존 정책을 유지한다.

필요할 때의 예상 영향: Config rule validation/build, 카드 선택·commit tests, Enemy policy tests, snapshot/Save 무변경 회귀. CharacterSkillRuntime/Save schema 변경은 기본안에서 필요하지 않다. 기존 단일 손패 fixture와 양쪽 배치 fixture는 별도로 검증해야 한다.

## 3. Clash 초기화: 실제 누락과 안전한 구현 경계

### 현행 순서와 누락

EntryPoint의 사전 hook은 roster/snapshot 구성 뒤 Hand→BP→Ultimate를 준비한다. 이후 Opening Effect/첫 Tick, ActiveSession, Flow/Director/Enemy bind, OnCombatStarted가 이어진다.

Clash config는 현재 Orchestrator가 보관하며, 실제 반복 합 상태 `CombatClashSeriesState`는 첫 공격·대응이 승인된 Exchange에 생성된다. 강인도는 이 Series에서 시작값을 받고 라운드 사이 감소하며, Posture와 별개다. Hand/BP 초기화가 성공했다고 Series를 미리 생성해서는 안 된다.

현재 Clash Debug opt-in은 OnCombatStarted에서 `TryEnableClashSeriesValidation`을 호출한다. P2B1 성공 표식이 있으면 이를 건너뛰므로 **Hand/BP-only 시작은 성공하지만 반복 합 설정은 없다.** 현재 Config에는 정식 Clash 활성/설정 필드가 없어, ‘Clash 설정 누락 시 startup 실패’라는 새 계약을 아직 표현하지 못한다.

따라서 지금 Hand/BP 시작에 Clash를 무조건 필수화하면 P2B1의 기존 명시적 Hand/BP-only 계약을 바꾼다. 이는 명백한 계산 오류 수정보다 **기능 활성화/필수 범위 결정**에 해당한다. 이번에 현재 성공 fixture를 실패하도록 바꾸지 않았다.

### 필요한 정식 설정 — 아직 승인값 없음

- 반복 합 활성화/필수 여부(Hand/BP와 별도인지, 전체 Production profile의 필수 기능인지).
- attacker/responder 초기 Durability, 최대 비교 수.
- 유한한 양수 결정 timeout과 Pause/비배율 시간 규칙.
- 최종 피해 스킬 선택: 마지막 승인 승자 스킬인지, 별도 선택인지.
- timeout attack 선택: 첫 가용 스킬인지, 명시적 정책인지.
- 동점/동시 소진/최대 비교 수 도달 안전 종료 계약. 기존 core 구현과 기획 승인 범위를 대조.

Config의 현 MP/Posture 필드를 Durability로 재해석하지 않는다. Debug fixture의 3/3/3/5초 또는 selector 구현을 정식 기본값으로 복사하지 않는다.

### 권장 최소 구현안 — 정책 승인 후만

1. 기존 Config에 명시적인 Clash 기능 선택(예: `enableProductionClash`)과 승인된 설정을 추가한다. 기존 Asset의 누락 선택은 비활성으로 유지한다. **미구현 새 필드를 이번 YAML에 쓰지 않는다.**
2. Hand/BP-only를 허용할지 결정한다. 허용한다면 기존 P2B1 활성 경로는 유지하되 전체 반복 합 Production Ready로 기록하지 않는다. Clash가 required인 선택을 명시했을 때는 필드/selector 누락·잘못된 값·NaN/Infinity timeout을 첫 효과/명령 전에 거부한다.
3. EntryPoint에서 전체 설정을 사전 검증하고, 기존 Hand/BP initializer와 동일한 startup transaction에 포함한다. Orchestrator는 binding을 확보하되 첫 pump/요청 발행을 보류한 상태에서 config를 받아야 한다. 기존 Bind 후 공개 요청을 발행한 다음 뒤늦게 검사하는 방식은 피한다.
4. 기존 Orchestrator API의 핵심 설정 검증/저장을 공통 내부 메서드로 추출하는 최소 변경을 검토한다. 현재 `TryEnableClashSeriesValidation`은 `ClashSeriesValidationEnabled` 이벤트를 발행하여 Debug Hand/BP/UI listener를 깨운다. **그 이벤트를 Production 준비의 필수 메커니즘으로 재사용하지 않는다.** 공개 validation API는 동일 이벤트 동작을 유지하는 compatibility façade로 남기고, 정식 경로는 공통 설정만 사용하도록 설계한다.
5. 성공 후에만 첫 명령과 Enemy startup을 활성화한다. 실제 Series는 기존 StateMachine/Exchange가 첫 commit 후 생성한다. 재진입은 Session/binding/선언 상태로 거부한다.
6. 실패 시 Orchestrator의 pending config/request/token/binding을 해제하고, 기존 `AbortStartup`/EntryPoint rollback으로 Hand/BP/Ultimate/전역 흐름을 정리한다. 부분 설정 후 비용·Opening Effect·UI 활성화가 남지 않아야 한다.

예상 최소 수정 범위: 기존 Config, EntryPoint, Orchestrator의 startup 준비 경계 및 관련 테스트. Session은 상태/수명 경계를 유지하고 Bootstrapper hook은 기존 것을 사용한다. 선택한 정책에 꼭 필요하지 않다면 marker/새 타입도 추가하지 않는다. 새 엔진/Manager/UI·Input Owner는 필요하지 않다.

**승인 필요:** Hand/BP-only 허용, Clash required/optional, 명시적 수치와 selector 정책, 기존 validation 이벤트를 유지하는 호환성 계획. 현재 missing-Clash startup 안전 실패 테스트는 실행 가능한 정식 계약이 없으므로 **미검증**이다. 기존 core가 잘못된 config/API 인자를 거부하는 검증과 구분한다.

## 구현 가능 범위와 확정 상태

| 구분 | 이번 확정/결과 | 후속 처리 |
|---|---|---|
| 소유권·identity 경계 | 기존 EntryPoint/Session/Orchestrator/SkillRuntime 사용, 소유 분류≠카드 종류 | 기술적 설계 기준으로 유지 |
| 현재 단일 rule·카드 비용·Legacy | 현재 동작을 보존하며 기존 회귀 실행 | 변경 필요 없음 |
| Enemy Hand 선택 | 권장 설계만 제시; 정책 승인 없음 | 적용 범위·이전 Config 의미 승인 후 최소 구현 |
| 동일 Skill 양쪽 배치 | 필요 여부부터 미정 | 필요 승인 시에만 pair-rule/선택 비용 검증 |
| 정식 Clash 초기화 | 저장·첫 pump 이전 설정·rollback 재사용 설계, 수치/필수 범위 미정 | 승인 후 기존 Owner에 연결 |
| 보호 자산 전환 | 미승인 | 위 코드/데이터 완료 뒤 P2-B3에서 별도 승인 |

정책을 가정하지 않고 고칠 명백한 신규 초기화 결함은 이번 조사에서 확인하지 못했다. 현 충돌을 해결하려면 먼저 위 활성화/분류 정책의 확정이 필요하므로 Runtime/Editor/Test 코드를 수정하지 않았다.

## 테스트·검증 범위

선택 회귀 실행 결과는 아래 최종 기록에 반영한다. 필수 시나리오별 증거 범위:

- 기존 비활성 Config, Player Hand/Character snapshot, Session 카드 ID, 단일 배치, 중복 초기화/소비, Debug 혼합 방지, P2B1 18개 및 LegacyPlanning: 기존 fixture를 재실행한다.
- 적 Hand 미적용/적용 **core 정책**: FinalCombatEnemyBehaviorPolicyTests의 기본 유효 스킬 선택과 AuthoredPriority_UsesOnlyCardPresentInEnemyHand 등 기존 검사를 재실행한다. 정식 Config의 새 선택 기능은 미구현/미승인이므로 on/off 전환은 미검증이다.
- Hand/BP startup 순서와 부분 실패 폐기는 P2B1에서 검증한다. Hand/BP/**정식 Clash** 원자적 준비 순서와 required Clash 누락 실패는 미검증이다.
- 동일 Skill 양쪽 배치 신규 동작은 미검증이며 테스트를 새로 구현하거나 통과로 기록하지 않는다.
- 기존 실패6개: Production Scene Hand Debug gate5개와 Dungeon_Template BP Debug gate1개를 개별 재현한다. 전체 이름은 P2B1 보고서에 있으며 계속 Asset Change Required다.
- 전체 회귀/보호 설치·저장 검사, Production 수동 Play Mode·Inspector, Player Build·별도 Editor 재시작 Save 검증은 미실행. 기존 전체 1,562/8/1은 이전 기준선이다. 정책 미승인을 이유로 기존 테스트를 삭제/Skip/완화하지 않았다.

## P2-B3 선행 조건 및 보존

위 세 정책의 승인, 정식 Clash 설정/호환 초기화 구현, 공식 Character/Skill/Enemy/Ultimate 데이터 작성과 별도 검증이 먼저다. Runtime 기능 테스트만으로 자산 변경을 진행하지 않는다.

후속 자산 대상은 기존 `Prefabs/CombatRuntime.prefab`의 BP Debug, `Scenes/Dungeon_1_Production.unity`의 Runtime/Combat/CombatRuntime에 추가된 Hand/Clash/BP Debug, `Scenes/Dungeon_Template.unity`의 prefab instance/override, 기존 `Data/Combat/Runtime/FinalCombatRuntimeConfig_V1.asset`이다. 정식 참조가 준비된 뒤 component 원본/instance를 구분해 제거·연결하고 Missing Script/Reference와 6개 gate의 후속 조건까지 확인한다. 정확한 GUID/field·복구 절차는 P2-A/P2B1/B2 계획을 유지한다.

실행 직전 사용자 미커밋 상태 포함 백업/해시를 보존하고, 의도한 component/필드만 변경한다. 문제 발생 시 HEAD reset 대신 그 작업 전 사본에서 승인된 변경만 복구한다. 05/06A 수동 보류는 별도 지시 없이 해제하지 않는다.

## Development tracking

- System/Feature: Combat startup contracts / CBT-R2-08-P2B2C.
- Owner: 기존 CombatEntryPoint/Session/StateMachine/Orchestrator와 Skill/Hand/BP runtime 유지.
- Implementation: **설계 범위 정리 완료 — 정책 승인·후속 구현 Pending**. 충돌이 Production 코드에서 해결됐다고 기록하지 않음.
- Compile/Tests: 아래 최종 결과 참조. 새 C# 없음.
- Inspector/Play Mode/Integration: 기존 읽기 전용·격리 검증과 실제 Production 전환 구분. 후자 미검증.
- Recommended: **Contract Design Ready for Decision — Enemy Hand / Dual Placement / Clash Startup Pending**.
- Cutover/정식 수치 입력/Production 활성화 없음.

## 최종 실행 기록

Unity 6000.3.23f1 선택 회귀: **655 PASS / 6 FAIL / 1 IGNORED**, 총662 (`Logs/CBT-R2-08-P2B2C-Regression.xml`, `.log`). 종료2는 기존6개 Asset gate 실패를 반영한다. P2B2 XML과 실패 이름 비교 결과 차이 없음. 신규 실패 없음(선택 실행 범위).

포함: P2B1 초기화18개, Enemy behavior policy9개, 기존 snapshot/Hand/BP/Clash/인계·Ultimate·Item/Film, Legacy 관련220개 및 기존6개 실패 개별 실행. 직접적인 정식 Enemy Hand 선택·양쪽 배치·required Clash startup 테스트는 미구현/정책 미승인이어서 위 통과 개수에 해당 기능 검증으로 포함하지 않는다.

기존6개는 `DungeonOneProductionSceneTests`의 `ProductionDungeonOne_HasCanonicalCombatRewardUiCameraAndBootstrapOwners`, `ProductionDungeonOne_HasExplicitInteractionOwnersWithoutLegacyInteractionPaths`, `ProductionDungeonOne_HasOneCanonicalRepeatableNpcInteraction`, `ProductionDungeonOne_HasQuestDerivedCompletionContractWithoutLegacyExitOrFallbackDestination`, `ProductionDungeonOne_IsCleanEnvironmentMigration` 및 `ProductionSceneWiringTests.DungeonTemplate_IsCanonicalSingleOwnerProductionSource`다. 공통 namespace는 `Game.Tests.Integration`이며 상태는 모두 **Asset Change Required**. ignored는 기존 `Game.Tests.Integration.PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners`다.

새 C#이 없으며 이번 Unity 로그에서 compiler error/warning은 발견하지 못했다. 강제 전체 재컴파일을 실행한 것으로 기록하지 않는다. 기존 진단/Asset gate 예외는 compiler 오류와 구분한다. 전체 회귀와 실제 Production/Build/재시작 검증은 위 범위 제한대로 미실행이다.

기존 코드/보호 자산/미커밋 파일 **1,799개 전부 SHA-256 동일**. `Logs/CBT-R2-08-P2B2C-BeforeHashes.json` 기준. 신규 보고서 공백 검사 통과. 기존 파일 수정·삭제·이동 없음, 새 API/SerializeField/GUID/meta 없음. 새 산출물은 이 보고서와 무시된 Logs뿐이다. 관계없는 기존 변경·LegacyPlanning·Debug 검증 경로·05/06A 보류를 유지했다.
