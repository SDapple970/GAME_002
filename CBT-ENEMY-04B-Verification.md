# CBT-ENEMY-04B — FinalExchange 선언 변경형 Feint V1

검증일: 2026-10-17. 구현 완료, Unity 컴파일·집중 EditMode·격리 Editor EnterPlayMode·전체 EditMode 회귀 확인. **Production Dungeon UI/Inspector 및 Player Build는 미검증**이며 System Complete로 기록하지 않는다.

## 1. 구현 결과

명시적으로 허용된 적이 초기 공격을 선언한 뒤, Response/NoResponse가 아직 승인되지 않은 동안 같은 공격자·같은 대상에 대해 다른 보유 Attack 스킬로 한 번 교체할 수 있다. 교체 자체는 비용을 소비하지 않는다. 최종 선언만 기존 Commit → Approach → Clash/Unopposed → SkillRunner → 결과 경로로 실행한다.

발동 확률, 자동 타이밍, 자동 선택, 철회, 공격권 선점, Defense/Dodge/반격, 보스 Phase는 추가하지 않았다. ENEMY-03의 초기 선언 범위를 적용하여 Chain 중 교체는 거부한다. 실패한 시도는 원래 선언·MP·요청·성공 횟수를 보존한다. 요청서가 성공한 교체만 1회로 제한하므로, ENEMY-03의 과거 실패 시 권리 종료 제안은 적용하지 않았다.

## 2. 지침·Git·환경·보존

- 읽은 지침: 루트 `AGENTS.md`. 적용되는 중첩 AGENTS/override 없음.
- 읽은 설계: `CBT-ENEMY-03-Design.md` 원문. ENEMY-02/02V 및 04A 구현·테스트·결과도 조사했다.
- Branch: `main`. HEAD: `1ebe3dd9be008ad063654aa72308f832192119b4` (`12`).
- Unity: `6000.3.23f1 (09d2ecc7fb28)`.
- 작업 시작 시 미커밋 파일 33개. SHA-256과 내용을 메모리에 기록했다. 그중 29개는 종료 시에도 해시가 동일하다. 겹치는 4개(Profile/Snapshot/Enemy Controller/Orchestrator)는 이전 변경 위에 이번 기능만 추가했다.
- 기존 PARTY/STATUS/ITEM/ENEMY-02/04A 변경과 문서, 기존 meta를 보존했다. commit/push/branch 생성 없음.
- 보호 Scene/Prefab, 기존 SO asset, Input Asset, 기존 meta/GUID, Party/Reward/Save 정책은 수정하지 않았다.

## 3. 실제 수정·추가 파일

경로는 `Assets/GAME/Scripts/Combat/` 기준이다. 기존 Git diff에 보이는 PARTY/ENEMY-02/04A 파일 전체가 이번 수정 목록은 아니다.

| 수정 파일 | 이번 변경 |
| --- | --- |
| `Data/FinalCombatEnemyBehaviorProfileSO.cs` | 별도 기본 false `declarationFeintEnabled` |
| `Runtime/Model/EnemyCombatBehaviorSnapshot.cs` | 권한 값 복사, 교체 권한만 있는 Profile도 Snapshot 유지 |
| `Runtime/Model/CombatExchangeState.cs` | 선언 ID/revision/교체 성공 여부, 기존 저장 처리 재사용 |
| `Runtime/Core/CombatStateMachine.cs` | 교체 최종 승인·유효성 검사·원자 설치 |
| `Runtime/Core/CombatFlowOrchestrator.cs` | Token 공개·교체 요청·Response 재발행·재진입 방어·scoped Response overload |
| `Runtime/Core/FinalCombatEnemyCommandController.cs` | 현재 Session에 귀속된 명시적 교체 제출 API |
| `Runtime/Model/CombatExchangeDecisionRequest.cs` | 현재 선언·Token의 읽기 전용 snapshot |
| `Runtime/UI/FinalCombatPlayerCommandController.cs` | 현재 요청 객체로 Response 제출, 새 요청 시 기존 선택 초기화 |
| `Runtime/UI/PlayerCombatDecisionViewState.cs` | 표시할 선언·Token 전달 |
| `Runtime/UI/FinalCombatUIBinder.cs` | 기존 contextText에 변경된 스킬·대상과 재선택 안내 |

추가:

- `Assets/GAME/Scripts/Combat/Runtime/Model/CombatDeclarationToken.cs` 및 Unity 생성 meta.
- `Assets/GAME/Tests/Editor/FinalCombatDeclarationFeintTests.cs` 및 Unity 생성 meta.
- 이 보고서. 결과 XML/로그는 `Logs/CBT-ENEMY-04B-*`에 생성했다.

삭제·이동 파일 없음. 새 meta GUID는 Unity import가 생성했으며 기존 GUID를 변경하지 않았다.

## 4. Production Owner와 승인 흐름

```text
EnemyDefinitionSO → 기존 Profile → EnemyCombatProvenanceBridge
→ CombatStartRequest → FieldCombatantFactory → CombatSession Snapshot
→ 기존 Enemy Command Controller의 초기 선언
→ Orchestrator.ResponseDecisionRequested (선언 Token 포함)
→ 명시적 Controller.TryReplaceDeclaration(token, replacementSkill)
→ Orchestrator.TryReplaceEnemyDeclaration
→ StateMachine.TryReplaceEnemyDeclaration [모든 검사]
→ ExchangeState.ReplaceDeclaration [한 번 설치]
→ 기존 ResponseDecisionRequested 재발행
→ Player Controller 새 Response → 기존 Commit/실행/종료
```

`ConditionalFinalCombatEnemyDecisionPolicy`와 FirstValid의 행동 선택은 수정하지 않았다. 자동 Feint 판단/스케줄도 없다. 기존 `CombatEntryPoint`가 만든 Enemy Controller를 그대로 사용한다. StateMachine이 최종 승인하고 ExchangeState가 전투 전용 상태를 저장한다. SkillRunner·Director·UI Router·GameFlow·Reward·Save에는 새 실행 Owner를 만들지 않았다.

Legacy `TrySubmitEnemyDecision(IEnemyCombatDecisionPolicy)`와 `DeterministicCycleEnemyCombatPolicy`, Demo/Debug 경로는 확장하거나 삭제하지 않았다.

## 5. 선언·Response·Version 계약

- Token 값: `SessionId`(기존 CompletionId), `DeclarationId`, `Revision`, `ExchangeVersion`. GameObject/SO 참조 없음.
- 새 정상 선언은 ID 증가, revision 0, 교체 미사용 상태로 시작한다. 교체는 ID를 유지하고 revision과 Version을 증가시킨다. 일반 상태 변화에도 증가하는 Version을 사용 횟수로 해석하지 않는다.
- 승인 조건: 현재 Session/ID/revision/version 일치, FinalExchange AttackDeclaration, 비 Chain, Response Pending, 미 commit, 미 교체, 살아 있고 행동 가능한 opt-in 적, 원래 선언도 유효함.
- 새 스킬은 다른 ID의 `SkillTag.Attack`이며 실제 보유 인스턴스여야 한다. 동일 actor/target에 대한 기존 선언 정책과 MP 비용 검증을 통과해야 한다.
- **Response 우선:** Core가 Response 또는 NoResponse를 승인한 즉시 Pending이 끝나므로, Commit이 아직 실행되지 않은 순간에도 교체를 거부한다. 승인된 Response를 철회하지 않는다.
- **교체 우선:** 과거 선언을 새 불변 선언으로 교체하고, 과거 Response 요청을 무효화한다. 새 Version의 Response를 발행하며 기존 선택을 초기화한다. 새 Response/NoResponse만 승인된다.
- 동일 프레임은 메인 스레드의 실제 승인 순서가 기준이다. 별도 프레임 우선순위나 타이머를 만들지 않았다.
- 교체 설치 중 Orchestrator의 Response/NoResponse와 pump 재진입을 막는다. 이벤트 첫 구독자가 요청을 변경하면 후속 구독자에게 과거 요청을 계속 보내지 않는다.
- 같은 Exchange에서 새 Token으로 재시도해도 두 번째 성공은 거부한다. 스킬·대상·자원 검증 실패는 원래 선언을 유지하며 성공 허용 횟수를 소비하지 않는다.

## 6. MP·피해·Status·Runtime 분리

교체 API에는 MP 차감·환불·SkillRunner 호출이 없다. 실제 지불 Owner는 기존 `TryCommitExchange(version)` → `CombatExchangeCommitPolicy`다. 최종 스킬 비용과 유효 대응 비용만 한 번 정산한다. 교체는 기존 선언 저장 처리를 재사용해 Response/Clash/준비 결과를 비우며, 원래 공격이 Chain이나 실행 결과에 남지 않는다.

제작 값은 Profile, 복사 값은 Snapshot, 선언 ID/revision/사용 여부는 Session이 소유한 ExchangeState에만 둔다. 동시에 하나의 선언만 존재하므로 적별 별도 전역 dictionary나 Pending 공격 queue가 필요하지 않다. 다음 선언/다음 적/다음 Session에서 사용 상태가 재시작된다.

미확정 상태에서 사망/Stun/Panic 등으로 선언이 무효가 되면 기존 선언/Commit 검사가 비용과 실행을 거부한다. 새 자동 취소·공격권 재배분 정책은 만들지 않았다. 종료·Abort·Unbind 후 과거 교체 Token은 사용할 수 없다.

## 7. Cue·UI·공개 API·직렬화

- ENEMY-04A `feintCueEnabled`, 값 요청, Director 이벤트와 Cue enum은 이번에 수정하지 않았다. Cue는 교체 권한이 아니며, 교체 성공도 Cue의 발행/수신 여부에 의존하지 않는다. 두 권한의 독립성을 테스트했다.
- 기존 Response 이벤트에 현재 선언 정보가 실린다. Player Controller가 새 요청을 받으면 스킬/대상 선택을 지우고 새 유효 Response 입력 기회를 제공한다.
- Binder는 기존 `contextText`에 새 스킬 이름·대상 ID·이전 선택 해제 안내를 표시한다. 기존 UI의 대상 표기 방식을 유지했다. 새 Router/입력 잠금/GameState 작성자 없음.
- `ProductionDungeonUI.prefab`의 기존 contextText 참조는 파일 검사로 확인했다. 실제 Dungeon에서 이 문구와 버튼 재생성·레이아웃이 올바르게 표시되는지는 검증하지 않았다.
- 기존 public 명령 API와 serialized 필드를 유지했다. 새 Token, 조회/교체 메서드와 요청 객체를 받는 Response/NoResponse overload를 추가했다. 기존 version-only API는 호환용으로 남아 있으므로 지연 입력은 Session 정보가 있는 새 요청 overload를 사용해야 한다. Production Player Controller는 이 경로로 전환했다.
- 신규 serialized 항목은 Profile의 `declarationFeintEnabled` 하나이며 기본 false다. 기존 SO/Scene/Prefab에 값을 쓰지 않았다. Inspector 재연결은 이번 코드 실행에 필요하지 않다. 향후 콘텐츠별 활성화는 별도 검증 대상이다.

## 8. 실제 검증 근거

| 실행 | 결과 | 근거 |
| --- | --- | --- |
| Unity C# 컴파일 | 성공 | Focused-1/2 로그의 Tundra build success 및 실제 테스트 실행 |
| 최종 집중 실행 | **28/28 통과**, 실패/ignored 0 | `Logs/CBT-ENEMY-04B-Focused-3.xml`, `.log` code 0 |
| 집중 중 순수 EditMode | **26/26 통과** | 위 XML에서 EnterPlayMode 2건을 제외한 사례 |
| 집중 중 실제 EnterPlayMode | **2/2 통과** | ReplaceFirst / ResponseFirst 별도 UnityTest |
| 전체 EditMode | **1,407 통과 / 0 실패 / 1 ignored**, 총 1,408 | `Logs/CBT-ENEMY-04B-AllEditMode.xml`, `.log:169029` code 0 |
| 전체 중 Combat | **765/765 통과** | 전체 XML Combat suite |
| 전체 중 Integration | **512 통과 / 0 실패 / 1 ignored** | 전체 XML Integration suite |

전체 실행은 2026-10-17 14:00 KST에 완료했다. 전체의 root 결과 `Skipped:Ignored`를 1,408/1,408 통과로 해석하지 않는다. ignored는 `PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners`: 선행 write 및 새 프로세스 `-party05vRestartStage read`가 필요한 별도 절차다. 이번에는 이 조건을 만들거나 ignored 테스트를 통과했다고 주장하지 않는다.

기존 FirstValid/Conditional/ENEMY-02V/04A, Clash/Response/Chain, Status/Posture, Panic/Overcome, Party/Reward/Quest/Save 회귀는 전체 실행에 포함됐다. 집중과 전체에 중복 포함된 테스트를 독립 커버리지로 합산하지 않는다.

### 격리 Play Mode에서 실제 실행한 것

저장되지 않는 빈 Scene → 실제 EnterPlayMode → Tests 전용 메모리 EnemyDefinition/Profile → 실제 Bridge/Request/EntryPoint/Session → 기존 Enemy Controller 초기 공격 → 명시적 교체 또는 Response 우선 → 기존 Player Controller의 스킬/대상 선택 및 Confirm → 실제 MP Commit → Director Approach → Clash → SkillRunner → 피해/Status → 다음 Standoff → 플레이어 마무리 공격 → Victory CombatResult와 Session 정리.

교체 우선에서는 원래 skill 1이 실행되지 않고 skill 2만 실행됐다. Response 우선에서는 교체가 실패하고 skill 1만 실행됐다. 두 경우 모두 적 MP 2와 대응 MP 1을 한 번만 지불했다. 대상 HP는 각각 7/9, 해당 Status 스택 1, 다른 Status 없음, WinningSkill 신호도 예상 스킬 한 번을 확인했다. 테스트 목적 수치이며 게임 밸런스가 아니다.

물리 키 입력·InputAction을 발생시키지 않고 실제 Player Command Controller API로 대응했다. 적 교체는 Fixture가 EntryPoint의 기존 Controller를 읽어 명시적으로 호출했다. 정책/SkillRunner/Commit은 Mock으로 대체하지 않았다. EditMode의 순수 규칙 테스트만 고정 난수/합 입력 제공자를 사용했고 실제 Play Mode는 기본 Production providers와 Director를 사용했다.

EditMode는 성공·실패 무변경, 양쪽 승인 순서(Commit 전 Response 승인 포함), 낡은 Session/선언/revision/version, 중복 교체, 부족 MP/미보유/잘못된 target/Utility/Stun/Panic/사망, 다수 적·다음 Session, 실제 Clash/Unopposed와 Status 스택 1, Chain 중 거부, 동기 이벤트 재진입을 검증했다.

### 초기 실패와 경고

- Focused-1: 24 통과 / 2 실패. 도메인 재로드를 넘는 테스트 iterator/closure 구성 문제로 Fixture 생성 전 NullReference. public UnityTest 안에서 Enter/ExitPlayMode를 직접 수행하도록 수정했다.
- Focused-2: 26 통과 / 2 실패. 무효 교체 검증용 0 MP Utility 스킬이 기존 AI의 정상 연계 후보로 남아 다음 Standoff를 기다리는 Fixture가 실패했다. 그 테스트 스킬 비용만 3으로 바꿨다. Production 규칙을 바꾸지 않았다.
- Focused-3 및 전체는 최종 코드로 통과. 실제 Play Mode 수집기의 예상치 못한 Error/Exception/Assert는 0.
- 컴파일의 기존 FieldEnemy 미사용 이벤트 1건, Story/RescueNpc 미사용 필드 3건 경고를 별도로 확인했다.
- 빈 Scene에서는 로컬 CombatFieldLock 생성과 combatRoot 미연결 경고가 있다. Production UI 배치를 검증했다는 뜻이 아니다.
- 전체 로그의 subscriber failure/주입된 damage/startup 예외는 기존 예외 처리 회귀의 예상 로그다. licensing access token 안내도 테스트를 실패시키지 않았다. 전체 Console 로그 전체가 오류 문자열 0이라고 보고하지 않는다.

## 9. 미검증·수동 절차·위험

미실행: 별도 PlayMode assembly, Player Build, 실제 Dungeon Play Mode/UI/Inspector 연결, 실제 키 입력, SO/Prefab의 실콘텐츠 활성화, PARTY-05V 별도 프로세스 read.

자동 시점/확률/횟수 규칙을 정하지 않았으므로 실제 콘텐츠에 자동 Feint는 발생하지 않는다. 같은 actor/target의 초기 선언 교체만 지원하며 Chain Feint·단순 철회·자동 무효 선언 정리/공격권 재배분·시간제 Response는 범위 밖이다. 기존 Legacy와 version-only 호환 API는 유지했다.

수동 확인 절차:

1. Unity 6000.3.23f1 Test Runner의 EditMode에서 `FinalCombatDeclarationFeintTests`를 실행한다. EnterPlayMode 두 테스트가 빈 메모리 Scene에서 실제 전투를 끝내는지 확인한다.
2. 격리 Fixture의 교체 승인 전후에 디버거로 `CurrentDeclaration`, Token, `PendingDecisionRequest`, MP를 확인한다. 이전 request를 제출하면 false, 새 request를 제출하면 true인지 확인한다.
3. 향후 별도 승인된 콘텐츠 연결 작업에서 기존 contextText와 Response 버튼을 관찰한다. 교체 시 새 스킬/대상과 선택 해제 안내가 보이고, 기존 선택이 지워진 뒤 다시 대응할 수 있는지 확인한다. 보호 Scene/Prefab에 임시 Fixture를 저장하지 않는다.
4. 실제 Dungeon/Input/연출 자산 및 pause 동작은 해당 연결 검증에서 추가한다. 이번 테스트 결과로 그 범위를 완료 처리하지 않는다.

## 10. Development tracking

- System: Combat / FinalExchange Enemy declaration feint V1.
- Requirement: CBT-ENEMY-04B 및 ENEMY-03 B안의 초기 선언 교체 계약.
- Production owner: 기존 Enemy Command Controller → Orchestrator → StateMachine/ExchangeState; 기존 Player Controller/Binder는 표시·응답 전달.
- Implementation status: **Implementation Complete** (명시적 opt-in 요청 범위).
- Compile validation: Verified.
- Inspector/serialized assets: API·필드 호환 및 기존 연결 파일 검사. Production 실행은 미검증, 자산 변경 없음.
- Play Mode validation: **실제 Editor EnterPlayMode 2/2 Verified**.
- Integration validation: 격리 EntryPoint → 선언 변경/Response → Commit/Clash/SkillRunner → CombatResult Verified.
- Recommended status: **Implementation Complete — Isolated Integration Verified / Production Dungeon UI·Input Verification Pending**.
- Remaining: Production UI/Inspector/Input·Player Build·실콘텐츠 활성화. System Complete 아님.
- 문서 차이: ENEMY-03의 timer/window·취소 목적지 제안은 미구현. 04B의 명시적 교체만 필요하므로 기존 Pending 상태를 재사용했다.

종료 점검: 의도한 diff와 새 파일을 검토했고 `git diff --check` 통과. 기존 33개 미커밋 중 관련 4개를 최소 확장했고 나머지 29개는 SHA-256 동일. 보호 자산/기존 meta 변경, 삭제·이동, commit/push 없음.
