# CBT-R2-08-P2B3A — Playtest V1 설정 구성 및 자동 검증

2026-10-30 KST. 승인된 **Playtest V1** Hand/BP 값을 기존 Config 타입으로 작성했다. 최종 밸런스/정식 콘텐츠가 아니며 Production 초기화, Scene/Prefab 전환 및 Cutover는 하지 않았다.

## 지침·Git·조사

- 활성 지침: 루트 `AGENTS.md`; 중첩 지침 없음. Branch `main`, HEAD `6fa26d294472574097d33b884858ba0ee8bdc8b7` 유지. Unity 6000.3.23f1.
- P2B1/P2B2/P2B2C/P2B2D 보고서와 실제 Config/EntryPoint/Bootstrapper/Session, Hand/BP/Ultimate/Clash Runtime, snapshot bridge, Enemy 정책, Debug opt-in, 기존 Scene/Prefab/Config/meta 및 관련 테스트를 조사했다.
- 시작 상태·기존 tracked/untracked 해시는 `Logs/CBT-R2-08-P2B3A-BeforeStatus.txt`, `...-BeforeHashes.json`에 기록했다. 기존 변경을 덮어쓰지 않았다.
- Runtime 코드는 이미 승인값을 표현한다. 새 Runtime 필드·Owner·전투 경로·영구 데이터 모델 없이 테스트 전용 데이터와 검증만 추가했다.
- 직전 P2B2D 선택 기준선은 **720 PASS / 6 FAIL / 1 IGNORED**(727개), 이전 전체 기준선은 **1,562 / 8 / 1**이다. 이번 실행과 구분한다.

## 데이터 및 정확한 필드

새 SO: `Assets/GAME/Tests/Editor/Fixtures/CombatPlaytestV1/FinalCombatRuntimeConfig_PlaytestV1.asset`.
기존 `FinalCombatRuntimeConfigSO` 타입을 사용한다. `TestOnly`, `PlaytestV1`, `ContentPending`, `ClashPending` 라벨과 Editor 전용 경로로 구분한다. Production 자산에서 참조하지 않는다.

| 승인 항목 | 저장 필드 또는 기존 Runtime 계약 | 값·범위 |
|---|---|---|
| 일반 손패 | `normalHandCapacity` | 2 |
| 합 손패 | `clashHandCapacity` | 3 |
| 초기 BP | `CombatBpRuntime` 생성자, 별도 SO 필드 없음 | 참가자별 0 |
| BP 상한 | `bpMaximum` | 3 |
| 획득 주기 | `bpRoundCyclesPerGain` | 유효 RoundCycle 2회 |
| 획득량 | `CombatBpRuntime.GrantIfActive` | 주기마다 +1, 상한 적용 |
| 행동당 투자 | `bpMaximumInvestment`, 기존 `CanInvest` | 0~3 및 보유량 이내 |
| Ultimate 충전 | `CombatUltimateRuntime.TryAcceptApprovedSpend` | 동일 캐릭터 승인 BP 누적 소비 1/2/3에서 Stage 1/2/3 |
| 최대 단계·실행 | `MaximumStage=3`, 기존 전용 명령 | Stage 3, 기존 phase/권한/상태/MP/대상 조건 적용 |
| 실행 후 초기화 | 기존 `TryConsumeReadyStage` | 정상 실행 후 Stage 0 |
| 자동 초기화 | `enableProductionHandBp` | false 유지 |

저장 데이터의 `handCardRules`, `ultimateSkills`, `enemyHandSourceKeys`는 비워 두었다. `handSeed`와 Clash 값은 미작성 기본값이며 제작 승인값이 아니다. SO 타입의 기존 MP/Posture/Mental 기본값도 Unity 직렬화에 포함되지만 이번 Playtest 승인·새 밸런스 확정으로 해석하지 않는다. 해당 필드나 기존 Production SO는 변경하지 않았다.

BP 획득은 양측의 서로 다른 성공한 AttackCycle이 짝을 이룬 RoundCycle 기준이다. 프레임, 시간 경과 또는 Clash 비교 횟수를 BP 주기로 사용하지 않는다. KO/Stun은 기존 획득 제외 정책을 유지한다. Panic은 기존 코드상 BP 획득 대상에서 별도 제외하지 않으며 공격/투자/Ultimate 명령 제한으로 처리한다. 새 Panic 획득 정책은 만들지 않았다.

## 작성·초기화·검증 경계

새 테스트 클래스의 명시적 Editor 메서드 `CreatePlaytestAssetIfMissing`를 Unity batch에서 호출해 새 SO/폴더/meta를 생성했다. 기존 SO가 있으면 반환하며, 새 Config에 승인된 5개 수치만 입력한다. 전체 dirty asset을 저장하는 `SaveAssets`를 사용하지 않고 해당 새 asset만 저장한다. 자동 전투 시작 installer가 아니다.

검사는 실제 저장 SO를 읽고 **메모리 clone**에만 기존 P2B2D의 Clash fixture 3/3/3/5초 및 명시적 selector를 공급한다. 기존 fixture의 normal4/clash6 copies와 임시 Skill SO/Character ID/Enemy Source Key, 효과/MP/BP enhancement도 메모리에서만 사용한다. 이를 정식 데이터로 저장·승격하지 않는다.

기존 `CharacterSkillRuntime → 장착 snapshot → CombatEntryPoint → Bootstrapper → Session` 경로로 초기화한다. Session별 Hand/BP/Ultimate와 첫 pump 전 Clash 설치는 P2B2D 구현을 그대로 사용한다. 손패 instance는 Session RuntimeId로 구분하고 persistentKey는 스킬 저장 정체성으로 유지한다. Normal/Clash 양쪽 동시 rule 거부와 Source Key별 Enemy Hand 선택은 기존 계약 및 P2B2D 회귀를 유지한다.

Production Owner: `CombatEntryPoint`, `CombatSession`, `CombatStateMachine`, `CombatFlowOrchestrator`, 기존 SkillRunner. GameFlowController/UI/Input/Inventory/Reward/Save 소유권 및 LegacyPlanning/Debug 경로 무변경.

## Clash 설정: 미승인 제작값과 기존 규칙

| 항목 | 현재 코드 | 승인/작성 상태 |
|---|---|---|
| 새 Hand/BP 모드의 Clash 필수 | EntryPoint 사전 검증, Opening Effect/첫 명령 이전 거부 | P2B2D 승인 계약 유지 |
| 시작 강인도 | `clashAttackerDurability`, `clashResponderDurability` 양수 필수 | **Content Pending**. 저장 SO는 0, fixture 자동 주입 없음 |
| 최대 비교 수 | `clashMaxComparisons` 양수 필수 | **Content Pending**. 저장 SO는 0 |
| 선택 시간 | `clashDecisionDurationSeconds` 유한 양수 필수 | **Content Pending**. 저장 SO는 0 |
| 승자 피해 스킬 | 명시적 `clashFinalSkillPolicy`; 현재 지원은 마지막 승인 승자 스킬 | 제작 프로필 선택 Pending, 저장 값 Unspecified |
| Timeout 공격 | 명시적 `clashTimeoutAttackPolicy`; 현재 지원은 첫 가용 스킬 | 제작 프로필 선택 Pending, 저장 값 Unspecified |
| 위력 | Skill의 ClashPower + 기존 상태/BP 보정 | 정식 Skill 및 계수 Content Pending. 별도 위력 Config 생성 안 함 |
| 비교 결과 | 우세 위력 차이만큼 상대 강인도 감소; 동점이면 양쪽 -1 | **Existing Runtime Rule**, 이번 변경 없음 |
| 동시 소진 | Speed, 이후 공격권 소유 기준으로 승자 결정 | **Existing Runtime Rule**, 수치 승인과 구분 |
| 안전 종료 | 비교 상한 도달 시 Tie, 다음 공격 불가 시 기존 Tie 종료 | **Existing Runtime Rule**, 이번 변경 없음 |
| 실제 Series 생성 | 첫 공격/대응 commit 후 기존 StateMachine/Orchestrator 경로 | 기존 소유권 유지. Hand/BP 작성만으로 생성/시작 성공 처리 안 함 |

이 표는 기존 계산 규칙이 최종 밸런스 승인을 받았다는 의미가 아니다. 정식 강인도/위력/반복 상한/timeout을 새로 만들지 않았다.

## 파일·API·GUID 영향

추가:

1. `Assets/GAME/Tests/Editor/CombatPlaytestV1ConfigTests.cs` 및 Unity 생성 meta.
2. `Assets/GAME/Tests/Editor/Fixtures/CombatPlaytestV1.meta`.
3. 위 폴더의 `FinalCombatRuntimeConfig_PlaytestV1.asset` 및 meta.
4. 이 보고서와 무시되는 Logs 검증 기록.

기존 파일 수정·삭제·이동 없음. 기존 public Runtime API/SerializeField/enum/Save schema 무변경. 새 public API는 테스트 전용 작성 메서드/경로 상수뿐이다. Unity가 발급한 GUID:

- SO `7842331559c851f489f18452af765b0b`.
- 테스트 script `f22a0b11a8a28e846b300008cc4f74e3`.
- fixture folder `d7e0f9c1a8de5cf43b37d3fc66c591df`.

기존 Config script GUID `914e9a1f4d1248d982fb012634938a2a`를 정상 참조한다. 기존 Production Config `FinalCombatRuntimeConfig_V1.asset` 및 Scene/Prefab, 기존 meta/GUID는 보존한다.

## 테스트 및 기존 실패

실행 결과는 아래 최종 기록에 기재한다. 새 검사는 실제 SO 읽기, 1~3인 snapshot/독립 Hand·카드 ID, 초기 Draw 한 번/3·2 슬롯/종류별 discard/refill, Source Key opt-in 및 일반 적 후보, BP 초기0/상한3/2회 유효 주기, 중복·무효 cycle, -1/0/1/2/3/4 투자, 승인 receipt와 중복 거부, 캐릭터별 Stage 1/2/3 및 전용 명령 후 reset, KO/Stun/Panic 거부, 인계 유지, 재전투 reset, Clash 누락 거부, 비활성 호환 및 unsaved Play Mode를 검증한다. 최종 보강 검사는 잘못된 Utility/SingleAlly 지원 대상 명령의 거부, 양측 양보 및 시간 경과에서 BP/Stage/cycle/비용/손패 무변경을 확인한다. 새 지원 전용 명령이나 유효 지원 행동의 별도 주기 정책은 구현하지 않았다.

기존 실패 6개는 모두 `Game.Tests.Integration` namespace이며 **Existing Confirmed / Asset Change Required** 상태다:

1. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasCanonicalCombatRewardUiCameraAndBootstrapOwners`
2. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasExplicitInteractionOwnersWithoutLegacyInteractionPaths`
3. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasOneCanonicalRepeatableNpcInteraction`
4. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasQuestDerivedCompletionContractWithoutLegacyExitOrFallbackDestination`
5. `DungeonOneProductionSceneTests.ProductionDungeonOne_IsCleanEnvironmentMigration`
6. `ProductionSceneWiringTests.DungeonTemplate_IsCanonicalSingleOwnerProductionSource`

앞5개는 Dungeon Scene의 Hand Debug 선행 검사, 마지막은 Template→CombatRuntime prefab BP Debug 존재 검사다. 이번에 Debug 참조를 제거하거나 검사를 완화하지 않았다.

전체 회귀는 보호 Scene 설치/저장 함수를 호출하는 기존 테스트 때문에 미실행한다. 05/06A 수동 보류 유지. 실제 Production Inspector/수동 Play Mode, Player Build, 별도 프로세스 디스크 재시작 Save 검증도 실행하지 않았다. EditMode suite 안의 EnterPlayMode 자동 fixture와 실제 Production 통합을 구분한다.

## P2B3 전환 조건·Inspector 및 수동 절차

- 현재 SO는 **준비된 Playtest 숫자 프로필**이며 콘텐츠/Clash가 없는 비활성 데이터다. 실제 전투 시작용 완성 Config로 표시하지 않는다.
- 정식 또는 별도 승인된 Playtest Character ID/Skill 장착/Enemy authored snapshot, registry, Hand kind/copies/seed, 필요한 Source Key와 EnemySource 연결을 준비한다. Ultimate 효과와 추가 MP 비용, 총공격 콘텐츠는 계속 Pending이다.
- Clash 강인도/위력/최대 반복/timeout 및 제작 selector 선택을 별도로 승인·작성하고, 전체 초기화/실패 rollback 검증을 통과해야 한다.
- 실제 보호 자산 전환·Debug 분리는 별도 승인 후 기존 P2B2D 계획대로 원본 prefab/Scene instance override를 구분하여 최소 변경한다. 전환 전 현재 미커밋 상태 사본/해시를 보관하고, 문제 시 작업 직전 사본에서 승인 변경만 복구한다. HEAD reset 금지.

수동 확인 순서(이번 미실행):

1. 새 테스트 SO Inspector에서 3/2·BP3/2/3, enable=false, 빈 content/Clash와 라벨을 확인한다. Production 참조에 연결하지 않는다.
2. Test Runner의 새 fixture 및 P2B2D 검사를 실행해 저장 자산 비활성/미변경과 메모리 clone의 성공·실패 경계를 확인한다.
3. 후속 승인된 자산 전환 이후 Contact/FieldAttack, 1~3인 독립 Hand, 인계/빈 Hand/Enemy opt-in, BP·Ultimate/KO/Stun/Panic, Clash 및 Timeout/중복/Abort/재전투, Reward/EXP/Input/UI 복귀를 실제 씬에서 검증한다.
4. 기존 실패6개, Missing Script/Reference, prefab override/GUID/UnityEvent 및 YAML diff를 확인한다. 05/06A 검증 보류는 별도 해제 지시가 있어야 한다.

## Development tracking

- System: Combat / Playtest V1 Hand-BP-Ultimate configuration.
- Requirement: CBT-R2-08-P2B3A 사용자 승인 Playtest 값. 최종 밸런스와 구분.
- Owner: 기존 CombatEntryPoint / Session / StateMachine / Orchestrator 및 Hand/BP/Ultimate Runtime.
- Implementation: Playtest 데이터 구성·자동 검증 완료, Runtime 동작 변경 없음.
- Compile/자동 검사: 아래 최종 기록.
- Inspector/serialized asset: Unity 생성/자동 로드 확인; 실제 Production Inspector Pending.
- Play Mode: unsaved 자동 fixture와 실제 Production 수동 검증 구분. 후자는 미실행.
- Integration/Cutover: Pending. Content/Clash 작성과 별도 자산 전환 승인 필요.
- 권장 상태: **Playtest V1 Config Prepared — Automated Fixture Verified; Production Clash / Content / Asset Wiring / Integration Pending**.
- 이전 문서의 손패3/2·BP값 미승인 표시는 역사적 상태다. 현재는 이번 Playtest V1 범위로만 승인되었다.

## 최종 실행 및 보존 기록

실제 실행:

| 실행 | 결과 | 범위/기록 |
|---|---|---|
| Unity Config 작성·컴파일 | 종료0 | `Logs/CBT-R2-08-P2B3A-CreateConfig.log`. Unity AssetDatabase로 새 데이터와 meta 생성 |
| 최초 Playtest 집중 | **21 PASS / 0 FAIL** | `...-Focused.xml`, `.log` |
| 선택 회귀 | **741 PASS / 6 FAIL / 1 IGNORED**, 총748 | `...-Regression.xml`, `.log`, `...-TestFilter.txt`. 기존727개 + 최초 Playtest21개 |
| 최종 Playtest 집중 | **22 PASS / 0 FAIL** | `...-FocusedFinal.xml`, `.log`. 무효 지원/양보 경계1개 추가 후 최종 테스트 코드 재컴파일·전체 집중 검사 재실행 |

최종 보강 뒤 광범위 선택 회귀를 다시 실행하지 않았다. Runtime/Config 데이터는 바뀌지 않았고 새 테스트 한 개만 추가했으므로 최종22개 집중 검사를 실행했다. 서로 다른 실행을 합산해 단일 회귀 결과로 표시하지 않는다.

선택 실행의 P2B2D 초기화 **40/40 PASS**, Legacy 관련 **220/220 PASS**(EntryConsolidation36/FinalExchangeDriver15/FinalRulesPolicy52/Foundation2/ProductionOwnership13/SessionTurnFlow44/UIRouting58), Item Play Mode2/2 및 Film Contact/FieldInput/ColdSaveLoad1/1 PASS. 기존 Skill 분류·Source Key opt-in·단일 rule·양쪽 동시 배치 거부·stale 명령/중복 승인·Clash 필수 검사를 유지했다. 새22개에는 실제 unsaved EnterPlayMode 3인 fixture가 포함된다.

이전 P2B2D 선택720/6/1과 실패 이름을 비교한 결과 **동일6개, 신규 실패 없음(실행 범위)**. 실패 원문은 `Logs/CBT-R2-08-P2B3A-SixEvidence.txt`에 기록했다. 앞5개는 `Forbidden Demo/Debug/Test/Legacy component remained: Game.Combat.Debugging.CombatHandValidationOptIn on CombatRuntime`, 마지막은 Template Debug predicate `Expected: False / But was: True`. 6개 모두 **Asset Change Required**다.

Ignored는 기존 `Game.Tests.Integration.PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners`로 별도 쓰기/읽기 Editor 프로세스 재시작 검증이 필요하다. 이번에는 실행하지 않았다. 새 Skip 없음. 전체1,562/8/1은 여전히 과거 전체 기준선이다.

새 Editor 테스트 코드의 Unity 컴파일 오류 없음. 이번 로그에서 `warning CS`도 발견하지 못했다(기존 Runtime 전체 강제 재컴파일를 수행한 것으로 주장하지 않음). 별도 기존 import/환경 경고: `OfficeFlowController.cs.meta`의 잘못된 GUID 안내 및 scripts 없는 `Scenes/Tests/Tests.asmdef` 경고. 해당 기존 파일은 해시 동일하며 이번 범위에서 수정하지 않았다. 기존 Scene gate 예외는 컴파일 오류와 구분한다.

최종 보존: 시작 시 기존 **1,801개 전부 SHA-256 동일**, 수정·삭제·이동 없음. 기존 Scene/Prefab/SO/기존 meta/GUID, Packages/ProjectSettings와 앞 Batch의 dirty/untracked 코드/보고서 전부 보존했다. 새 GUID는 각 meta에서 유일하고 새 SO의 Production Scene/Prefab/Data 참조는 없다. Git 상태 차이는 위 신규6개 파일뿐이다. 신규 C#/보고서 공백 검사 통과. 전체 `git diff --check`의 기존 폰트3건·Scene1건 trailing whitespace는 보존했고 이번 변경으로 보고하지 않는다.

구현 상태: **Playtest V1 Config Prepared / Compile Verified / Automated Isolated Fixture Verified**. 실제 Production 자동 초기화는 비활성, 정식 Clash/콘텐츠·Inspector/수동 Play Mode/통합·Cutover는 **Pending**. commit/push/PR 없음.
