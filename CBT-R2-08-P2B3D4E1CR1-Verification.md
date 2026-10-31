# CBT-R2-08-P2B3D-4E1C-R1 — New Game 초기 스킬·장착 및 Save/Load

## 결과와 적용 범위

**Implementation Complete — Compile Verified / Isolated Automated Play Mode & Integration Verified.** 기존 Core/Skill Owner 내부에 요청 구분과 데이터 기반 초기 보유·장착을 구현했다. Production Scene/Prefab/SO 연결 및 Cutover는 하지 않았다. 현재 Production의 빈 CharacterStart 연결과 기존 Combat Config는 그대로이므로, 실제 Production New Game에 아난이 활성화됐다는 뜻은 아니다. System Complete로 판정하지 않는다.

요구사항 출처는 이번 첨부 작업 요청이다. 선행 필수 자료 `CBT-R2-08-P2B3D4E1C-Partial.md`, `CBT-R2-08-P2B3D4E1A-ProductionConnectionAudit.md`를 읽고 실제 코드와 자산을 대조했다.

## 지침·Git·보존

- 적용 지침: 루트 `AGENTS.md`; 숨김 폴더 포함 재검색에서도 적용 중첩 `AGENTS.md`/`AGENTS.override.md` 없음.
- branch: `main`; HEAD: `b42544824838e7de0e70b71afc2ca21a59db8192`. branch/commit/push/PR 없음.
- 시작 시 기존 tracked 수정: `CombatEncounterGroup.cs`, `CombatEncounterTrigger2D.cs`, `WorldEncounterIntegrationTests.cs`의 3파일/59추가줄.
- 기존 아난 Character/Skill SO, 기존 후보 Config, 격리 테스트·Scene·Prefab 및 선행 보고서는 미추적 상태였다. 기존 파일이라는 이유로 덮어쓰거나 정리하지 않았다.
- 작업 전 tracked/untracked 2,652개 파일 SHA-256을 `Logs/CBT-R2-08-P2B3D4E1CR1-Before.json`에 기록했다. 최종 대조에서 계획한 6개 기존 파일만 변경됐고 **나머지 2,646개 전부 일치**, 누락 0개다. 추가 파일은 아래 5개뿐이다. 결과는 `Logs/CBT-R2-08-P2B3D4E1CR1-Preservation.json`에 기록했다. 기존 사용자 수정 3개와 미추적 파일, 보호 자산·모든 기존 GUID가 보존됐다. 최종 branch/HEAD도 시작과 같다.

## 실제 Production Owner와 호출 경계

1. `GAME.Title.TitleSceneController.HandleStartClicked`: 실제 New Game 선택이며, 기존 버튼/서류 진행 guard를 통과한 경우에만 요청을 만든다.
2. `SaveLoadService.TryCreateNewGameRequest` → Title이 보관한 내부 요청 객체 → 서류 승인 coroutine의 `TryApplyNewGameRequest`.
3. `SaveLoadService.TryResetRuntimeForNewGame`: 기존 순서의 `INewGameRuntimeReset`을 처리한다. Party → Progression → 기존 후속 Owner 순서는 유지한다.
4. `CharacterSkillSaveParticipant.ResetForNewGame`: 기존 `CharacterSkillRuntime.Reset`, `TryAcquire`, `TryEquip`으로 초기 상태를 구성한다. 별도 Skill Manager/Film 획득/전투 시작 지급 없음.
5. `RuntimeBootstrapper`: 기존 Party/Progression/Skill Owner에 같은 Start 설정만 전달한다. 설정/씬 재진입 자체로 스킬을 지급하지 않는다.
6. 전투 요청 → 기존 `CombatEntryPoint` → `PersistentSkillCombatLoadoutBridge` → 기존 `CombatSession` 및 Hand 초기화. 이 경로의 전투 규칙은 수정하지 않았다.
7. Continue → 기존 `SaveLoadService.TryLoad` → 씬 준비/`RestoreGameSaveDataSnapshot` → Skill Runtime의 `Restore`. New Game 지급 호출 없음.

### 요청 중복과 명시적 재시작

- 요청 identity는 **기존 SaveLoadService가 발급한 내부 객체의 참조**다. 공개 메서드/공개 타입/Save Schema 추가 없이 Production의 단일 Title 호출자를 연결했다.
- 같은 요청이 성공한 뒤 다시 도착하면 상태와 무관하게 성공한 처리 이력만 반환하고 Reset Owner를 호출하지 않는다. 진행 후/Title 재방문 후에도 같은 객체의 재실행은 무변경이다.
- 다른 실제 Start 선택은 다른 객체를 발급받으므로 기존 Reset 정책으로 새 상태를 만든다. 발급만 됐던 이전 요청은 거부한다.
- Reset 진입 전에 pending 요청을 소비하고 기존 operation state로 재진입을 막는다. 부분 실패의 재전송은 재초기화하지 않으며 새로운 명시적 선택이 필요하다. 기존 Reset 전체를 트랜잭션으로 바꾸지는 않았다.
- Load/직접 snapshot restore는 미처리 pending 요청을 무효화한다. 이미 처리한 이전 요청의 늦은 재전송도 저장 복원 상태를 바꾸지 않는다.
- Title은 coroutine 시작 시 해당 요청을 캡처하고, 요청 교체 또는 이미 요청한 씬 이동을 확인해 중복 씬 이동도 막는다.
- **호환 API의 한계:** 기존 공개 `TryResetForNewGame(out string)`는 요청 정보가 없는 명시적 Reset API로 그대로 남겼다. 매 호출은 기존 정책의 새 Reset이다. 이 API가 동일 요청과 새 요청을 구분한다고 주장하지 않는다. 실제 Runtime 호출자는 Title 한 곳이었고 새 내부 경계로 전환했다. 기존 테스트/호환 호출자는 보존했다. 외부 호출자가 중복 보호를 원하면 이 요청 경계로 이전해야 하며, 기존 공개 API의 의미를 임의로 바꾸지 않았다.
- static flag, PlayerPrefs, 전역 Request ID Manager 및 새 Singleton 없음. 새 이벤트 구독 없음.

### 경쟁·호환 경로

`Game.Demo.TitleSceneController`는 계약 패널을 열며 정식 SaveLoad Reset 호출자가 아니다. `TitleScene.unity`는 `GAME.Title.TitleSceneController` GUID를 참조한다. 기존 LegacyPlanning/Debug/compatibility loadout fallback과 `CharacterSkillRuntime`의 Film/Unique 계약은 유지한다. Production의 Hand/BP Debug 자산 gate는 이 작업에서 해소하지 않는다.

## 초기 데이터와 후보 사용

새 `Assets/GAME/Data/Character/CharacterStart_AnandSkills_PlaytestV1_Candidate.asset`을 Unity로 생성했다. GUID는 `b6e0d9b3b325b104f9ad84ed61524bb7`이다. 기존 `CharacterStart_AnandDeMorte.asset`은 수정하지 않았다.

| 구분 | 새 게임 설정 |
|---|---|
| Party/Leader/Combat member | `char.anand_de_morte` 1명 |
| Progression | 기존 CP 참조, Level 1 / EXP 0 |
| 캐릭터별 보유·장착 key | `skill.playtest.anand.basic`, `skill.playtest.anand.zornhau` 각 1개 |
| Film 공유 소유권 | 추가 없음 |
| Normal | Skill ID 10001, 2장 |
| Clash | Skill ID 10002, 3장 |
| 시작 MP | 기존 덱 후보의 2 |
| Contact | 기존 Production 적 선공 유지 |

Start SO에는 선택적인 `initialSkillGrants` 직렬화 필드를 추가했다. 기존 자산의 필드 부재는 빈 목록으로 호환된다. 초기 Party에 포함된 캐릭터만, 중복 없는 Compatibility key/runtime ID만 허용하며 Film/Unique 위장을 거부한다. 구성 전 기존 Owner들을 검증한다.

덱 후보 `FinalCombatRuntimeConfig_AnandStart_PlaytestV1_Candidate.asset`과 GUID `44a8b6f4c6314396953f0792e126d13a`는 **바이트 단위로 보존**했다. 테스트만 해당 실제 후보를 읽어 메모리 복제본의 `enableProductionHandBp`를 true로 설정한다. handCardRules/MP/BP/Clash/seed 값 변경이나 TestOnly fixture의 Production 등록은 없다.

`handCardRules`는 스킬별 정적 copies/kind 정의다. 장착 snapshot에 실제 포함된 스킬에만 해당 규칙을 적용하며, 카드 인스턴스·소비·Discard는 매 CombatSession마다 만든다. 영구 보유는 카드 5개가 아니라 스킬 key 2개이고, 이 둘을 혼동하지 않는다. 장착 변경은 Save가 지원한다. 사용자별 copies 편집/저장 계약은 현재 없으며 새 필드를 추가하지 않았다. 전부 미장착일 때의 기존 compatibility fallback 정책도 변경하지 않았다.

## 변경 파일

수정한 기존 파일 6개:

- `Assets/GAME/Scripts/Core/SaveLoadService.cs` — 내부 New Game 요청 경계, Load 시 pending 무효화, 기존 Reset 코어 재사용.
- `Assets/GAME/Scripts/Title/Runtime/TitleSceneController.cs` — 실제 선택별 요청 캡처/전달 및 중복 씬 이동 guard.
- `Assets/GAME/Scripts/Core/RuntimeBootstrapper.cs` — 기존 Skill Owner를 먼저 마련하고 Start 설정을 함께 검증·전달.
- `Assets/GAME/Scripts/NonCombat/Progress/CharacterStartDefinitionSO.cs` — 선택적인 초기 Skill 설정 및 검증.
- `Assets/GAME/Scripts/NonCombat/Progress/CharacterSkillSaveParticipant.cs` — 설정만 수용하고 명시적 New Game Reset에서 기존 Runtime API로 지급·장착.
- `Assets/GAME/Tests/Editor/SaveLoadPreparationTests.cs` — 기존 Title 위임 검사가 새 내부 호출을 확인하도록 갱신.

추가 파일:

- 위 Start 후보 `.asset`와 Unity 생성 `.meta`.
- `Assets/GAME/Tests/Editor/AnandNewGameSaveIntegrationTests.cs`와 Unity 생성 `.meta`.
- 본 보고서.

삭제·이동 없음. Production 공개 API 추가/변경/삭제 없음. 내부 요청/설정 API만 추가했다. Save DTO/Schema/마이그레이션 변경 없음(schema 14 유지). 기존 persistent identity와 `.meta` GUID 보존. 실제 Scene/Prefab/기존 SO 수정, 새 컴포넌트 연결, Inspector 재연결 없음. 새 후보는 Production에서 참조하지 않는다.

새 테스트 meta GUID는 `e51046b7f22815041ae70937952b16a7`이다. 이 GUID와 새 Start GUID를 전체 Assets meta에서 검색해 각각 1개만 존재함을 확인했고, 새 Start GUID의 Scene/Prefab/다른 SO 참조는 없다. 기존 덱 후보 GUID도 그대로 1개다.

## 실행 검증

Unity **6000.3.23f1**을 사용했다. `Logs/CBT-R2-08-P2B3D4E1CR1-*`에 실행 로그/결과/저장 샘플을 남겼다. 제한 환경의 최초 실행은 WMI/라이선스 초기화에서 실패하여 호스트 실행으로 검증했다. 초기 신규 테스트의 누락된 namespace 참조는 수정했으며 최종 컴파일 오류는 없다.

| 검증 | 실제 결과 / 증거 |
|---|---|
| 후보 생성·컴파일 | `CreateHost2.log`, exit 0. Unity가 새 Start와 meta 생성 |
| 최초 신규 테스트 실행 | `Focused.xml`: 14/14 PASS. 이후 중복 후 덱 수량 검사 1건 추가 |
| 최종 선택 회귀 | `Regression.xml`: **1,014 total / 1,007 PASS / 6 FAIL / 1 IGNORED**, Unity exit 2(기존 실패 포함) |
| 신규 클래스 | 최종 회귀에서 **15/15 PASS**, 실제 EnterPlayMode/ExitPlayMode 검사 1건 포함 |
| 기존 Title/Scene Load | CharacterProductionSceneTests **2/2**, SaveLoadSceneIntegrationTests **1/1 PASS**. 실제 Title 버튼/서류 승인과 Cold Continue 포함. 새 아난 후보를 Production에 저장한 검사는 아님 |
| 기존 CharacterSkillRuntime | **11/11 PASS** |
| 기존 SaveLoadPreparation | **42/42 PASS** |
| 기존 조우/재조우 | WorldEncounterIntegrationTests **111/111 PASS**; 기존 아난 격리 Scene 검사도 선택 회귀에서 통과 |
| 진짜 프로세스 종료 후 저장 복원 | `WriteProcessRestartEvidence`를 실행한 Unity 종료(exit 0) 후 `ReadProcessRestartEvidence`를 다른 Unity에서 실행(exit 0). writer PID와 reader PID가 다름을 assertion으로 확인. `RestartReadEvidence.txt` PASS |
| 패치 검사 | `git diff --check` 최종 exit 0. Git CRLF 안내는 오류와 별도 |

회귀 필터는 `Logs/CBT-R2-08-P2B3D4E1CR1-TestFilter.txt`다. 기존 R1 필터에 신규 클래스 및 실제 Title/SaveLoad 씬 검사 3건을 추가한 선택 회귀이며, 저장소 전체 테스트/Player Build가 아니다. NUnit runner는 EditMode지만 `[UnityTest]` 중 `EnterPlayMode`를 사용하는 검사는 실제 자동 Play Mode 실행이다. 단순 EditMode 검사와 이를 구분한다.

### 요구 시나리오 A–F

| 시나리오 | 검증 결과 |
|---|---|
| A — 최초 New Game | PASS: Party/편성에 아난 1명, Level1/EXP0, 개인 보유·장착 key 각 2개, 공유 Film 0. 실제 후보 기반 첫 Snapshot은 Normal2/Clash3/MP2 |
| B — 동일 요청 중복 | PASS: 성공 요청의 즉시/진행 후/Exploration/Title 재방문 재전송, Reset 중 재진입 검사. 진행·장착 변경 보존, 중복 시 Changed 알림 0, 초기 장착 유지 상황 덱 2/3. 새 이벤트 구독 추가 없음 |
| C — 명시적 새 선택 | PASS: 이전 성장·장착 해제·추가 Party 멤버를 Reset하고 아난 Level1/EXP0/장착2개·덱2/3 재구성. 미처리 stale 요청 거부 |
| D — Save/Load | PASS: 초기 상태 실제 디스크 저장→새 Owner Continue 및 별도 Unity 프로세스 Load. ID/보유/장착/덱2/3 유지, Skill Changed는 Restore 1회만 발생 |
| E — 사용자 장착 변경 | PASS: Zornhau 해제, Level3/EXP50 저장→새 Owner 및 새 프로세스 Load. 보유2개/장착 Basic1개, 덱Normal2/Clash0 유지. 반복 Bootstrap도 덮어쓰지 않음. 사용자별 copies 편집·저장은 미지원 |
| F — 기존 Save | PASS: hero.test ID/성장/legacy.skill 장착 보존, 아난 스킬 소급 지급 없음. 기존 Legacy JSON Load 후 초기 스킬 없음/원본 파일 무변경. 다른 ID에 아난 스킬 자동 소유·장착/snapshot을 주입하지 않음 |

### 실제 Combat Snapshot의 범위

신규 테스트는 기존 AnandStart 후보에서 읽은 handCardRules와 실제 두 Skill SO를 쓴다. 일반 EditMode 통합 검사에서는 실제 EntryPoint/bridge/factory/session으로 생성한 Snapshot에서 Normal 카드 2개를 각각 1회 소비하고 재소비를 거부한다. Zornhau 카드 2개는 독립 인스턴스로 Discard되며 MP2→1→0을 확인한다. 남은 카드가 같이 소비되지 않고, 자연 MP가 소진된 추가 선언은 거부된다. fixture MP 추가 지급은 없다.

양측 완료 사이클 후 원래 5개 인스턴스로 재순환/Refill1회를 확인한다. 카드 runtime ID는 해당 세션 안에서 중복이 없고, 종료 후 같은 두 스킬 보유·장착은 유지된다. 두 번째 전투는 새 Session/Hand와 덱2/3을 생성한다. 세션 간 runtime ID의 전역 유일성을 요구하거나 새 persistent card ID를 도입하지 않았다.

자동 Play Mode에서는 기존 적 선공 정책의 실제 Flow pending response에 Zornhau 카드를 제출하고 MP2→1, 해당 카드 Discard, 같은 response 재제출 거부, Abort 정리, 진행 후 New Game 중복 콜백 무변경, 두 번째 전투 덱2/3을 확인했다. 이 새 검사 자체가 전체 3회 Clash 연출/피해/보상 완료를 검증했다는 뜻은 아니다. 기존 Hand/Clash/MP 및 아난 FirstClash·실제 Director/조우 회귀는 별도로 통과했다.

### 기존 실패 6건과 경고

이전 `CBT-R2-08-P2B3D4E0R1-Regression.xml`과 실패 test fullname 집합을 비교해 추가/제거 **0건**이다.

- DungeonOneProductionSceneTests의 다음 5건: `ProductionDungeonOne_HasCanonicalCombatRewardUiCameraAndBootstrapOwners`, `ProductionDungeonOne_HasExplicitInteractionOwnersWithoutLegacyInteractionPaths`, `ProductionDungeonOne_HasOneCanonicalRepeatableNpcInteraction`, `ProductionDungeonOne_HasQuestDerivedCompletionContractWithoutLegacyExitOrFallbackDestination`, `ProductionDungeonOne_IsCleanEnvironmentMigration`. 모두 기존 `CombatHandValidationOptIn on CombatRuntime` 자산 gate에서 실패한다.
- `ProductionSceneWiringTests.DungeonTemplate_IsCanonicalSingleOwnerProductionSource`: 기존 inherited BP gate 실패 유지.
- IGNORED 1건은 기존 `PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners`의 별도 실행 인자 조건이다. 이번 아난 별도 프로세스 검사 성공을 이 미실행 테스트의 PASS로 바꾸지 않는다.

최종 별도 프로세스 실행은 C# 오류 0이며, Runtime 재컴파일에서 기존 경고 4종을 확인했다: `FieldEnemy.OnBattleRequested` CS0067, `StoryInteractionController.useLegacyFallbackKey`/`fallbackInteractKey` CS0414, `RescueNpcActor.interactKey` CS0414. 수정하지 않은 기존 파일의 경고이며 새 코드 경고는 없다. 회귀에는 의도적으로 실패/거부 경로를 검사하는 로그도 포함되므로 로그 전체 Error/Warning 0이라고 보고하지 않는다.

### 수동 확인 절차와 미실행 범위

1. Production 원본을 저장하지 않는 별도 격리 씬/복제본에서 기존 Bootstrapper에 새 Start 후보를 연결한다. Title 진입 전에 설정이 준비돼야 한다. 기존 EntryPoint registry에 실제 Skill SO를 공급하고, 기존 덱 후보의 메모리 복제본만 활성화한다.
2. 실제 Title Start → 서류 승인으로 New Game. Party 1명/Level1/EXP0/보유·장착 key2개/MP2/Normal2/Clash3을 확인한다. 같은 승인 콜백 재전송에서 진행·장착·카드·씬 이동이 중복 처리되지 않는지 확인한다.
3. EXP 및 장착을 변경하고 Title로 돌아가 새 Start를 선택한다. 새 요청은 Level1/EXP0 및 장착2개·덱2/3을 재구성해야 한다.
4. Zornhau 미장착 상태를 저장하고 Continue한다. 기본 공격 장착만 유지되고 덱은 Normal2/Clash0이어야 한다. 보유 key2개는 유지한다.
5. 적 선공 실제 Contact에서 서로 다른 Zornhau 인스턴스의 소비·MP2→1→0·응답 불가·Discard/재순환을 관찰한다. Abort/Victory/Defeat 뒤 재조우 정책과 새 전투의 새 Hand를 확인한다.
6. 기존 hero.test 및 Legacy Save를 별도 테스트 경로에서 Load한다. 기존 ID/장착 보존과 아난 스킬 소급 지급 부재를 확인한다. Production Save에는 쓰지 않는다.

사람이 수행하는 수동 Play Mode/UI·시각 검수 및 실제 Player Build 검증은 실행하지 않았다. Production Cutover/새 후보의 실제 Scene Inspector 연결 검증은 요청 범위 밖이며 실행하지 않았다. 자동 fixture의 입력/표현 준비를 실제 플레이 조작 완료로 보고하지 않는다.

## Development tracking

- System: Core / Character Skill / Combat Hand Integration / SaveLoad.
- Feature: New Game 초기 보유·장착, 요청 재전송 보호, 초기 덱과 저장 복원.
- Production owner: SaveLoadService, TitleSceneController, RuntimeBootstrapper, CharacterSkillSaveParticipant/CharacterSkillRuntime; CombatEntryPoint의 기존 Snapshot/Hand 경계 유지.
- Requirement: 이번 CBT-R2-08-P2B3D-4E1C-R1 작업 요청. 외부 tracking 문서 수정 없음.
- Implementation: Implementation Complete (코드 및 비활성 후보 범위).
- Compile: Compile Verified — Unity 6000.3.23f1, 최종 오류 0 / 기존 경고 4종.
- Inspector/serialized: 기존 자산 해시/새 SO 참조/새 GUID 정적 검사 완료. 실제 Production Inspector 연결은 Unverified/미변경.
- Play Mode: 격리 자동 Play Mode Verified. 수동 Play Mode/Player Build는 Unverified.
- Integration: 격리 New Game→Skill→Snapshot 및 Save/Load/별도 프로세스 검증 완료. 새 후보의 Production 통합은 Unverified.
- Recommended tracking: **Implementation Complete — Isolated Integration Verified; Production Wiring/Cutover & Manual Verification Pending**.
- Remaining: Production 연결/Cutover와 수동 실제 플레이 검증은 별도 범위. 공개 unkeyed Reset은 명시적 Reset 호환 경로로 유지하며 요청 재전송 API로 취급하지 않는다.
- Stale mapping: D4E-1A의 초기 지급 부재는 이번 코드/후보 단계에서 해소된다. 당시 Production Debug/보호 자산 연결 차단은 그대로다. D4E-1C Partial은 역사 기록으로 보존한다.
