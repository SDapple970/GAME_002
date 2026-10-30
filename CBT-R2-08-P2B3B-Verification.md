# CBT-R2-08-P2B3B — Clash Playtest V1 통합 설정 및 검증

2026-10-30 KST. 기존 Production 초기화와 실행 경로를 **격리 fixture**에서 검증했다. 실제 Production 초기화는 비활성이다. Dungeon Scene/Prefab 전환·Debug 제거·정식 콘텐츠 작성·Cutover는 하지 않았다.

## 지침·Git·조사

- 적용 지침: 루트 `AGENTS.md`; 중첩 AGENTS/override 없음. Branch `main`, HEAD `6fa26d294472574097d33b884858ba0ee8bdc8b7` 유지. Unity 6000.3.23f1.
- 시작 시 tracked/untracked 상태 및 기존 파일 해시를 `Logs/CBT-R2-08-P2B3B-BeforeStatus.txt`, `...-BeforeHashes.json`에 기록했다. 기존 미커밋 코드를 기반으로 조사했으며 덮어쓰지 않았다.
- P2B2D/P2B3A 코드·보고서, Config/EntryPoint/Bootstrapper/Session/StateMachine/Orchestrator, ClashSeries/Hand/BP/Ultimate, Enemy controller, Director, Status/SkillRunner, Completion/Reward/참가자 EXP 및 Scene/Prefab/meta 참조를 확인했다.
- 이전 선택 기준선: P2B3A **741 PASS / 6 FAIL / 1 IGNORED**(748개; 당시 최초21개 Playtest 검사 포함), 최종 별도 집중22/22. P2B2D 집중40/40. 이전 전체 **1,562/8/1**은 과거 결과이며 이번 전체 실행으로 표시하지 않는다.

## 추가 데이터 및 파일

새 Config: `Assets/GAME/Tests/Editor/Fixtures/CombatPlaytestV1/FinalCombatRuntimeConfig_ClashPlaytestV1.asset`.
기존 B3A Config를 Unity에서 복제해 기존 타입 `FinalCombatRuntimeConfigSO`로 작성했다. B3A 원본은 수정하지 않았다. 새 Config도 enable=false, Hand rule/Ultimate/Enemy Source Key 참조는 빈 배열이며 Production에서 참조하지 않는다. 라벨은 TestOnly / PlaytestV1 / ClashFixture / ContentPending.

| 데이터 | 출처·값 | 의미 |
|---|---|---|
| Normal/Clash 슬롯 | B3A 승인 Playtest 2/3 | 최종 밸런스 아님 |
| BP | 초기0, 최대3, 유효 RoundCycle2회마다 +1, 투자0~3 | 기존 Runtime/Config 그대로 |
| Ultimate | 승인 BP 누적 소비량1/2/3 → Stage1/2/3 | 기존 Runtime 그대로, 제작 Skill/추가 MP 계약 미확정 |
| 강인도 | attacker3 / responder3 | 기존 P2B2D/B3A fixture 재사용 |
| 비교 상한 | 3 | 기존 fixture 재사용 |
| 결정 시간 | 5초 | 기존 fixture 재사용 |
| 최종 스킬 selector | LastCommittedWinningSkill | 기존 지원 선택을 fixture에서 명시 |
| Timeout 공격 selector | FirstAvailableSkill | 기존 지원 선택을 fixture에서 명시 |

새 자산만 저장하는 명시적 Editor 메서드 `CreateClashPlaytestAssetIfMissing`를 사용했다. 전체 dirty asset 저장은 하지 않는다. 이 메서드는 전투 시작 Owner나 Runtime 설치기가 아니다.

추가 파일:

1. `Assets/GAME/Tests/Editor/CombatClashPlaytestV1IntegrationTests.cs` 및 Unity 발급 meta.
2. 위 통합 Config 및 Unity 발급 meta.
3. 이 보고서와 무시되는 Logs 검증 기록.

기존 파일 수정·삭제·이동 없음. Runtime/API/SerializeField/enum/Quest/Reward/Save/Unity/Package 변경 없음. GUID: 새 SO `77fff0b326a327943b8f564b26f03718`, 새 테스트 script `a8cb1128e0b9ffa489f0514a5b923196`. 기존 Config script GUID `914e9a1f4d1248d982fb012634938a2a` 및 기존 모든 meta/GUID를 유지한다.

## 결정적 실행용 메모리 fixture

저장 통합 Config의 clone에만 enable=true와 임시 Skill/Status/Character ID/Enemy Source Key를 공급한다. 어떤 임시 콘텐츠도 Asset으로 저장하거나 Production에 연결하지 않았다.

- 초기 MP3 / Skill MP1: 세 번의 승인 비용을 정확히 관찰하기 위한 fixture. 저장 통합 Config의 기존 initialMp2는 바꾸지 않는다.
- 우세 비교: 공격 위력3 대 대응2 → 매 라운드 강인도1 소진 → 3라운드 승리. 위력은 Skill fixture 데이터이며 정식 수치 아님.
- BP 연동 비교: 기본 위력2 + 승인 BP1의 기존 ClashPower enhancement1 대 대응2. 매 라운드 투자1로 동일한 위력차1을 유지하면서 Stage1/2/3을 검사한다.
- BP3 예산이 필요한 경우 초기0을 확인한 다음 기존 ledger의 유효 양측 cycle 완료 API로 6 RoundCycle을 명시적으로 준비한다. 이것은 테스트 사전조건이며 실제 6회 전투를 플레이했다고 기록하지 않는다. 이후 3 ClashRound가 이 카운터를 늘리지 않음을 검사한다.
- 동점/동시 소진: 양측 위력2, 강인도3/3. Speed5/5는 공격권 소유, Speed5/6은 대응자 우선 분기를 결정적으로 검사한다. 6은 분기를 나누기 위한 fixture 값이다.
- 상한 검사: 기존 P2B2D의 강인도7/8·상한2 fixture. 양측 위력2로 소진 전에 상한 도달 → Tie 종료를 검사한다.
- 역전 비교: 위력1 대2로 공격자만 강인도0에 도달하는 분기를 검사한다.
- 카드 copies normal4/clash6, 최종 피해1, 부여 상태의 Stack 상한5 등은 테스트 관찰용 기존/결정적 데이터이며 제작 승인값이 아니다. 상태 보정은0이므로 위력/최종 피해 계산을 섞지 않는다.

정식 승인 Pending: 강인도·위력/상태/BP 계수, 반복 상한·timeout, 프로필 selector 선택, Skill kind/copies/seed/효과/MP, 공식 Character/Enemy/Film/Unique/Ultimate/AllOut 콘텐츠. 지원되는 enum이나 fixture 숫자를 작성했다고 정식 승인을 받은 것으로 판단하지 않는다.

## 실제 Owner 및 초기화·실행 순서

`CombatEntryPoint → 요청/참가자·Character ID 검증 → 장착 및 적 authored snapshot → CombatBootstrapper callback → Hand/BP/Ultimate/Clash 전부 사전 검증 → actor별 Hand → BP → Ultimate → Session Clash snapshot/성공 표식 → Opening Effect → Standoff → Orchestrator 첫 pump 전 Clash 설치 → 명령/Director/Enemy 연결 → 공격 선언·대응 승인 → Approach → ClashSeries → 비교별 카드·MP·BP 승인 → 각 라운드 연출 완료 → 최종 Outcome/SkillRunner → 상태·정신력·Posture·Terminal 평가 → Outcome 연출 경계 → Chain/인계 또는 기존 EntryPoint Completion`.

실제 Series는 첫 commit 후 생성된다. 강인도 감소는 비교 계산이며, HP/Status/Mental/Posture를 라운드마다 적용하지 않는다. 마지막 라운드 연출을 완료한 뒤 최종 효과를 한 번 실행하고, Outcome 연출 완료가 다음 Chain 경계를 연다. Terminal이면 기존 종료 경로를 사용한다.

Production Owner 유지: EntryPoint(시작/종료), Session(런타임 상태), StateMachine(phase/승인), Orchestrator(진행), SkillRunner/기존 Outcome 경로(효과), Director(연출), RewardService/Progression(보상/EXP). Debug 컴포넌트나 새로운 전투 엔진/Manager를 사용하지 않았다.

EditMode 검사는 OnCombatStarted에서 기존 Director 연출 구독을 해제하고 기존 presentation request의 완료 신호만 직접 제어한다. 계산/비용/상태 전환은 기존 Runtime을 통과한다. 별도 EnterPlayMode 검사는 **실제 CombatDirector와 Enemy Controller를 유지**한 채 세 라운드를 진행하고 Chain·Abort 및 timeScale/fixedDeltaTime 복구를 확인한다. 이 차이를 실제 Dungeon Inspector 검증으로 혼동하지 않는다.

## 필수 시나리오와 검증 근거

| 시나리오 | 이번 증거·경계 |
|---|---|
| 1 Debug 없이 초기화 | 새1/2/3인 fixture, 실제 EntryPoint, Debug component 없음 확인 |
| 2 준비 전 명령 차단 | 시작 전 명령 거부, 잘못된 Clash에서 명령0·Session없음·Exploration 복귀, 첫 요청 시 전체 준비 확인. P2B2D callback 실패/Opening Effect 검사도 재실행 |
| 3~4 단일/2~3인 | 새3개 case와 실제3인 Play Mode; 기존 B3A22/B2D40 독립 Hand/snapshot 회귀 |
| 5~6 Enemy opt-in | 일반 적 no-Hand와 Source Key 일치 적 Hand 경로 모두 3라운드; 기존 AI가 적 자신의 카드/스킬 선택 |
| 7~8 Standoff부터 3회 비교 | 선언·적 대응·Approach·Clash를 실제 Owner로 실행. 실제 Director Play Mode도 포함 |
| 9 비용 | 매 비교까지 MP 누적1/2/3, Normal1/Clash2 discard, 적 Clash3 discard, BP 투자1씩 총3 및 Stage1/2/3 |
| 10 최종 효과 | 각 비교 중 HP10/Mental100/상태없음/Posture0. 마지막 완료 후 HP9/Mental85/상태1회, 재전달·재실행 거부. 강인도0과 Posture 적용 구분 |
| 11 동점·무대응 | 양측 강인도0의 Speed/공격권 분기, 처음 무대응 및 반복 중 무대응 |
| 12 상한 | 7/8·최대2에서 소진 전 Tie, Standoff 복귀·피해/효과/BP주기 추가 없음 |
| 13 Chain·인계 | 실제 3회 비교 후 Chain, 자격 있는 동료로 인계, 동일 Hand/카드·refill·cycle 유지 |
| 14 Ultimate 단계 | 실제 라운드별 승인 BP로 Stage1/2/3. 전용 실행/reset은 기존 B3A22 및 기존 Ultimate 회귀 |
| 15 중복·stale | 같은 round request/card 재전송 거부, receipt 재전송 거부, 연출 완료/최종 Skill 재실행 거부 |
| 16 실패·Abort | Clash 미작성 거부 후 유효 retry; 비교 후 선택 중 Abort, pending/timer/binding 정리, 지연 command/연출 거부 |
| 17 다음 전투 | 새 CompletionId, BP0/Stage0/빈 discard/초기 MP 및 손패 |
| 18 Terminal·보상/EXP | 최종 피해로 적 HP0 → 기존 EntryPoint 결과1회. 실제 CompletionId의 RewardRequest를 기존 Service로 전달해 중복 차단 및 참가자별 EXP1회 확인 |

보상 검사는 격리된 완성 결과를 테스트 코드에서 기존 RewardService에 전달한다. 새 보상 정책/Production subscriber를 추가하지 않았다. 실제 Dungeon Reward UI/Save/Continue 연결·디스크 재시작을 검증한 것은 아니다. Film/Unique 소유와 Normal/Clash 사용 분리는 기존 P2B2D/B3A 회귀로 유지하며 새로운 영속 데이터 분류를 만들지 않았다.

## 실행 기록 및 기존 실패

최종 회귀 결과는 아래 최종 기록에 기재한다. 전체 회귀는 기존 보호 자산 설치·저장 검사가 포함되어 실행하지 않는다. 안전한 기존 선택 filter에 새 fixture를 추가했다.

기존 실패6개(공통 namespace `Game.Tests.Integration`):

1. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasCanonicalCombatRewardUiCameraAndBootstrapOwners`
2. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasExplicitInteractionOwnersWithoutLegacyInteractionPaths`
3. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasOneCanonicalRepeatableNpcInteraction`
4. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasQuestDerivedCompletionContractWithoutLegacyExitOrFallbackDestination`
5. `DungeonOneProductionSceneTests.ProductionDungeonOne_IsCleanEnvironmentMigration`
6. `ProductionSceneWiringTests.DungeonTemplate_IsCanonicalSingleOwnerProductionSource`

앞5개는 기존 Dungeon Hand Debug 선행 gate, 마지막은 Template→CombatRuntime prefab BP Debug gate다. **Existing Confirmed / Asset Change Required**로 유지한다. 테스트 삭제/Skip/기대값 완화나 Debug 참조 숨김으로 통과시키지 않았다.

## Production 차단 조건·후속 자산 전환·수동 절차

현재 통합 Config도 비활성이며 콘텐츠 참조가 없으므로 그대로 완성된 시작 데이터로 사용할 수 없다. 실제 연결 전에는 승인된 콘텐츠/ID/registry/snapshot/Skill 분류·copies/Enemy Source Key, 정식 또는 별도 승인 Playtest Clash 값과 selector, Ultimate/AllOut 효과·비용 계약이 필요하다.

보호 자산 전환은 별도 승인 후 기존 계획대로 수행한다. 대상은 기존 `Data/Combat/Runtime/FinalCombatRuntimeConfig_V1.asset`, `Prefabs/CombatRuntime.prefab`, `Scenes/Dungeon_1_Production.unity`, `Scenes/Dungeon_Template.unity`. Dungeon의 `Runtime/Combat/CombatRuntime` Hand/Clash/BP Debug 및 원본 prefab BP Debug를 원본/instance override와 구분해 검토한다. 이번에는 제거/수정/연결하지 않았다.

후속 수동 순서/통과 기준:

1. 새 테스트 통합 SO Inspector에서 enable=false, 3/2 Hand·BP3/2/3·Clash3/3/3/5 fixture 및 빈 콘텐츠를 확인한다. 기존 B3A/Production SO 참조와 값은 그대로여야 한다.
2. Test Runner에서 새15개와 B3A22/B2D40을 실행하고, 실제 unsaved Director Play Mode의 3회 모션·최종 피해1회·Chain·Abort 복구를 관찰한다.
3. 실제 자산 전환 승인 후에만 현재 작업본 사본/해시/override/UnityEvent를 기록하고 공식 데이터 연결 및 Debug 분리를 최소 변경한다. Missing Script/Reference, GUID/override·6개 gate를 점검한다.
4. 실제 Dungeon에서 Contact/FieldAttack, 1~3인 snapshot/인계, 일반/Hand 적, MP/BP/손패/Ultimate, 반복 합·동점·무대응·timeout·stale·Panic/Stun/KO, Terminal/Reward/EXP/입력/UI 복귀와 재전투를 검증한다. 별도 Save/Continue·Player Build·디스크 재시작 검증도 남아 있다.
5. 문제 시 HEAD reset이 아니라 작업 직전 사본에서 승인 변경만 복구한다. 05/06A 수동 검증 보류는 별도 해제 지시 없이 유지한다.

미검증: 실제 Production Inspector/씬 연출·입력/UI/카메라/필드 배치/정식 콘텐츠 통합, Player Build, 디스크 재시작 Save. 이 격리 결과로 전체 시스템 완료나 Cutover 완료를 선언하지 않는다.

## Development tracking

- System: Combat / Hand-BP-Ultimate-Clash Playtest V1 integration.
- Requirement: CBT-R2-08-P2B3B. Production Owner: 기존 EntryPoint/Session/StateMachine/Orchestrator/SkillRunner/Director.
- Implementation: 격리 통합 Config·실행 fixture 완료; Runtime 변경 없음.
- Compile/자동 검사: 아래 최종 결과.
- Inspector: 새 SO의 Unity 직렬화/자동 로드 확인, 실제 Production Inspector Pending.
- Play Mode: unsaved 실제 Director 자동 fixture 검증. 실제 Dungeon 수동 검증 Pending.
- Integration: 격리 Owner 간 기능 검증과 실제 Production 통합을 구분. 후자 Pending.
- Recommended: **Playtest V1 Isolated Runtime Integration Verified — Production Content / Asset Wiring / Manual Integration Pending**.
- 기존 보류/Legacy/Compatibility 유지. Cutover 미실행.

## 최종 실행 및 보존 기록

Unity 6000.3.23f1 최종 선택 회귀 **757 PASS / 6 FAIL / 1 IGNORED**, 총764. `Logs/CBT-R2-08-P2B3B-Regression.xml`, `.log`, `...-TestFilter.txt`. 종료2는 기존6개 Asset gate 실패를 반영한다. 이전 선택과 실패 이름을 직접 비교한 결과 동일6개, 신규 실패 없음(선택 범위).

- 새 Clash 통합 **15/15 PASS**, 실제 CombatDirector EnterPlayMode1개 포함.
- P2B3A **22/22 PASS**, P2B2D **40/40 PASS**.
- Legacy 관련 **220/220 PASS**: EntryConsolidation36 / FinalExchangeDriver15 / FinalRulesPolicy52 / Foundation2 / ProductionOwnership13 / SessionTurnFlow44 / UIRouting58.
- Item UI 격리 Play Mode2/2 및 Film Contact/FieldInput/ColdSaveLoad1/1 PASS. 기존 Mental/Status/Clash/BP/Hand/Ultimate/Enemy/Reward/EXP/Save 경계 선택 검사도 포함한다.
- 기존6개 실패 원문은 `Logs/CBT-R2-08-P2B3B-SixEvidence.txt`에 보존. 앞5개 Hand Debug 선행 gate, 마지막 Template Debug predicate `Expected: False / But was: True`. 상태 **Asset Change Required** 유지.
- Ignored: 기존 `Game.Tests.Integration.PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners`. 별도 Editor 쓰기/읽기 재시작 검증 미실행. 새 Skip 없음.

중간 실행도 구분한다. 처음 테스트 작성 시 잘못 참조한 `Posture` 속성 CS1061 2개를 기존 `CurrentPosture`로 고친 뒤 새 데이터 생성/컴파일이 종료0으로 완료됐다(`...-CreateConfig2.log`). 최초 집중은 **14 PASS / 1 FAIL**이었다. 실패는 실제 Director의 0.15초 연출을 batch의 600프레임 제한보다 오래 기다려야 하는 fixture 환경 문제였다. 실제 출력에는 첫 라운드 DirectorRoutineStarted/AwaitPresentation까지만 있었다. 프레임 수 대신 10초의 실제 시간 상한으로 기다리도록 수정한 뒤 **15/15 PASS**(`...-Focused2.xml/.log`) 및 최종 선택 실행에서도15/15를 확인했다. Runtime/연출 기본 시간이나 assertion을 완화하지 않았다.

최종 변경 코드 컴파일 오류 없음. 이번 최종/수정 후 로그에서 `warning CS` 없음. 기존 Runtime 전체 강제 재컴파일을 수행한 것으로 주장하지 않는다. 기존 import/환경 안내(OfficeFlowController meta GUID 경고, scripts 없는 Tests.asmdef)는 기존 파일 무변경으로 유지하며 Scene gate 예외와 컴파일 오류를 구분한다.

보존 결과: 시작 시 기존 **1,807개 모두 SHA-256 동일**, 기존 파일 수정/삭제/이동 없음. 보호 Production Scene/Prefab/SO, B3A Config·테스트, 기존 meta/GUID, Packages/ProjectSettings 및 앞 Batch dirty/untracked 코드를 모두 보존했다. 시작 해시 목록에 없던 신규 파일은 새 테스트/meta, 새 Config/meta, 이 보고서 **5개 파일**뿐이다. 새 GUID는 유일하며 새 SO의 Production Scene/Prefab/Data 참조가 없다. 신규 C#/보고서 공백 검사 통과. 저장소 전체의 기존 폰트/Scene trailing whitespace는 수정하지 않았다.

권장 상태: **Playtest V1 Isolated Runtime Integration Verified**. 실제 Production 자동 초기화는 비활성, 정식 Clash·콘텐츠/Inspector/수동 Play Mode·통합/자산 전환/Cutover는 Pending이다. 05/06A 보류와 Legacy/Compatibility 유지, commit/push/PR 없음.
