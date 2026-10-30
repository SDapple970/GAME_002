# CBT-R2-06A — 기절 복귀·총공격 구현 및 검증

작성일: 2026-10-28 (KST). 범위는 CBT-R2-06A이며 CBT-R2-05의 Inspector 연결·Production 수동 Play Mode 보류를 해제하지 않는다.

## 1. 구현 결과

코드 구현: **Implementation Complete**. 실제 Dungeon_1_Production의 Inspector·UI·Input·연출 통합은 **Unverified**다. 전체 전투 시스템 완료를 의미하지 않는다.

- 기존 Stun, 체간(Posture), 반복 합의 Durability를 별개로 유지했다. 반복 합 강인도 계산을 바꾸지 않았다.
- Terminal을 후보보다 먼저 평가한다. 적 0명은 Victory, 아군 전멸 및 동시 전멸은 기존 FinalCombatTerminalPolicy의 Defeat를 따른다.
- 살아 있는 적 전원 기절만 총공격 후보가 된다. 일부 기절은 후보가 아니다. 후보 생성 전 기절을 해제하지 않는다.
- 후보 중에는 수락/거절을 제공한다. 일반 연계·인계·궁극기로 선택을 우회하지 못한다. 기존 Chain 결정 요청과 exchange version을 재사용했다.
- 수락 직전에 현재 roster·HP·기절·행동 가능한 아군·제작된 AllEnemies 공격 스킬을 다시 확인한다. 무효 요청은 피해 없이 false를 반환한다. 다음 흐름 갱신은 무효 후보를 Standoff 또는 Terminal로 정리한다.
- 수락은 기존 SkillRunner를 한 번 호출한다. 피해 → 제작된 상태효과 → 정신력 결과 → Terminal 평가 순서다. 살아남으면 정상 Standoff 복귀 시 살아 있는 기절 대상만 해제하고 체간을 0으로 초기화한다. 사망 대상을 되살리거나 Terminal에서 복귀시키지 않는다.
- 거절/Timeout도 같은 Standoff 복귀 경계를 사용한다. 기절 해제에는 기존 StaggerSystem 정책을 사용한다. 일반 LegacyPlanning의 턴 종료 코드는 유지했다.
- 전원 기절로 시작한 경우에도 먼저 총공격 선택을 제공한다. 후보가 아닌데 현재 행동 진영 전원이 기절한 Standoff는 복귀 경계에서 회복시켜 대기를 해소한다.
- 동기 피해/복귀 콜백, 중복 클릭, 중복 pump, 낡은 요청·버전, 다른 세션 요청을 방어한다. 부분 피해 후 예외는 Abort로 종료하고 재실행하지 않는다.
- 승인된 실행 중 세션이 바뀌면 이전 실행이 새 세션의 요청/타이머/상태를 지우지 않는다.
- 선택 타이머는 Orchestrator가 소유한다. 기본 10초의 비배율 시간이며 Inspector에서 조정 가능하다. Pause/timeScale 0에서는 정지하고 명령을 거부한다. Pause 동안 표시/로컬 선택을 비우고 재개 시 선택을 다시 표시한다. Abort·세션 해제·Orchestrator 비활성화 시 요청/타이머와 Controller 구독을 정리한다.
- Director의 기존 ChainDecisionAccepted/TerminalCompleted/FinalExchangeUnbound 연결로 슬로모션을 복구한다. 새 timeScale/fixedDeltaTime 소유자를 추가하지 않았다.
- UI는 수락/거절, 남은 시간과 선택 불가 사유를 표시한다. 필수 root/text/button template 미연결은 명시적 Error다. 폐기할 동적 버튼은 즉시 비활성화한다.

총공격 피해 배율이나 새 피해 엔진은 추가하지 않았다. 기존 CombatAllOutExecutionPolicy의 **처음 실행 가능한 아군이 보유한 제작된 AllEnemies 공격 스킬을 사용하는 V1 계약**을 유지했다. fixture 피해 4/100 등은 테스트 수치이며 Production 밸런스가 아니다. 해당 스킬이 없으면 수락은 비활성화되고 사유·거절·Timeout이 유지된다.

## 2. 적용 지침과 Git 상태

- 적용 파일: 루트 `AGENTS.md`. 중첩 `AGENTS.md`/`AGENTS.override.md` 없음.
- 현재 작업문 CBT-R2-06A를 구현 요구사항으로 사용했다. 외부 기획/개발현황 문서를 구현 근거로 사용하지 않았다.
- Branch: `main`.
- HEAD: `6fa26d294472574097d33b884858ba0ee8bdc8b7`.
- 편집 직전 보존 기준: tracked/untracked 기존 변경 33개 파일의 SHA-256을 기록했다. 4개 기존 수정 파일에 이번 변경을 추가했고 나머지 29개는 동일 해시로 보존했다.
- 작업 시작 최초 status 이후 편집 직전 기준에 포함된 `ProjectSettings/TimeManager.asset`도 보존했다. Scene/Prefab/폰트 및 기존 미추적 CombatUltimateRuntime `.cs`/`.meta`는 보존했다.
- 작업문 버전 `6000.2.6f2`와 달리 실제 ProjectVersion 및 AGENTS는 `6000.3.23f1`이다. 실제 Unity 6000.3.23f1로 검증했으며 버전·패키지·Input 설정은 변경하지 않았다.
- commit/branch/push/PR, 파일 삭제·이동 없음.

## 3. 수정·추가 파일

수정 7개:

| 파일 | 이번 변경 |
| --- | --- |
| `Assets/GAME/Scripts/Combat/Runtime/Core/CombatAllOutPolicy.cs` | Terminal 우선 후보 판정 |
| `Assets/GAME/Scripts/Combat/Runtime/Core/CombatAllOutExecutionPolicy.cs` | 실제 Attack 스킬만 실행 후보로 선택 |
| `Assets/GAME/Scripts/Combat/Runtime/Core/CombatStateMachine.cs` | 기절 복귀·실행 예약·현재 후보/Terminal 재검증 |
| `Assets/GAME/Scripts/Combat/Runtime/Core/CombatFlowOrchestrator.cs` | 요청 승인·타이머·정리·세션 교체 보호·기존 공격 주기 정산 연결 |
| `Assets/GAME/Scripts/Combat/Runtime/UI/FinalCombatPlayerCommandController.cs` | 요청 객체 기반 수락/거절·Pause 표시·구독 해제 |
| `Assets/GAME/Scripts/Combat/Runtime/UI/FinalCombatUIBinder.cs` | 수락/거절·사유/타이머·연결 오류·동적 버튼 정리 |
| `Assets/GAME/Tests/Editor/CombatStatusPlayModeIntegrationTests.cs` | 기존 지속시간 검증의 기절 주입 시점을 outcome gate로 조정 |

마지막 테스트는 전원 기절 초기 상태가 이제 즉시 후보를 여는 요구사항에 맞춰 **설정 순서만** 바꿨다. 일반 교환 후 상태 지속시간 1, 총공격 후에도 1이라는 기존 검증을 제거하거나 약화하지 않았다.

추가 5개:

- `Assets/GAME/Tests/Editor/CombatAllOutRecoveryTests.cs`
- `Assets/GAME/Tests/Editor/CombatAllOutRecoveryTests.cs.meta`
- `Assets/GAME/Tests/Editor/CombatAllOutRecoveryPlayModeTests.cs`
- `Assets/GAME/Tests/Editor/CombatAllOutRecoveryPlayModeTests.cs.meta`
- 이 보고서 `CBT-R2-06A-Verification.md`

테스트 `.meta`는 Unity가 생성했으며 각각 GUID의 중복이 없음을 검색했다. 기존 `.meta` 변경 없음. 진단용 XML/log는 `Logs/CBT-R2-06A-*`에 기록했다.

## 4. Production Owner와 실행 흐름

단일 시작/종료 소유자는 **CombatEntryPoint**다. 새 Manager나 별도 전투 시작 경로를 만들지 않았다.

1. 기존 합/피해/체간/기절/정신력 처리 후 현재 Terminal·AllOutCandidate를 확인한다.
2. CombatStateMachine이 ChainDecision을 유지하고, Orchestrator가 아군 선택 요청 하나와 타이머를 발행한다.
3. Player Controller는 현재 요청 객체를 전달한다. Binder는 규칙을 계산하지 않는다.
4. 수락 승인 후 StateMachine이 실행을 잠그고 기존 SkillRunner를 호출한다. Director는 확정된 결과를 표시한다.
5. 사망 결과면 기존 Terminal → EntryPoint → CombatResult/Completion. 살아 있으면 기절 복귀·체간 초기화 → Standoff.
6. 거절/Timeout은 피해 없이 같은 복귀 경계. Pause는 선택을 일시 중지한다. Abort/해제는 요청을 폐기한다.
7. Reward 및 참가자 EXP는 기존 CompletionId/RewardService 중복 방지 계약을 사용한다. Quest/Reward/Save 정책을 수정하지 않았다.

LegacyPlanning, CombatDemoFlowController, Debug 검증 opt-in, 기존 version-only API를 삭제하거나 Production 소유자로 승격하지 않았다. 기존 CombatDirector/SkillRunner/CombatEntryPoint/CombatSession에는 이번 작업의 편집을 추가하지 않았다.

## 5. 공개 API·직렬화 영향

기존 API 제거·이름/타입 변경 없음. 추가 API:

- Orchestrator: `ExecuteAllOut(CombatExchangeDecisionRequest)`, `DeclineAllOut(CombatExchangeDecisionRequest)`, `IsAllOutSelection`, `AllOutUnavailableReason`, `AllOutDecisionTimeRemaining`.
- Player Controller: `ConfirmAllOut(CombatExchangeDecisionRequest)`, `DeclineAllOut(CombatExchangeDecisionRequest)`, `IsAllOutCandidate`, `AllOutUnavailableReason`.
- StateMachine 내부 지원 메서드 `TryRefreshDecisionBoundary(int)`.

Production UI는 요청 객체를 캡처하므로 이전 세션/이전 UI 버튼이 새 요청을 승인할 수 없다. 기존 `ExecuteAllOut(int)`와 인자 없는 Controller 메서드는 호환 목적으로 유지했다. 외부 신규 호출자는 요청 객체 overload를 사용해야 세션 간 동일 숫자 버전도 구별할 수 있다.

새 SerializeField는 Orchestrator의 `allOutDecisionDurationSeconds` 하나다. 기본 10초다. 기존 필드·타입·GUID 보존, 새 Component/Scene/Prefab/SO 연결 변경 없음. 이 필드를 자산에 저장하지 않았다.

## 6. 실제 검증 결과

최종 집중 실행: **341/341 통과**, 실패·skipped 0, Unity 프로세스 종료 0.

- 근거: `Logs/CBT-R2-06A-FinalFocused.xml`, `.log`.
- 신규 규칙·경계: **29/29 통과**.
- 신규 실제 Editor EnterPlayMode fixture: **4/4 통과**.
- 기존 상태이상 Play Mode fixture: **1/1 통과**.
- 기존 Player Command 21, 연출 34, 반복 합 Core 11/Integration 24, BP 9, 손패 9/opt-in 6, 전투 규칙/인계 52, 적 명령 8/정책 9, Completion/Reward 96, 참가자 EXP 28 모두 통과했다.
- 신규 Play Mode는 빈 임시 Scene과 메모리 제작 데이터로 실제 EntryPoint·Director·Binder 버튼·SkillRunner·Completion·RewardService를 사용했다. 별도 PlayMode assembly 실행이나 Production 화면의 시각 검증을 뜻하지 않는다.
- 물리 키 입력 대신 기존 Player Command API 및 Binder의 실제 Button 이벤트를 사용했다.

최종 전체 실행: **1,538 통과 / 8 실패 / 1 ignored**, 총 **1,547건**, 프로세스 종료 2. 근거는 `Logs/CBT-R2-06A-AllEditMode-Final.xml`, `.log`다. 신규 33건과 조정한 기존 상태이상 fixture는 모두 통과했다. 따라서 전체 green으로 보고하지 않는다. 집중 실행과 전체 실행에 같은 테스트가 포함되므로 독립 커버리지로 합산하지 않는다.

필수 시나리오 대응:

| 요구 | 근거 |
| --- | --- |
| 적 0/1/복수 일부/복수 전원 | Candidate 파라미터 테스트 |
| 기절+사망, 아군 전멸 | DeathAndStun, DeadStunnedActor, ChangedCandidate |
| 후보 이후 wake/death/remove/ally-death | ChangedCandidate 4종, RemovedOneOfManyTargets |
| 수락/거절/Timeout | Accept, InitialAllStunned, Timeout 및 실제 Play Mode |
| 중복·stale·동기 재진입 | DamageCallbackAndRecoveryCallback, StaleRequestAcrossSessions, RepeatedPump |
| 생존 후 정상 공격 | Accept nonlethal, 실제 AcceptSurvive 후 일반 공격 승리 |
| Pause/Abort/해제/비활성화·시간 복구 | Cancellation 3종 및 실제 PauseThenAbort/DeclineThenTimeout |
| 피해·효과 중복 | 다중 적 HP·상태 1스택, 중복 버튼/연출 콜백의 재요청 거부 |
| 부분 실행 예외 | ThrowingDamageCallback: 부분 피해 후 Abort, 재시도 차단 |
| 세션 전환 도중 실행 | SessionSwitchDuringEffect: 새 요청/타이머/HP 보존 |
| Reward/EXP 중복 | 실제 Completion 1회, 동일 결과 RewardService 재요청 차단·지급액 검증, 기존 참가자 EXP 28건 |
| 반복 합·BP·손패·인계·적 AI | 위 집중 회귀 목록 |

초기 검증 이력도 숨기지 않는다.

- Initial: C# 컴파일 성공이나 잘못된 namespace filter로 **0건 실행**. 통과 근거로 사용하지 않는다.
- Focused-1: 신규 테스트의 SoSkill using 누락으로 컴파일 실패. 테스트 import를 보완했다.
- Focused-2: 79/84 통과. 신규 fixture 버튼 template/비활성화 lifecycle 구성 누락 5건.
- Focused-3: 28/29 통과. 실제 신규 Play Mode 4건은 통과, EditMode 동적 버튼 중복 집계 1건.
- Focused-4: 86/87 통과. EditMode에서 생성된 버튼 정리의 Destroy 사용 1건. 실제 Play/Editor에 맞게 정리를 보완했다.
- 전체 1차: 1,536 통과 / 9 실패 / 1 ignored, 총 1,546. 신규 32건 모두 통과. 상태이상 fixture 순서 충돌 1건을 위와 같이 조정했고 다른 8건은 보호 자산/기존 UI 기대값과 관련되어 아래에 분리 기록한다.

## 7. 전체 회귀의 별도 실패와 보존 범위

이번 범위에서 수정하지 않는 기존 상태/기대값 충돌:

- DungeonOneProductionSceneTests 5건: 보호 Scene의 CombatRuntime에 CombatHandValidationOptIn이 남아 있어 Production validator가 거부한다.
- FilmUniqueProductionPlayModeTests 1건: 보호 Scene의 Hand validation fixture와 임시 스킬 구성이 맞지 않아 Error가 발생한다.
- ProductionSceneWiringTests.DungeonTemplate_IsCanonicalSingleOwnerProductionSource 1건: 현재 template에 Debugging/Legacy 계열이 존재한다.
- CombatItemUsePlayModeIntegrationTests 1건: fixture는 아이템을 skillListRoot에서 찾지만 작업 전/현재 Binder 모두 handoffListRoot에 구성한다.

보호 Scene/Prefab 및 이 기존 테스트들은 바꾸지 않았다. 보존한 작업 전 Binder 사본에도 `BuildCombatItemOptions(handoffListRoot, ...)`가 있으며, 보호 자산 해시와 현재 Debug 컴포넌트 참조를 읽기 전용으로 확인했다. **작업 전 코드로 테스트를 실행해 재현했다고 주장하지 않는다.**

작업본을 임시 원본 사본으로 덮어쓰는 baseline 비교 실행은 자동 승인 검토가 변경 유실 위험 때문에 거부했다. 실행되지 않았고, 작업본을 변경하지 않는 사본 비교로 대체했다. 해당 실행 결과나 가상의 baseline pass를 완료 근거로 사용하지 않는다.

전체 `git diff --check`는 기존 폰트 asset/Production Scene의 trailing whitespace 4줄을 보고한다. 해당 파일은 보존 대상이므로 정리하지 않았다. 이번 수정 7개 파일의 범위 지정 diff check와 작업 전 사본 대비 patch 점검은 통과했다.

컴파일 오류와 경고는 구분한다. 실제 집중 검증에서 C# 오류 0. 기존 FieldEnemy 미사용 이벤트(CS0067) 및 StoryInteractionController 2개/RescueNpcActor 1개 미사용 필드(CS0414) 경고는 별도다. 부분 피해 예외 테스트의 주입 로그는 예상 로그이며 passing 결과로 검증했다. 빈 Scene의 로컬 field lock/UI root 경고는 Production 연결 검증 근거가 아니다.

## 8. Inspector 확인 사항과 수동 절차

이번에 새로 재연결한 항목은 없다. 향후 승인된 Production 연결 검증에서 확인할 사항:

- 기존 CombatEntryPoint ↔ Orchestrator/Director 연결, 기존 UI Router의 combat root 및 Binder 자동 binding.
- FinalCombatUIBinder의 `handoffListRoot`, `contextText`, `optionButtonPrefab`. 기존 ProductionDungeonUI prefab에는 참조가 직렬화되어 있지만 실제 Inspector/Play 동작은 검증하지 않았다.
- 실행 가능한 아군의 제작된 AllEnemies Attack 스킬. 이번에 Production 스킬 SO/배율을 새로 배정하지 않았다.
- 선택 시간 변경이 필요하면 Orchestrator의 `allOutDecisionDurationSeconds`를 지정한다.

독립적으로 지금 실행 가능한 절차:

1. Unity 6000.3.23f1 Test Runner의 EditMode에서 CombatAllOutRecoveryTests와 CombatAllOutRecoveryPlayModeTests를 실행한다. 29 + 4건 모두 통과해야 한다.
2. Play Mode 4건이 저장되지 않는 빈 Scene에서 수행되며 오류 로그 없이 끝나는지 확인한다. 보호 Scene에 fixture를 저장하지 않는다.

CBT-R2-05 연결 보류 해제 후 Production 수동 통과 기준:

1. 일부 적만 기절하면 총공격 선택이 없어야 한다. 마지막 유효 적이 기절하면 복귀 전에 수락/거절·시간이 표시되어야 한다.
2. 수락을 연속 클릭해도 피해/상태효과는 한 번이어야 한다. 적이 생존하면 기절이 해제되고 체간이 0이며 다음 정상 행동이 가능해야 한다.
3. 거절 또는 시간 만료 시 총공격 피해 없이 복귀해야 한다. 기절/체간 정리는 한 번만 이루어져야 한다.
4. 선택 중 Pause는 시간과 명령을 멈춰야 한다. 재개 후 남은 시간에서 계속해야 한다. Abort/Scene 전환 후 이전 버튼/요청은 무효이고 다음 전투에 선택이 남지 않아야 한다.
5. Director 비활성화·취소·선택 종료 후 기존 timeScale로 돌아와야 하며 fixedDeltaTime은 바뀌지 않아야 한다. 다른 시간 소유자의 값은 덮어쓰지 않아야 한다.
6. 적/아군 전멸은 선택보다 Terminal이 우선이어야 한다. 전투 종료 결과와 Reward/참가자 EXP는 CompletionId당 한 번이어야 한다.
7. 필수 UI 참조 누락은 명시적 Error, 실행 가능한 AllEnemies 스킬 부재는 비활성화 사유가 보여야 한다.
8. 기존 반복 합·BP·손패·인계·적 AI/대응 동작을 실제 연결/입력으로 확인한다.

미실행: 실제 Dungeon_1_Production 수동 Play Mode/Inspector, 실콘텐츠 Animator/카메라·가독성·레이아웃, 실제 InputAction/키 입력, Player Build, 별도 PlayMode assembly, 별도 프로세스 재시작 Save 검증. CBT-R2-05 보류 범위 및 이번 격리 검증 범위를 넘기 때문이다.

## 9. Development tracking

- System: Combat / FinalExchange.
- Feature: CBT-R2-06A 기절 복귀·AllOutCandidate 선택·승인·단회 실행·Terminal/Completion 연계.
- Requirement: 현재 사용자 작업문 CBT-R2-06A. 별도 기획 문서/탭을 요구사항으로 사용하지 않음.
- Production owner: CombatEntryPoint → CombatStateMachine/CombatFlowOrchestrator, 기존 SkillRunner/Director 및 Player Controller/Binder.
- Implementation: **Implementation Complete**.
- Compile: **Compile Verified**, Unity 6000.3.23f1.
- Inspector/serialized assets: 기존 필드/GUID 및 보호 파일 보존 확인. 실제 Production Inspector는 **Unverified**.
- Play Mode: 신규 격리 Editor EnterPlayMode **4/4 Verified**. 실제 Dungeon은 **Unverified**.
- Integration: 격리 EntryPoint·UI 버튼·SkillRunner·Completion·Reward/EXP 경로 Verified. Production 연결은 **Unverified**.
- Remaining: CBT-R2-05 Inspector/수동 검증, Protection Scene Debug opt-in 충돌, 기존 Item fixture UI 경로 기대값, 실콘텐츠/물리 입력/Player Build.
- Recommended development-tracking status: **Implementation Complete — Isolated Integration Verified / Production Integration Verification Pending**.
- Stale documentation/mapping: 작업문 Unity 버전과 실제 프로젝트 차이, 기존 Item fixture의 root 기대값 차이, 기존 상태이상 fixture의 초기 전원 기절 순서 차이.
- 개발현황 시트는 변경하지 않았다. System Complete를 권장하지 않는다.

## 10. 최종 점검 기록

최종 전체 1,547건: 1,538 pass / 8 fail / 1 ignored. 실패 8건은 7절에 개별 원인을 기록했고 보호 자산/기존 Item UI 기대값을 고치지 않았다. ignored는 `PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners`로, write 단계 뒤 별도 Unity 프로세스의 `-party05vRestartStage read`가 필요하다. ignored를 pass로 합산하지 않았다.

종료 시 branch/HEAD는 시작과 동일하다. 보존 기준 33개 중 의도적으로 확장한 기존 전투 코드 4개 외 **29개 SHA-256 동일**이다. 이번 추가 diff 7개 수정 파일의 `git diff --check`는 통과했으며 새 테스트/보고서에도 trailing whitespace가 없다. 기존 보호 asset/scene의 whitespace 4줄은 남겼다. 기존 `.meta`, Scene/Prefab/SO/폰트, Quest/Reward/Save 및 미추적 Ultimate 파일을 보존했고 삭제/이동/commit/push/PR은 없다.

최종 권장 상태: **Implementation Complete — Compile Verified / Isolated Play Mode & Integration Verified / Production Integration Verification Pending**. 전체 테스트 8건의 별도 실패 및 CBT-R2-05 보류 사항을 해결한 것으로 보고하지 않는다.
