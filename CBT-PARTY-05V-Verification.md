# CBT-PARTY-05V — 실제 디스크 Save/Load 통합 검증

검증일: 2026-10-15 (Asia/Seoul). 브랜치: `main`. Unity: `6000.3.23f1 (09d2ecc7fb28)`.

## 결과

**검증 통과. Production 코드 수정 없음.** 최종 전체 EditMode 회귀는 **1,358 / 1,358 통과, 실패 0, 건너뜀 0**, Unity 프로세스 종료 코드 0이다. EditMode runner에서 `EnterPlayMode`로 실행하는 격리 FinalExchange 테스트를 포함한다. 별도 Player Build / standalone Player 실행은 하지 않았다.

| 요구 사항 | 실제 검증 결과 |
|---|---|
| 3인 Party FinalExchange 승리 | 실제 EnterPlayMode에서 CombatEntryPoint → FinalExchange 해결 → Victory 결과 발행 |
| 참가자별 EXP | A 150, B 150, C 150; 비참가 reserve 0 |
| KO 동료 | 전투 중 B의 HP를 0으로 만들고 결과의 IsAlive=false 및 EXP 150 확인 |
| Gold / Item | 같은 Completion에 Gold 50, 테스트 토큰 2개; 저장 전후 및 재전달 후 동일 |
| Reward Ledger Schema 12 | 수령자 3명의 requestedExp/appliedExp/settled, 전체 expSettled 및 combatLedger 복제 기록 확인 |
| 디스크 저장 | Production SaveLoadService.TrySave가 실제 JSON 파일 생성; 비어 있지 않음, 임시 파일 잔류 없음 |
| 동일 프로세스 재구성 | 저장 담당·성장·보상·지갑·인벤토리 서비스 5종의 기존 GameObject를 파괴하고 새로 생성; 복원 전 0 상태 확인 |
| 디스크 복원 | 새 SaveLoadService.TryLoad 및 OnLoadCompleted 성공 확인; 파일 읽기 중 내용 불변 확인 |
| 동일 Completion 재전달 | DuplicateBlocked=true, 결과 EXP/Gold/Item 0, 기존 성장·지갑·인벤토리·Ledger 불변 |
| 부분 지급 | B의 정의만 제외한 테스트에서 A/C 각 150, B Pending; 실제 파일에서 수령자별 상태 확인 |
| Pending 재정산 | 전체 정의를 가진 새 서비스에서 로드할 때 ProgressionChanged가 B에 대해서만 1회 발생; A/C 중복 없음 |
| 최종 정산 보존 | Pending 0, Ledger 전체 정산 완료; 다시 저장·서비스 재생성·파일 복원 후 중복 지급 없음 |
| Schema 11 → 12 | 실제 Schema 11 파일 로드, A의 기존 EXP 150 및 Completion ID 보존; 구보상 재전달 차단; 12 재저장·재복원 |
| Schema 1~11 | 각 버전의 과거 단일 수령자 합성 파일을 Production 로드 경로로 마이그레이션하고 12로 재저장·재복원 |
| 미래 Schema | Schema 13 파일 거부, 메모리 Gold 및 입력 파일 불변, OperationState가 Idle로 복귀 |

A=`test.party.leader`, B=`test.party.ally.one`, C=`test.party.ally.two`, reserve=`test.party.ally.three`.

테스트 성장 정의는 메모리에만 생성하며 레벨업 문턱을 1,000으로 설정했다. EXP 150을 레벨업에 소비하지 않고 정확히 관찰하기 위한 설정이며 Production EXP 정책이나 SO를 변경하지 않았다.

CombatResult 변환은 원래 Item payload를 만들지 않는다. 테스트는 Production CreateCombatRewardRequest의 Completion/Gold/EXP/수령자를 유지하면서 `party05v.token` 2개를 동일 요청에 붙여 Item 채널도 검증한다. 실제 던전에서 이 아이템이 드롭된다는 의미는 아니다. 전투 입력·정의·적 응답은 격리 fixture가 제어하고 승리 판단과 EXP 지급은 실제 Production 로직을 사용한다.

## 실제 Editor 재시작과 동일 프로세스 재구성의 구분

- **동일 프로세스:** 다섯 서비스 GameObject를 실제 파괴하고 새 인스턴스를 생성한다. 기존 인스턴스 파괴 및 새 상태가 0임을 확인한 다음 Production TryLoad를 실행한다. DTO를 직접 RestoreSaveData에 전달하는 대체 경로를 사용하지 않는다.
- **Editor 재시작:** 쓰기 실행 Unity PID **32040**이 종료된 뒤 새 Unity PID **25000**에서 파일을 읽었다. 재시작 전용 테스트 **1 / 1 통과**, 종료 코드 0. 최종 전체 회귀 PID **32008**에서도 이전 프로세스 파일 읽기가 다시 통과했다.
- 재시작 입력은 FinalExchange 완료 파일, B Pending 파일, Schema 11 파일이다. 완료 파일은 EXP 적용 이벤트 0회, Pending 파일은 B에 대해서만 적용 이벤트 1회다. 읽기 후 동일 Completion 재전달로 EXP/Gold/Item 불변을 검증했다.
- 이는 실제 Unity Editor OS 프로세스의 종료·재실행이다. 컴퓨터 재부팅, Player 재시작 또는 Production 던전 Continue 버튼의 수동 조작 검증은 아니다.
- `Party05VDiskVerification`이라는 이름의 메모리 빈 씬을 양쪽 프로세스에서 사용했다. 씬 전환 로드는 이번 격리 검증의 대상이 아니다.

## 실행 증거

모든 경로는 저장소 루트 기준이다. `Logs`는 Git에서 제외되며 로컬 증거 파일을 삭제하지 않고 보존했다.

| 실행 | XML | 결과 |
|---|---|---|
| 최초 집중 검증 및 쓰기 | `Logs/CBT-PARTY-05V/FocusedWrite.xml` | 15 통과 / 0 실패 / 재시작 읽기 전용 1개 의도적 보류 |
| 별도 Editor 프로세스 읽기 | `Logs/CBT-PARTY-05V/EditorRestart.xml` | 1 통과 / 0 실패 / 0 건너뜀 |
| 최초 전체 회귀 | `Logs/CBT-PARTY-05V/FullEditMode.xml` | 1,343 통과 / 새 fixture 준비 단계 14 실패 / 1 보류 |
| **수정 후 최종 전체 회귀** | **`Logs/CBT-PARTY-05V/FullEditModeFinal.xml`** | **1,358 통과 / 0 실패 / 0 건너뜀** |

최초 전체 회귀 실패는 다른 EditMode 테스트가 남긴 파괴된 Unity wrapper를 NUnit `Is.Null`로 검사한 새 테스트의 결함이었다. Unity의 `== null` 검사로 바꾼 후 전체 회귀를 다시 통과했다. Production 결함은 발견되지 않았다. 실패 결과도 감사용으로 보존했다.

최종 로그: `Logs/CBT-PARTY-05V/FullEditModeFinal.log`.
재시작 로그: `Logs/CBT-PARTY-05V/EditorRestart.log`.
프로세스 실행 기록: `Logs/CBT-PARTY-05V/ProcessRuns.jsonl`.

실제 파일 증거:

| 파일 (`Logs/CBT-PARTY-05V/Restart/`) | 바이트 | 의미 |
|---|---:|---|
| `final-exchange.json` | 7,050 | 실제 FinalExchange 승리 후 Schema 12, A/B/C 각 150, reserve 0, Gold 50, 토큰 2 |
| `partial.json` | 5,257 | A/C 지급 성공, B appliedExp=0 / settled=false |
| `partial-settled.json` | 5,408 | 재시작 후 B만 정산된 Schema 12 |
| `schema11.json` | 522 | 기존 A EXP 150 / 완료 보상을 포함한 Schema 11 입력 |
| `schema12.json` | 4,006 | 재시작 후 마이그레이션하여 Production 저장한 Schema 12 |

각 테스트가 만든 고유 폴더에는 추가 원본·Schema 12 파일과 `.bak`도 남아 있다. `DiskEvidenceSummary.json`에 주요 파일의 SHA-256·수령자 EXP·Ledger가 정리되어 있다. Schema 1~11의 `.bak`이 입력 구버전 JSON과 정확히 같음을 테스트에서 확인했다.

## 컴파일, 경고, 오류

- **Unity C# 컴파일:** 실제 Unity 6000.3.23f1에서 Csc `Assembly-CSharp-Editor.dll` 재컴파일, Bee/Tundra build success, 어셈블리 로드 및 전체 테스트 실행 확인. 최종 로그의 C# compiler error/warning 없음.
- **경고:** 기존 `Assets/GAME/Scenes/Tests/Tests.asmdef`에 연결된 스크립트가 없어 해당 어셈블리를 컴파일하지 않는 경고가 있다. 변경하지 않았다. 중복 보상 경고와 미래 Schema 거부 경고는 의도한 검증 경로이며 테스트 결과로 확인했다. 전체 로그에는 기존 fixture의 예상 경고·오류 출력도 포함되므로 로그 문자열 개수를 실패 건수로 해석하지 않는다.
- **오류:** 최종 XML 실패 0. 격리 FinalExchange 테스트의 Error/Exception/Assert 수집 결과 0. 최초 제한 환경 실행은 라이선스 초기화 및 Mono 소켓 초기화에서 실패하여 유효 테스트 결과가 없었다. 정상 권한 환경 재실행으로 해소했으며 해당 실패 로그도 보존했다.
- **패치 검사:** 최종 `git diff --check`에서 공백 오류 없음. Git이 `ProjectSettings/TimeManager.asset`의 LF/CRLF 변환 안내를 출력하지만 해당 파일의 Git 내용 차이는 없다.
- **Player Build / standalone Player:** 실행하지 않음, 미검증.

## 지침, 범위, Production Owner 및 사용자 데이터 보호

- 적용 지침: 사용자 작업 지시 + 루트 `AGENTS.md`. 중첩 `AGENTS.md` / `AGENTS.override.md` 없음.
- 시작 상태: `main`, 미커밋 변경 및 미추적 파일 없음. commit / push / 브랜치 생성 없음.
- Production Owner: 전투 시작·종료 `CombatEntryPoint`; 저장·복원 `SaveLoadService`; Completion 및 수령자별 Ledger `RewardService`; 성장 상태 `CharacterProgressionService`; Gold/Item 상태 `CurrencyWallet` / `InventoryService`.
- 기존 Legacy 단일 수령자 보상, combatLedger 호환 기록, Schema 1~11 migration을 보존했다. 별도 Production 저장·보상 경로를 만들지 않았다.
- 수정: `Assets/GAME/Tests/Editor/CombatPartyRosterPlayModeIntegrationTests.cs` — 기존 EnterPlayMode 보상 검증을 실제 디스크·새 서비스 복원으로 보강.
- 추가: `Assets/GAME/Tests/Editor/PartyRewardDiskSaveTests.cs`, Unity가 생성한 해당 `.cs.meta`, 이 검증 보고서.
- 삭제 / 이동: 없음. Production 공개 API / serialized field 변경: 없음.
- Inspector 재연결, 컴포넌트 추가·제거, Production asset 참조 변경, 사용자 데이터 migration 작업: 없음. 신규 테스트 스크립트의 `.meta`만 추가되며 기존 `.meta`는 변경하지 않았다.
- `Dungeon_1_Production.unity`, `Dungeon_Template.unity`, `CombatRuntime.prefab`, `ProductionDungeonUI.prefab`, Production SO / 기존 `.meta` 변경 없음. Unity·패키지·Input·URP 버전 변경 없음.
- 테스트 SaveLoadService는 기존 `SetStoragePathForTests`로 `Logs/CBT-PARTY-05V` 아래에만 저장한다. 사용자 Save Slot을 쓰거나 삭제하지 않았다.
- 실행 전후 `AppData/LocalLow/DefaultCompany/GAME 00-2`의 확인 대상 3개 파일을 비교했다. **파일 집합·SHA-256·크기·수정 시각 변경 0**. 여기에는 실제 `game_save.json`, `game_save.json.bak` 및 기존 Unity analytics JSON이 포함된다. 증거는 `UserSaveBefore.json`, `UserSaveAfter.json`, `UserSaveComparison.json`.
- 실제 Save의 SHA-256: `5EE668FFAF0562030F679A39077B90C80E5C47C7D43B9E1E693C683766B47992` (3,055 bytes). 백업: `78FBA30AFE81B9F909FDC03E45F4AD833A36B1F0102F37D1DC96C4F75EFC53A8` (7,462 bytes).
- 무관한 기존 사용자 변경은 보존했다. 이번 검증 외 Production 변경은 없다.

## 재현 및 수동 확인 절차

1. Unity 6000.3.23f1을 사용한다. 테스트를 수행할 다른 Editor 인스턴스가 프로젝트를 점유하지 않게 한다. 사용자 Save Slot은 그대로 둔다.
2. Test Runner의 EditMode에서 `CBT_PARTY_05V` category를 실행한다. EnterPlayMode 테스트에서 B KO, Victory, 파일 생성, 다섯 서비스 재생성, 파일 복원, 중복 보상 차단을 자동 확인한다. 재시작 읽기 전용 테스트는 별도 인자 없이는 의도적으로 보류한다.
3. 실제 새 Editor 프로세스 읽기를 재현하려면 현재 `Logs/CBT-PARTY-05V/Restart` 증거 폴더를 별도 이름으로 보관한다. 이 폴더는 테스트 산출물 전용이며 사용자 저장 폴더가 아니다. 쓰기 테스트는 기존 재시작 입력 파일을 덮어쓰지 않도록 실패한다.
4. Unity를 `-batchmode -nographics -projectPath <repository> -runTests -testPlatform EditMode -testCategory CBT_PARTY_05V -testResults <write.xml> -logFile <write.log> -party05vRestartStage write`로 실행하고 완전히 종료될 때까지 기다린다.
5. 새 Unity 프로세스를 `-batchmode -nographics -projectPath <repository> -runTests -testPlatform EditMode -testFilter EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners -testResults <read.xml> -logFile <read.log> -party05vRestartStage read`로 실행한다. 입력 3개를 복원하며 A/B/C EXP, Pending B만 지급, 구버전 재지급 차단을 확인한다.
6. 전체 회귀는 `-runTests -testPlatform EditMode -party05vRestartStage read`와 고유 XML/로그 경로로 실행한다. 이번 최종 실행은 이 방식으로 1,358개가 모두 통과했다. `-quit`은 테스트 완료 전에 종료할 수 있으므로 이번 실행에 사용하지 않았다.
7. 수동으로 남은 항목을 검증하려면 별도 승인된 테스트 저장 경로를 사용하는 Player 환경에서 저장→종료→재실행→복원→동일 Completion 재전달을 확인한다. Production 던전의 Continue 버튼·Inspector 연결과 실제 아이템 드롭 구성도 별도 검증 대상이다. 사용자 Save Slot로 대신 검증하지 않는다.

## Development tracking

- System: Combat / Party / Reward / Progression / SaveLoad.
- Feature: CBT-PARTY-05 다인 EXP 및 Reward Ledger Schema 12의 실제 디스크 저장·복원, Pending 재정산, 구버전 호환 검증.
- Production owner: CombatEntryPoint → RewardService / CharacterProgressionService → SaveLoadService.
- Requirement: 현재 작업 `CBT-PARTY-05V`. 외부 기획 문서·추적표를 구현 근거로 사용하지 않았고 수정하지 않았다.
- Implementation: **Implementation Complete**. 검증 테스트와 보고서 완료; Production 변경 없음.
- Compile: **Compile Verified** — Unity 6000.3.23f1.
- Inspector / serialized assets: 변경 없음, 원본 보존 확인. 실제 Inspector 조작 검증은 **Unverified**.
- Play Mode: **Play Mode Verified** — 격리 EnterPlayMode FinalExchange 승리, KO EXP, 디스크 복원.
- Integration: **Integration Verified (isolated)** — 실제 Production 저장·보상·성장 서비스 및 실제 Editor 프로세스 재시작 경계. 전체 던전 사용자 흐름이나 Player 통합 검증으로 확대 해석하지 않는다.
- Full EditMode regression: **1,358 passed / 0 failed / 0 skipped**.
- Remaining unverified: Player Build / Player 재시작, Production 던전 Continue UI 및 Inspector 수동 검증, OS 재부팅·전원 중단·파일 손상 주입은 이번 Batch에서 수행하지 않음. 부분 지급 실패 주입은 B 정의 누락에 한정한다.
- Recommended development-tracking status: **CBT-PARTY-05V 검증 통과 — 실제 디스크 및 Editor 재시작 Integration Verified; Player Build 검증 대기**. System Complete로 기록하지 않는다.
- Stale documentation / mapping: 이번 범위에서 확인한 충돌 없음. 기존 메모리 JSON 복원 테스트를 실제 디스크 복원 근거로 오인하지 않도록 이번 증거로 구분한다.
