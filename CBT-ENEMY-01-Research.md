# CBT-ENEMY-01 — 특수 몬스터 행동 구조 조사 및 설계

조사일: 2026-10-15. 조사·설계 전용이며 아래 신규 타입/필드는 구현 제안이다. 신규 몬스터, Production 코드/Asset은 생성하거나 변경하지 않았다.

## 1. 기준선과 지침

- 적용 지침: 루트 `AGENTS.md`. 중첩 `AGENTS.md`/`AGENTS.override.md` 없음.
- Branch: `main`. HEAD: `1ebe3dd9be008ad063654aa72308f832192119b4` (`12`).
- Unity: `6000.3.23f1 (09d2ecc7fb28)`.
- 기존 변경 보존: `PlayerFieldAttackController.cs`, `CombatPartyRosterPlayModeIntegrationTests.cs`, 미추적 `PartyRewardDiskSaveTests.cs`/`.meta`, `PlayerFieldAttackPartyRosterTests.cs`/`.meta`, `CBT-PARTY-05V-Verification.md`.
- 이번 추가 파일: 이 보고서. 변경/삭제/이동한 기존 소스, Asset, `.meta`, 공개 API, Inspector 연결 없음. commit/push 없음.

## 2. 확인한 기획과 확정 범위

원본 [기능구현 문서](https://docs.google.com/document/d/1JF92rWAxIJ-RNvqJL5lw8CAa_OsVQu61ohmvH9YfVkA/edit)의 「새로운 전투 — 대치 / 합 / 연계 시스템」, 특히 **23. 트리키 몬스터**를 직접 읽었다.

- 일반 적은 압박 게이지와 기본 전투 규칙을 따른다.
- 일부 트리키 몬스터 2~3종 및 특정 보스에 예외 행동을 사용한다.
- 제시된 예: 압박 게이지가 차기 전 페이크 공격, 공격하는 척하다 다른 행동, 예상한 타이밍과 다른 공격.
- 일반 몬스터에 이러한 예외를 남발하지 않는다.

이것은 예외 행동의 의도와 예시다. 몬스터별 이름/HP 임계치/발동 빈도/확률/우선순위/취소 시점/MP 처리/응답 기회/보스 phase/면역/텔레그래프 규칙까지 확정된 명세는 아니다. 이번 조사에서 이 값들을 만들거나 확정하지 않았다.

[게임 개발 기획서](https://docs.google.com/document/d/1gfyV1lSm2IXKR9Xx--43ICBXCMj9q5ny0_ekxWZB9e0/edit)도 확인했다. 오래된 턴·행동 슬롯·버프/반격 설명이 함께 남아 있어, 최신 FinalExchange에 그대로 옮길 수 없다. 예를 들어 피격 시 지정 스킬 자동 반격이라는 옛 설명은 현재 대응 선언→합과 별개의 규칙이다.

저장소의 `GAME002_EnemyEncounter_AuthoringGuide.md`는 boss phase AI가 Inspector-only Production 표준이 아님을 명시한다. 같은 문서 끝의 “Field Attack은 단일 플레이어 바인딩 경로” 설명은 현재 미커밋 PARTY-06 코드와 다르다. 이 부분은 오래된 안내이며 이번에 문서를 수정하지 않았다.

## 3. 실제 Production Owner와 흐름

| 단계 | 실제 Owner / 파일 | 책임 |
| --- | --- | --- |
| 필드 순찰/추적 | `Enemies/Runtime/FieldEnemyPatrolAI2D.cs`, `FieldEnemyMotor2D.cs` | Exploration에서 탐지·이동. 전투 스킬 판단 아님 |
| 접촉/공격 조우 | `CombatEncounterTrigger2D`, `PlayerFieldAttackController` | 기존 CombatStartRequest 작성 |
| Encounter | `CombatEncounterGroup` | 적 roster, 예약/해제, 종료 lifecycle, cleared 저장 |
| 전투 생성/정리 | `CombatEntryPoint` | 유일한 시작·종료 경계, FinalExchange config/Director/AI bind·unbind |
| 자동 진행/명령 창구 | `CombatFlowOrchestrator` | Attack/Response/Chain 결정 요청, version 검사, 단계 진행 |
| 적 명령 | `FinalCombatEnemyCommandController` | 적 요청만 구독해 policy 결과를 기존 명령으로 제출 |
| 현재 적 판단 | `FirstValidFinalCombatEnemyDecisionPolicy` | 유효한 첫 적·첫 스킬·첫 대상 선택 |
| 전투 상태 | `CombatSession`, `CombatantCombatState`, `CombatExchangeState` | HP/MP/체간/정신/상태이상, 공격권, 선언/commit/result |
| 실행 규칙 | `CombatStateMachine`, declaration/commit/clash/posture/mental/terminal 정책 | 합법성·비용·판정·결과 단일 적용 |
| 스킬 실행 | `Actions/SkillRunner.TryExecute` | 준비된 실행 요청의 피해/상태 적용, 결과 반환 |
| 연출 | `Effects/CombatDirector` | 접근·합·피격·느린 연계 선택 구간·VFX/SFX·완료 callback |
| UI | `FinalCombatPlayerCommandController`, `FinalCombatUIBinder`, status presenter | 현재 요청/상태 표시와 명령 전달 |

현재 연결은 `CombatEntryPoint.cs:398`에서 `new FinalCombatEnemyCommandController()`를 생성한다. 컨트롤러 생성자는 `IFinalCombatEnemyDecisionPolicy` 주입을 이미 지원하지만, Production EntryPoint는 기본 policy를 그대로 사용한다. 따라서 교체 가능한 판단 인터페이스는 있고 **적별 authored policy 선택/주입 경로는 없다**.

FinalExchange 흐름:

```text
CombatEntryPoint
  → CombatSession / CombatStateMachine
  → CombatFlowOrchestrator
      → Attack / Response / Chain 결정 요청
      → FinalCombatEnemyCommandController
      → IFinalCombatEnemyDecisionPolicy
      → SubmitAttackDeclaration / SubmitResponse / ContinueChain / EndChain
      → Core 검증 → Approach → Clash 또는 Unopposed
      → SkillRunner → Posture/Stun/Mental/Aftermath → Chain 또는 종료
      → CombatDirector 및 UI에 결과/표현 요청
```

## 4. 현재 적 AI가 읽고 선택하는 정보

- 공격: 요청된 actor가 있으면 그 actor, 없으면 roster 순서상 첫 active 적을 고른다. active는 생존·기절 여부에 기반한다.
- 스킬: loadout 순서로 MP 지불 가능한 스킬을 찾는다. 최종 비용은 `IFinalCombatSkillStats.FinalMpCost`를 우선한다.
- 대상: 반대 진영 roster 순서대로 선언 가능한 첫 대상을 고른다. 최저 HP/위협도/속성 약점/예상 합 승률 평가가 아니다.
- 대응: 공격 대상으로 지정된 적이 자기 스킬 순서에서 비용·선언 조건을 만족하는 첫 스킬을 선택한다. 불가하면 NoResponse.
- 연계: 현재 actor가 MP·타깃·생존/기절/패닉 조건상 계속할 수 있으면 계속, 아니면 종료한다. HP에 따른 위험 평가나 계획적 MP 보존은 없다.
- 패닉: 컨트롤러가 대치·공격권·생존·비기절 조건을 확인해 Overcome을 시도한다. 공격 불가 시 다른 eligible panicked actor를 찾는 경로도 있다.
- 압박: `CombatStateMachine.AdvanceStandoff`가 MP를 회복시키고 압박을 증가시킨다. 완충 시 `OnEnemyActionRequired` → Orchestrator가 적 공격권과 요청을 발행한다.
- 압박은 현재 **세션 공통 `StandoffRuntimeState` 하나**이며 적마다 별도 게이지가 아니다. `FinalCombatRuntimeConfigSO`도 현재 EntryPoint/세션 설정이다.
- HP/MP/정신/체간/StatusSnapshots, 현재 선언/연계 정보는 세션에 있어 후속 policy가 읽을 수 있다. 현재 기본 policy는 HP 비율/특정 상태 ID/phase threshold에 따라 전략을 바꾸지 않는다.
- `ExchangeState.Version`은 선언·응답·결과 등의 상태 변이에 따른 낡은 명령 차단 값이다. “전투 N회째 행동/Exchange” 카운터로 사용하면 안 된다. `TurnIndex`의 Legacy turn도 FinalExchange 행동 횟수와 동일시하지 않는다.

## 5. 현재 지원 행동과 제한

| 행동 | 현재 지원 | 제한/구분 |
| --- | --- | --- |
| 공격, 대상 선택 | 지원 | 기본 AI는 첫 유효 선택 |
| 대응 스킬 선언, 합 | 지원 | 공격 대상으로 지정된 responder만 대응. 합 승자의 공격을 실행 |
| NoResponse / Unopposed | 지원 | 대응 불가/선택 안 함. 합 난수 없이 공격 실행 |
| 합 승리 후 공격권 전환 | 지원 | Core authority policy에서 결정 |
| 적 연계 계속/종료 | 지원 | 기본 AI는 유효 스킬이 있으면 계속. 전략적 종료 분기 없음 |
| 동료 인계 | Core 지원 | 같은 진영, 조건 승인, 연계당 1회. 현 적 컨트롤러는 인계 명령을 선택하지 않음 |
| 피해 및 상태 부여 | 지원 | Skill SO의 피해·status 배열, damage/clash/posture modifier |
| 상태 중첩/갱신/교체/만료 | 지원 | 완료된 Exchange 참가자 기준으로 감소, 이번에 바뀐 상태 제외, 중복 callback 방지 |
| 체간·기절 | 지원 | 합 패자 체간 증가·상한 시 stun, 공유 행동 제한/후속 처리. 일반 Status SO의 임의 행동금지 효과와는 별개 |
| 정신·패닉·극복 | 지원 | 합 패배/피해/기절의 정신 감소. 패닉은 공격·대응·연계 제한. 극복은 허용된 대치/공격권에서 회복 후 상대에게 권한 이양 |
| 총공격 | 지원 | 살아 있는 적 전부 기절 시 후보. 현재 실행은 eligible 아군의 피해 있는 AllEnemies 스킬을 사용. 적 AI의 총공격 선택은 없음 |
| 방어/회피 | tag 존재 | `Defense`/`Dodge` enum 자체가 피해 경감/회피 성공/별도 action의 구현을 뜻하지 않음. SkillRunner에 tag 전용 규칙 없음 |
| 반격 | 대응 합은 지원 | 옛 문서의 피격 후 예약 스킬 자동 발동, 피해 반사, 재반격은 별도 미구현 |
| 선언 취소 | 제한된 Core API 존재 | `TryCancelAttackDeclaration`: AttackDeclaration이고 아직 commit 전만 가능. Orchestrator/AI에 페이크 전용 명령·연출 계약은 없음 |
| 회복·보호막·도발·지속피해·소환·변신 | 일반 Enemy Skill 데이터만으로 미지원 | 일부 Item 회복이 있다는 사실을 적 Skill 지원으로 확대 해석하지 않음 |
| 보스 phase/HP 조건 패턴/페이크 | authored Production 지원 없음 | 별도 명세와 확장 필요 |

Targeting enum 역시 보장 범위가 다르다. 현재 FinalExchange target resolver는 None/Self/SingleEnemy/SingleAlly/AllEnemies/AllAllies를 처리하지만 AnySingle/Environment는 거부한다. 기본 AI의 공격 선언은 적대 target을 선택하므로 SingleAlly 회복·버프를 현재 AI가 자동 선택한다고 볼 수 없다.

## 6. SO만으로 가능한 것과 코드가 필요한 것

기존 `SkillDefinitionSO`로 표현 가능:

- 기존 스킬 ID/이름, finalMpCost, clashPower, baseDamage, speed, targeting.
- 기존 `CombatStatusDefinitionSO`의 damage/clash/posture modifier, 중첩·지속 설정.
- 이동 방식·접근 거리·속도·action delay, animation trigger, cast/impact VFX/SFX.
- loadout을 바꾸어 현재 AI의 첫 유효 스킬 우선순위를 바꾸는 것.

이 SO는 “무슨 스킬인가”를 표현한다. “HP가 얼마일 때, 몇 번째 Exchange에, 얼마나 자주, 어떤 phase에서 고르는가”는 표현하지 못한다. 연출 지연 값을 바꾸는 것은 페이크 취소/새 공격권/새 응답 창을 만들지 않는다.

현재 `EnemyDefinitionSO`는 `persistentKey`와 `acquirableSkillPersistentKeys`만 가진다. 일반/특수/보스 구분, 능력치 표, 패턴, 조건·우선순위는 없다. `EnemySourceSnapshot`도 획득 provenance만 보존한다. GUID 참조 검사에서 기존 GAME Scene/Prefab/Data의 EnemyDefinition SO asset은 발견되지 않았다. 기존 무정의 적의 호환 동작을 유지해야 한다.

추가 코드가 필요한 범위:

1. HP/MP/상태 등으로 기존 공격·응답·연계 종료 선택을 바꾸기: 기존 policy 확장과 명시적인 적별 profile 연결. 전투 수식 재구현 불필요.
2. N회 사용 후 행동 전환/1회 발동/phase 전환: 세션별·적별 runtime memory와 정확한 성공/완료 이벤트가 필요.
3. 압박 완충 전 예외 행동: 대치에서 평가하는 승인된 Core/Orchestrator 명령 경로가 필요. policy 교체만으로 새로운 호출 시점을 만들 수 없다.
4. 페이크 후 다른 행동: commit 전 취소 API를 출발점으로 재사용하되 version을 검증하는 명령, 시간/대응 창, 비용과 압박 처리, 새 표현 요청이 필요. commit 이후 임의 피해·비용 롤백을 전제로 설계하지 않는다.
5. 신규 방어/회피/예약 반격/면역 등: 해당 공통 전투 규칙부터 구현. 특정 몬스터 MonoBehaviour가 HP나 phase를 직접 수정하는 방식은 피한다.

## 7. Legacy / Demo / 호환 경로

- `DeterministicCycleEnemyCombatPolicy` (`IEnemyCombatPlanPolicy`, `IEnemyCombatDecisionPolicy`): TurnIndex 기반 순환 스킬과 첫 유효 대상. Orchestrator의 Legacy plan 구성에 쓰인다. 현 FinalExchange `FirstValid...`와 같은 AI로 합치면 기존 턴 스킬 순서를 바꾸게 된다.
- `CombatStateMachine.TrySubmitEnemyDecision`의 이전 대치 policy 경로는 완충 조건을 요구한다. 현 Production FinalExchange의 이벤트→명령 controller와 별개로 보존한다. 두 경로를 동시 실행하지 않는다.
- `CombatTurnResolver`, `SkillRunner.Resolve`와 Legacy playbook 재생: 옛 turn/planning 호환. FinalExchange의 `SkillRunner.TryExecute` 경로와 구분한다.
- `Game.Battle.FieldEnemy`: 옛 접촉/피격→EntryPoint 요청 및 DemoMission 결합을 남긴 호환 경로. GUID는 `Test.unity`에서 확인했고 Production Dungeon에는 없었다.
- `Game.Enemy.Overworld.OverworldEnemyAI`: 별도 순찰/추적 구현. GUID는 `Test.unity`에서 확인했다.
- Production `FieldEnemyPatrolAI2D` GUID는 `Dungeon_1_Production`, `Dungeon_Template`, `Dungeon 1`, `Testing_Dungeon_Template`에서 확인했다.
- Demo/Debug 적 AI나 field 이동 로직에 FinalExchange 특수 패턴을 추가하지 않는다. 이번에 정리/삭제/마이그레이션하지 않았다.

## 8. 권장 최소 확장 구조 — 제안

### A. 먼저 기존 합법 행동의 선택만 확장

- 기존 `IFinalCombatEnemyDecisionPolicy`와 controller를 유지한다.
- 기본 `FirstValidFinalCombatEnemyDecisionPolicy`는 그대로 fallback으로 유지한다. opt-in profile이 없는 적은 순서·선공·MP·연계 동작이 같아야 한다.
- 적별 profile을 읽는 **하나의 decision policy**가 세션 roster/profile에 따라 판단한다. 별도 적 manager, CombatEntryPoint, SkillRunner를 만들지 않는다.
- `requestedActor`가 null인 최초 공격과 현재 actor가 지정된 연계/응답을 구분한다. 서로 다른 일반/특수 적이 섞인 encounter에서 profile을 다른 적에게 적용하지 않는다.
- 판단은 읽기 위주로 수행한다. 스킬·대상/응답 여부/연계 여부를 제안하고 기존 declaration/commit policy가 최종 검증한다. MP 소비·피해·Status 적용은 기존 Core만 수행한다.

### B. 정적 profile과 실행 상태 분리

제안 타입명: `EnemyBehaviorProfileSO` / 순수 데이터 snapshot / 세션의 적별 behavior state. 실제 이름과 파일 수는 구현 Batch에서 결정한다.

- 기존 `EnemyDefinitionSO`에 선택적 profile 참조를 추가하는 방향을 우선 검토한다. 없으면 기본 AI.
- 기존 `EnemySourceComponent`의 definition 연결을 재사용할 수 있다. 기존 Film mapping 유무와 행동 profile 유무는 독립적으로 처리한다.
- `FieldCombatantFactory`의 기존 field→combat 경계에서 profile을 읽어 순수 데이터로 복사하고 CombatSession에 actor별로 등록하는 방식이 최소다. Core가 SO/GameObject를 찾거나 런타임에 SO를 변경하지 않는다.
- `EnemySourceSnapshot`은 Film 획득 계약 그대로 둔다. AI profile을 획득 ledger에 섞지 않는다. 명시적인 request snapshot이 필요해지면 기존 `CombatStartRequest`의 별도 optional mapping을 검토하되 처음부터 필수로 늘리지 않는다.
- 발동 횟수/완료된 Exchange 수/1회 트리거/phase/현재 예고 행동은 session별 state에 보관하고 종료·abort·재bind 시 초기화한다. 저장 schema 추가는 이번 설계의 전제가 아니다.
- 조건 평가/불가 후보 탐색/preview는 카운터를 증가시키지 않는다. 명령 승인 또는 실제 Exchange 완료 중 명세가 정한 시점에 한 번만 기록한다.

### C. 페이크·비정상 타이밍은 후속 단계

- 새 공통 적 AI로 일반 몬스터 전체를 바꾸지 않고 승인된 profile만 예외를 허용한다.
- 대치 중 예외 시도, 플레이어 선언, 압박 완충이 같은 프레임에 경쟁하면 Orchestrator와 StateMachine의 현재 session/version/phase를 기준으로 한 건만 승인한다.
- 예고→취소/확정→결과의 의미를 먼저 명세한다. 정상 Approach를 몰래 생략하거나 Director callback에서 스킬을 교체하지 않는다.
- 기존 `TryCancelAttackDeclaration`의 commit 전 제한은 보존한다. commit 이후 변경이 필요하면 별도 합법 전이·비용 명세부터 설계한다.
- `ExchangeState.Version`을 소모 횟수로 삼지 않는다. 새 예고/phase 상태가 실제로 필요한 경우에만 기존 상태 모델에 최소 추가한다.

## 9. 후속 구현의 최소 수정 후보

아래는 수정 계획 후보이며 이번에 변경한 목록이 아니다.

| 범위 | 기존 수정 후보 | 추가 후보 / 이유 |
| --- | --- | --- |
| 기존 행동 선택 확장 | `CombatEntryPoint.cs`의 policy 생성 주입, `EnemyDefinitionSO.cs`의 optional profile, `FieldCombatantFactory.cs`와 `CombatSession.cs`의 적별 데이터 전달 | profile SO, rule-based decision policy, 순수 snapshot/state, 관련 테스트 |
| 위 과정에서만 필요 | `FinalCombatEnemyCommandController.cs` | 기존 인터페이스로 되면 수정 불필요. 적별 명령 성공 bookkeeping이 필요할 때만 최소 확장 |
| 압박 전 예외/페이크 | `CombatFlowOrchestrator.cs`, `CombatStateMachine.cs`, 필요 시 `CombatExchangeState.cs`와 presentation request 모델 | version 검증된 예외 요청/결과 계약 |
| 새 전투 효과가 확정됐을 때만 | `SkillRunner.cs`, 해당 declaration/target/status 정책과 Definition | 공통 효과 구현. 단순 AI 선택 변경에는 수정하지 않음 |
| 전용 예고/UI가 확정됐을 때만 | `CombatDirector.cs`, `FinalCombatUIBinder.cs`/view state | cue, 표현 상태, 취소/완료 callback |

처음부터 이 파일을 모두 수정하지 않는다. Contact/Field Attack, Party Adapter, Reward, Save, 기존 Production Scene/Prefab은 선택 정책 확장의 필수 수정 대상이 아니다. 연결은 이후 별도 승인 콘텐츠/Scene Batch에서 수행한다.

## 10. SO 데이터 설계 방향

profile의 후보 구성은 다음과 같으며 실제 몬스터 규칙/수치는 미확정이다.

- profile 식별자, 기본 fallback, 결정 단계(Attack/Response/Chain).
- 명시적 rule priority와 동일 우선순위의 안정적 순서.
- 조건: HP/MP 비율, status ID, mental/posture, 현재 phase, 완료된 행동/Exchange 카운터 등 승인된 것만.
- 선택 결과: 등록된 skill ID/참조, 허용된 target selector, 대응 거부, 연계 종료 등 기존 명령.
- 반복 제한/1회 조건/단계 전이는 runtime state와 결합한다. SO에 current HP, 카운터, cooldown 진행값을 쓰지 않는다.
- 스킬 미등록/소유 안 함/중복 rule ID/불가능한 targeting/범위 역전/금지 phase는 validator로 잡는다. 잘못된 특수 규칙이 Core 합법성 검사를 우회하면 안 된다.
- 페이크의 선행 시간, 압박 임계치, 빈도, 취소 가능 구간, 응답 처리, MP/압박 소비, 공개 정보는 별도 기획 확정이 선행돼야 한다.

## 11. UI와 연출 영향

- HP/상태 조건으로 기존 스킬을 고르는 것만으로는 새 UI/Director가 필요하지 않다. 기존 Skill SO 표현을 재사용한다.
- 현재 UI 압박 표시는 session 공통 값이며 HP/MP/체간은 대표 player와 선택/첫 enemy 중심이다. 적별 예고·boss phase UI는 아직 없다.
- 페이크는 “예고”와 “실제 commit/피해”를 분리해 표시해야 한다. 정보를 전부 공개할지 일부만 보여줄지도 기획 사항이다.
- AI는 연출 길이나 Time.timeScale로 판정을 계산하지 않는다. Core는 승인된 전이·결과, Director는 애니메이션/카메라/VFX/SFX/완료 알림, UI는 표시와 명령만 소유한다.
- disable/abort/session 교체/중복 callback/느린 연계 선택 종료 시 예고와 느린 시간 배율을 정리해야 한다. 기존 binding/version 보호를 재사용한다.

## 12. 필요한 자동 테스트와 구현 Batch 우선순위

| 우선순위 / Batch 제안 | 내용 | 완료 기준 |
| --- | --- | --- |
| P0 / ENEMY-02 명세 | 첫 특수 적 하나의 실제 조건·행동·빈도·우선순위·예고·취소·MP/압박 규칙 확정 | 예시를 확정 규칙으로 승격하는 기획 승인 |
| P1 / ENEMY-03 선택 확장 | 기존 interface에 opt-in profile과 기존 행동 선택 조건 추가 | 무profile 적 완전 호환, 혼합 roster 및 HP/상태 경계 테스트 |
| P2 / ENEMY-04 예외 전이 | 승인된 압박 전 시도/페이크의 최소 전이 | 플레이어/압박/예외 경쟁, version·commit·비용·중복 방지 통과 |
| P3 / ENEMY-05 연출/UI | 승인된 cue/telegraph와 표시 | disable·중단·session 교체에서 callback/timeScale 누수 없음 |
| P4 / ENEMY-05V 통합 | 메모리 Scene Contact/Attack→3인 FinalExchange→결과 | mixed enemy, KO/EXP/Quest/Completion/Save 회귀, 후속 전투 초기화 |
| 별도 콘텐츠 Batch | 승인된 SO/콘텐츠와 Production Scene 연결 | Inspector·Dungeon Play Mode·Player Build를 각각 기록 |

필수 테스트 세부:

1. 기존 기본 적의 actor/skill/target 순서·선공·첫 유효 MP·NoResponse·연계 종료가 동일함.
2. HP 임계점 바로 전/동일/직후, 사망/기절/패닉, status 부여/만료, MP 부족에서 정책과 Core 결과가 일치함.
3. 우선순위 충돌, 불법 스킬/대상, 대응 거부, 전 rule 불가 시 fallback, 일반/특수 혼합 roster.
4. 취소·실패·preview에서 카운터/MP가 증가·소비되지 않고 성공/완료 시 한 번만 변함.
5. 플레이어 선언·압박 완충·특수 트리거 같은 프레임 경쟁. 낡은 version/session, 중복 명령과 callback 거부.
6. commit 전 취소 후 정상 재진입, commit 이후 잘못된 변경 거부, 회복·압박·Stun/Overcome/All-Out 계약 보존.
7. 실제 EnterPlayMode에서 public field attack과 실제 Contact physics 경로를 구분해서 검증. 강제 종료와 실제 FinalExchange resolution 승리도 구분.
8. Contact/Attack 예약·해제/시작 실패/종료/다음 Encounter, Party participant snapshot, EXP/Gold/Quest/Film/Save 중복 차단 회귀.

## 13. 이번 실제 검증 결과

- 전체 EditMode를 정상 권한 환경에서 **한 번 재시도**했다. Unity process exit **0**.
- `Logs/CBT-ENEMY-01-FullEditMode.xml`: **1,368 / 1,368 Passed**, failed/inconclusive/skipped 각 0, 테스트 duration 6.7726862초.
- `Logs/CBT-ENEMY-01-FullEditMode.log`: `Tundra build success`, `Test run completed. Exiting with code 0 (Ok)` 확인. 이번 로그에 C# `error CS` 없음.
- 포함 사례: Enemy command 8, Enemy policy 5, Field Attack Party 10, Party roster 16, Status FinalExchange 11, Overcome 5, Terminal execution 7. 이 숫자는 전체 결과의 부분집합이다.
- Editor 테스트 assembly의 기존 `UnityTest`들은 실제 EnterPlayMode를 포함한다. 별도 PlayMode assembly/Player Build/실제 Dungeon 수동 플레이 또는 신규 특수 적 행동 검증을 했다는 의미는 아니다.
- 경고/진단: 기존 `OfficeFlowController.cs.meta` GUID 파싱/유효성 문제와 빈 `Scenes/Tests/Tests.asmdef` 안내가 남아 있다. 테스트에는 의도적으로 예외/오류를 유발하는 음성 검증도 포함되므로 “전체 Console Error 0”으로 보고하지 않는다. 결과 XML에서는 전부 통과했다. 해당 `.meta`는 수정하지 않았다.
- `git diff --check`: whitespace 오류 없음. 기존 TimeManager.asset LF→CRLF 안내만 출력됐으며 파일 변경은 없다.
- 새 특수 적은 미구현이다. 이번 권장 개발현황은 **조사·설계 완료 / 구현 명세 확정 필요 / 기존 전체 EditMode 회귀 통과**다. 특수 적 System Complete로 기록하지 않는다.

## 코드 근거

저장소 기준 위치:

- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatEntryPoint.cs:398`
- `Assets/GAME/Scripts/Combat/Runtime/Core/FinalCombatEnemyCommandController.cs:18`
- `Assets/GAME/Scripts/Combat/Runtime/Core/FirstValidFinalCombatEnemyDecisionPolicy.cs:12`
- `Assets/GAME/Scripts/Combat/Runtime/Core/IFinalCombatEnemyDecisionPolicy.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatFlowOrchestrator.cs:498`, `:777`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatStateMachine.cs:226`, `:984`, `:1573`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatDeclarationPolicy.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatExchangeCommitPolicy.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatOutcomeTargetResolver.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/FinalCombatClashRule.cs`, `FinalCombatPostureRule.cs`, `FinalCombatMentalRule.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatOvercomePolicy.cs`, `CombatAllOutExecutionPolicy.cs`, `CombatHandoffPolicy.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Actions/SkillRunner.cs:8`, `SoSkill.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Model/CombatSession.cs`, `CombatantCombatState.cs`, `CombatExchangeState.cs`, `StandoffRuntimeState.cs`
- `Assets/GAME/Scripts/Combat/Data/SkillDefinitionSO.cs`, `CombatStatusDefinitionSO.cs`, `FinalCombatRuntimeConfigSO.cs`
- `Assets/GAME/Scripts/Enemies/EnemyDefinitionSO.cs`, `Runtime/EnemySourceComponent.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Adapters/FieldCombatantFactory.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Model/EnemySourceSnapshot.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Effects/CombatDirector.cs`
- `Assets/GAME/Scripts/Combat/Runtime/UI/FinalCombatUIBinder.cs:380`
- `Assets/GAME/Docs/GAME002_EnemyEncounter_AuthoringGuide.md`, `GAME002_SkillContent_AuthoringGuide.md`
