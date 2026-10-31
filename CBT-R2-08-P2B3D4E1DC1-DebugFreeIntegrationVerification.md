# CBT-R2-08-P2B3D-4E1D-C1 — Debug 없는 격리 Production 통합 검증

작업일: 2026-10-31. 대상 Unity: 6000.3.23f1. 이번 승인은 테스트·격리 fixture·Editor 검증·보고서에만 적용했다. Production Cutover 및 보호 자산 수정은 수행하지 않았다.

## 결과 및 상태

**최종 1,050건: 1,043 PASS / 기존 6 FAIL / 1 IGNORED.** D4E1DB의 기존1,041건은 테스트별 결과 변화0건이며, 추가 실행한9건(신규 격리8 + 기존 Film/Unique Scene 시나리오1)은 모두 PASS다. C# 컴파일 오류0/경고0. 실제 Cold Writer/Reader도 서로 다른 Unity 프로세스에서 exit0이다.

| 최종 재실행 범위 | 결과 |
|---|---|
| DebugFreeAnandAngelIntegrationTests | 8/8 PASS |
| FilmUniqueProductionPlayModeTests / FilmUniqueProductionIntegrationTests | 1/1, 9/9 PASS |
| AnandNewGameSaveIntegrationTests | 15/15 PASS |
| WorldEncounterIntegrationTests | 111/111 PASS |
| CombatUIRoutingTests / ProductionUIAssets_PassExplicitRoutingValidation | 58/58, 1/1 PASS |
| CombatBpValidationTests / CombatHandRuntimeTests | 15/15, 9/9 PASS |
| CombatClashSeriesCoreTests / CombatClashSeriesIntegrationTests | 11/11, 26/26 PASS |
| SaveLoadPreparationTests / SaveLoadSceneIntegrationTests | 42/42, 1/1 PASS |
| 기존 여섯 Production/Template Asset gate | 6 FAIL 유지, 새 실패0 |

실행 플랫폼은 Editor EditMode runner이며, UnityTest가 실제 EnterPlayMode/ExitPlayMode를 수행한다. 별도 PlayMode assembly를 실행했다고 표시하지 않는다. 외부 증거 위치: `C:/Users/eunio/.codex/visualizations/2026/10/31/01a190ef-97ac-70c3-86be-879fdbc0b8b9/D4E1DC1-evidence/`의 `Regression.xml`, `Regression.log`, `Filter.txt`, `RegressionSummary.json`, `ColdWriter.log`, `ColdReader.log`, `Preservation.json`.

기존 Ignore는 `PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners`다. 기존 이유는 `Run in a new Unity Editor process with -party05vRestartStage read after the write run.`이며 이번에 Ignore를 추가하지 않았다. 아난 Cold Load의 별도 프로세스 검증은 이 Ignore와 별개로 실제 수행했다.

C# 경고0은 Console 전체 경고0이라는 뜻이 아니다. 광범위 회귀에는 missing-player/skill/root/lock negative fixture 및 active-context teardown, 중복 singleton 정리 경고가 존재한다. 실제 Scene Film 시나리오의 runtime-error 검사도 PASS다. `git diff --check`는 exit0이며 Git의 LF→CRLF 안내는 파일 변경으로 처리하지 않았다.

개발 중 초기 namespace 컴파일 오류와 새 테스트의 Start 최초 설정 순서/빈 Film snapshot/Combat 상태 alias/Play Mode domain reload를 넘는 캡처 변수 문제를 수정했다. 마지막 소스 상태에서 위 전체 회귀를 재실행해 새로운 실패가 없음을 확인했다. 실제 Runtime guard를 완화하거나 실패 로그를 무시하지 않았다.

C1의 테스트 구현과 격리 검증을 실제 Production Debug 제거 승인과 구분한다. 원본 Production의 여섯 Asset Change Required 실패를 Assertion 삭제/완화, Ignore, 예외 삼키기로 숨기지 않았다.

## 읽은 지침, Git 및 입력 자료

- 적용 지침: 루트 `AGENTS.md` 전체. 중첩 `AGENTS.md` / `AGENTS.override.md` 없음. 사용자가 전달한 저장소 지침도 적용했다.
- 시작 branch `main`, HEAD `b42544824838e7de0e70b71afc2ca21a59db8192`.
- 시작 tracked 수정11개: Production Scene, CombatEncounterGroup/Trigger, RuntimeBootstrapper, SaveLoadService, CharacterSkillSaveParticipant/StartDefinition, TitleSceneController, CombatProductionCutoverRehearsalTests, SaveLoadPreparationTests, WorldEncounterIntegrationTests. 기존 미추적 실제 데이터·테스트 씬·보고서도 보존 대상으로 잡았다.
- 필수 참조: D4E-1D-A 감사, D4E-1D-B 완료 보고/Regression.xml, `CBT-R2-08-P2B3D4E1C0-ProductionDebugRemovalPlan.md`, D4E-1C-R1 구현/테스트와 현재 Scene/Prefab/SO/호출자.
- 편집 전에 파일 범위, 최초 설정/직렬화 위험, 실제 Director와 수동 완료 구분, 회귀 및 해시 대조 계획을 보고했다.
- 이번 작업용 baseline은 기존 파일2,659개의 SHA-256이다. 최종 대조는 외부 `D4E1DC1-evidence/Preservation.json`에 기록한다.

## 실제 변경 파일

수정1개:

- `Assets/GAME/Tests/Editor/FilmUniqueProductionPlayModeTests.cs`: Hand Debug 필수 참조를 제거하고, 실행 중인 Scene 복제본에서 BP/Hand/Clash를 제거한 뒤 메모리 Config/Registry로 동일 Film/Unique 계약을 검증한다. 기존 획득·장착·UI·키보드 입력·Quest·보상·디스크 저장복원 검사를 유지했다.

추가:

- `Assets/GAME/Tests/Editor/DebugFreeProductionTestSupport.cs` 및 Unity 생성 `.meta`: 정확한 Debug 세 타입만 제거하는 테스트 도구, Missing Script/Reference 검사, 비활성 후보 Config의 메모리 복제/활성화 도구.
- `Assets/GAME/Tests/Editor/DebugFreeAnandAngelIntegrationTests.cs` 및 Unity 생성 `.meta`: 구조3건, legacy 차단 조건1건, 실제 EnterPlayMode 전투4건, 합계8건.
- `Assets/GAME/Tests/PlayMode/AnandAngel_DebugFree_Integration.unity` 및 Unity 생성 `.meta`: 기존 사용자 테스트 씬을 Editor Save-as-copy로 복제했다. 상속 BP 제거 override와 초기 지급 Setup 제거를 이 복사본에만 저장했다. 테스트가 최초 Character 설정을 제공하도록 이 테스트 씬의 Bootstrapper Start 참조는 null이다.
- 본 보고서.

삭제/이동한 기존 파일 없음. Production Runtime, 공개 API, SerializeField 이름/형식, Save Schema, 기존 GUID/.meta 변경 없음. 검증용 임시 Scene copy는 고유 경로로 만들고 finally에서 해당 새 Asset만 삭제한다.

## 격리 구성과 소유권

- Production/Template 구조 검사는 원본을 열어 별도 테스트 Scene으로 Save-as-copy한 뒤 그 복제본에서만 제거한다. 복제본의 제거 상태는 저장하지 않고 폐기한다. Prefab 연결과 상속 BP 제거 override, Camera/Director/Flow 보존, 모든 Component의 Missing Script/직렬화 Object Reference를 검사한다.
- Production 복제본에서 제거하는 대상: `Runtime/Combat/CombatRuntime`의 상속 `CombatBpValidationOptIn`(source fileID `3049129679861795314`), Scene Added `CombatHandValidationOptIn`(`1665839071176935759`), `CombatClashSeriesValidationOptIn`(`1665839071176935761`). 이미 제거된 Added BP `1665839071176935766`를 다시 대상으로 잡지 않았다.
- Template 복제본: 같은 GO의 상속 BP1개만 제거한다. Camera 등은 유지한다.
- 저장된 Anand 테스트 복제본은 Template 계열이라 BP1개가 대상이다. Hand/Clash Debug가 원래 없는 이 복제본에서도 정식 Hand/BP/Clash Runtime을 초기화해 실행한다.
- 실제 `CombatEntryPoint → CombatSession/CombatFlowOrchestrator/CombatStateMachine → CombatDirector`를 사용한다. 기존 GameFlowController, UIScreenRouter, Input Installer, WorldLifecycle, FieldLock, Camera, Reward Binder를 유지한다. EntryPoint/Flow/Router/Input Owner의 단일성을 검사한다.
- 신규 Runtime/Debug Manager 없음. Debug 컴포넌트를 Production Owner로 승격하지 않았다. Source `CombatRuntime.prefab`은 BP1개를 계속 보유한다. Apply/Revert All 없음.

## 실제 콘텐츠 및 초기화

- `CharacterStart_AnandDeMorte.asset`를 메모리 복제하고, 이 원본이 참조하는 `CP_AnandDeMorte.asset`와 `char.anand_de_morte`를 그대로 사용한다.
- 메모리 Start의 `initialSkillGrants`에 실제 `Skill_AnandBasic_PlaytestV1.asset`, `Skill_Zornhau_PlaytestV1.asset`를 연결한다. 직접 Runtime.TryAcquire로 아난 초기 지급을 대체하지 않는다. 기존 Bootstrapper 설정과 SaveLoadService의 NewGameRequest를 통해 최초 지급/장착한다.
- 이미 다른 Start를 채택한 소유자의 설정 교체는 현재 Runtime 계약상 거부된다. 따라서 테스트 Scene의 최초 설정 전에 메모리 Start를 제공한다. 이는 Production에서 부팅 때부터 동일한 승인 Start를 공급해야 한다는 준비 조건이다.
- 실제 `FinalCombatRuntimeConfig_AnandStart_PlaytestV1_Candidate.asset`를 메모리 복제해 `enableProductionHandBp=true`로 한다. 원본 후보는 계속 false다. 스킬 Registry의 실제 세 SO(기본/촌하우/천사 예언)를 기존 테스트 씬의 Registry로 사용하며 Production Registry에는 등록하지 않는다.
- New Game 아난1명/레벨1/EXP0, 보유·장착2개, Normal2/Clash3, MP2, BP0을 검사한다. 테스트 플레이어 HP320/천사 HP14는 기존 격리 field fixture 값이다. CP에 새 HP 규칙을 작성한 것이 아니다.
- 적은 `Enemy_Angel.asset`, source key `enemy.chapter1.angel`, 실제 `Skill_AngelTheProphecy_PlaytestV1.asset`를 사용한다. 후보 Config의 Enemy Hand opt-in은 빈 배열로 유지한다.

## Combat / Director / Result 증거 구분

1. **실제 Director:** 적 선공 Contact에서 The Prophecy(10003) 선언, 촌하우(10002) 응답, MP2→1, 정확한 카드 Discard 및 같은 토큰 재사용 거부, Director 교환 완료/Chain 진입을 검사한다. 같은 프레임에 AI가 ChainDecision을 떠날 수 있어 상태 전환 이벤트를 관찰한다. 완료 callback을 테스트가 호출하지 않는다.
2. **실제 Director:** 기본 공격(10001), 촌하우, Chain 교환, MP/Normal·Clash Discard, 적 HP 감소 후 명시적 Abort. 실제 Abort 종료1회, Reward 닫기, 즉시 재조우 방지와 영역 이탈 후 새 Contact 허용을 검사한다. 이 별도 Abort 시나리오의 첫 Contact는 테스트 Scene의 기존 아군 선공 fixture다.
3. **실제 Director 자연 종료:** Field Attack 아군 선공 → 실제 스킬/AI 반복 → Victory. Enemy Contact 적 선공 → Defeat. Defeat는 종료를 빨리 재현하기 위해 세션 안에서 플레이어 HP를1로 만드는 endpoint fixture이며 보호 SO/Field 원본을 바꾸지 않는다. 테스트가 Victory/Defeat 완료 신호를 만들지 않는다.
4. Victory/Defeat 결과1회, 실제 Reward UI, Reward ledger, Quest Objective Publisher/Runtime을 확인한다. Quest 정의는 메모리 test-only 목표이며, 기존 실제 CH01 Quest 콘텐츠 연동은 Film 시나리오가 별도로 검증한다. Reward 닫기 후 Exploration/입력 허용/FieldLock 해제/Camera 모드 해제/TimeScale 및 fixedDeltaTime 복구를 검사한다.
5. **계산 probe:** BP는 정식 Session Runtime이 존재하고 초기0이며 아난 실제 스킬에 임의 enhancement가 없음을 검사한다. 별도로 성공한 양측 공격 cycle을 직접 기록해 두 round cycle의 BP 증가를 검사한다. 이 계산 probe를 Director가 자연스럽게 두 cycle을 완료한 증거로 보고하지 않는다. 실제 BP 투자/Ultimate 콘텐츠는 이 후보에 없다.
6. 기존 `AngelProphecyPlaytestTests.EnemyFirst_ExistingAiUsesProphecy_AnandRespondsAndAbortRestores`도 PASS다. 실제 Prophecy/Zornhau의 계산 결과 AttackerPower2/ResponderPower3, 내구도3→2/3을 검사하지만, 이 테스트는 presentation.TryComplete를 수동 호출하므로 위 실제 Director 경로의 증거와 구분한다.
7. 기존 `AnandNewGameSaveIntegrationTests`의 실제 스킬 카드 소비·MP 소진·동일 카드 중복 거부·Discard 재순환·새 전투 초기 덱 복원을 재실행한다. 이는 정책/Runtime 경계 검사이며 전체 Scene 애니메이션 증거와 구분한다.

## New Game / Save / Load

- 기존15건을 그대로 유지하고 재실행한다: 최초 지급, 같은 요청 중복 방지, 명시적 재시작, 초기 장착 저장, 촌하우 해제/EXP 변경 저장, Continue/Load 무지급, legacy hero.test ID/장착/레벨/EXP 보존, 이전 Schema 마이그레이션, 스킬·카드 Runtime 경계 및 실제 Play Mode 응답.
- 별도 프로세스 검증은 기존 `AnandNewGameSaveIntegrationTests.WriteProcessRestartEvidence`와 `ReadProcessRestartEvidence`를 서로 다른 Unity 실행으로 호출한다. 디스크 파일은 테스트용 격리 경로와 ignored Logs만 사용한다. 초기 저장2/3 덱, 변경 저장2/0 덱/레벨3·EXP50, 한 번의 복원 알림, 재지급 없음, PID 차이를 검사한다.
- **명확한 잔여 차단:** `LegacyHeroRestore_PreservesIdentity_ButAnandOnlyStartBlocksCombatWithoutMapping`는 `hero.test` 데이터 보존 성공 후 `CharacterPartyCombatAdapter`의 실제 거부를 재현한다: `Character 'hero.test' has no authored definition or progression state.` 아난 전용 Start에 hero 정의가 없다. 저장 호환 성공을 legacy save의 실제 Production 전투 호환 성공으로 확대하지 않는다. Runtime을 고치거나 hero ID를 아난으로 변환하지 않았다.

실행 증거: Writer PID **9640**, Reader PID **38780**, 각각 exit0. Reader는 디스크 writer PID와 자신의 PID가 다름을 Assertion으로 검사했고 `PASS: distinct Unity writer/reader processes; initial 2/3 deck and changed 2/0 deck; schema14; one restore notification; no initial grant replay.`를 기록했다. 기존 R1 이름을 쓰는 검증 메서드를 이번에 새로 실행한 결과이며, 이번 증거 폴더에 두 JSON/PID/결과 텍스트를 복사해 보관했다. Cold Load의 전투 확인은 절차적으로 구성한 debug-free 실제 스킬/Config Session이며, 새 격리 Scene 전체를 별도 프로세스에서 자동 재진입한 검증은 아니다.

## Film / Unique 계약과 테스트 의존성 이전

- 실제 Production Film/Unique 콘텐츠가 완성됐다고 보고하지 않는다. 기존 transient Film3개/Unique6개/Character2개를 사용한 계약 검사다. 공식 SO를 새로 발행하거나 Production Registry에 연결하지 않았다.
- 이전 `FilmUniqueProductionPlayModeTests.ConfigureSceneInstances`는 Hand Debug 존재를 Assert하고 card source/capacity를 바꿨다. 이를 BP/Hand/Clash가 없는 Play Mode 복제본 + 정식 Runtime Config/명시적 규칙으로 이전했다. 기존 호환 skill1/2의 Clash 분류, snapshot skill당 카드1장, Film fixture의 Normal capacity3을 명시한다. 아난 Normal2/Clash3 정책에는 영향 없다.
- 최초 Film 획득 전에도 정식 초기화는 장착 snapshot을 요구한다. test-only HeroA에 기존 Compatibility 기본 스킬을 보유·장착시켰다. Film 획득 후 기존 starter만 장착돼 있음을 추가 검사하고, 명시적으로 해제한 뒤 원래의 빈 장착 Assertion을 유지한다. Film이 자동 장착되지 않는 계약을 검사한다.
- 실제 Contact/첫 Film 획득, shared/non-consumable 경계, Character 소유 경계, Unique level/owner 경계, 실제 스킬 UI/장착, Input System J → Field Attack → Film 실행, CH01 Quest/보상, Title 경유 디스크 restore를 검사한다.
- 첫 Film 획득은 기존 테스트의 Editor ForceFinishCombat + 세션 적 HP fixture를 그대로 사용한다. 두 번째 Film 공격 승리는 실제 실행/Director 경로다. 해당 시나리오의 Title/Load는 같은 Unity 프로세스의 scene reload이며, 위 아난의 별도 Unity 프로세스 검증과 구분한다.
- Debug 단위 테스트/Prefab source Debug 존재 검사/기존 original-scene footprint는 유지했다. 여섯 Asset gate의 조건을 바꾸지 않았다.

## 기존 실패6건 전후 대조

전체 이름의 공통 접두사: 첫5건 `Game.Tests.Integration.DungeonOneProductionSceneTests.`

| 테스트 | D4E1DB 이전 증거 | C1 최종 | 직접 원인 / 후속 최소 조치 |
|---|---|---|---|
| ProductionDungeonOne_HasCanonicalCombatRewardUiCameraAndBootstrapOwners | FAIL | FAIL 유지 | ValidateProductionScene가 상속 BP를 Forbidden Debug로 검출. UI/Camera 자체 오류라고 해석하지 않음 |
| ProductionDungeonOne_HasExplicitInteractionOwnersWithoutLegacyInteractionPaths | FAIL | FAIL 유지 | 같은 BP 금지 예외가 뒤 Interaction Owner 검사보다 먼저 발생 |
| ProductionDungeonOne_HasOneCanonicalRepeatableNpcInteraction | FAIL | FAIL 유지 | 같은 BP 금지 예외. NPC/Interaction SO 수정 근거 아님 |
| ProductionDungeonOne_HasQuestDerivedCompletionContractWithoutLegacyExitOrFallbackDestination | FAIL | FAIL 유지 | 같은 BP 금지 예외. Quest/exit 검사는 미도달 |
| ProductionDungeonOne_IsCleanEnvironmentMigration | FAIL | FAIL 유지 | 같은 BP 금지 예외. BP뿐 아니라 남은 Hand/Clash도 최종 clean 조건 대상 |
| Game.Tests.Integration.ProductionSceneWiringTests.DungeonTemplate_IsCanonicalSingleOwnerProductionSource | FAIL | FAIL 유지 | Any Debugging/Legacy가 true. Template 상속 BP1개 제거 override 필요 |

첫5 Assertion은 `Assert.DoesNotThrow(ValidateProductionScene)`이며, 현재 실제 메시지는 `Forbidden Demo/Debug/Test/Legacy component remained: Game.Combat.Debugging.CombatBpValidationOptIn on CombatRuntime.` 여섯째는 expected false / actual true. 보호 자산을 유지하는 C1에서 이 여섯 조건을 PASS로 바꾸지 않는다. 원본 자산 gate와 격리 복제본 기능 검증은 별개다.

## 직렬화 / GUID 및 보존

기존 **2,659개 중2,658개 SHA-256 동일**, 허용한 `FilmUniqueProductionPlayModeTests.cs`만 변경, 누락0개. 기존 tracked 수정11개 및 모든 기존 미추적 파일이 그대로다. Production/Template Scene, CombatRuntime.prefab, 기존 SO, 전체 기존 .meta/GUID 및 ProjectSettings도 baseline과 일치한다. 시작/종료 branch와 HEAD도 동일하다. commit/push/PR/branch 생성 없음.

새 테스트 씬 GUID는 Unity가 생성한 `15126e5e5f6935843b527e2dacbab532`다. 기존 source Prefab GUID `1bfc52cd235e809449d17d48571f5a38` 및 모든 기존 참조 ID는 보존했다. 새 파일은 위 C#2개/.meta2개/Scene1개/.meta1개/보고서1개, 총7개다.

새 테스트 Scene은 source Prefab BP를 삭제하지 않고 해당 instance에 Removed Component override를 기록한다. 새 .meta/GUID는 Unity가 생성했다. 상속 Camera/EntryPoint/Flow/Director/WorldLifecycle 및 기타 source field를 재연결하지 않았다. Debug 제거 후 dangling Object Reference 및 Missing Script 검사를 자동화했다. null인 선택적 Inspector 필드는 Missing Reference로 오인하지 않고 Unity의 unresolved reference instance ID를 검사한다.

## C2 승인에 필요한 파일·컴포넌트와 차단 조건

| 구분 | 파일 / 정확한 Inspector 대상 | 승인 필요 범위 |
|---|---|---|
| 제거 | `Assets/GAME/Scenes/Dungeon_1_Production.unity`, `Runtime/Combat/CombatRuntime` | Added Hand fileID1665839071176935759, Added Clash1665839071176935761 제거 + 상속 BP source3049129679861795314 제거 override. Added Camera1665839071176935757 및 core owners 보존 |
| 제거 | `Assets/GAME/Scenes/Dungeon_Template.unity`, 같은 GO | 상속 BP source3049129679861795314 제거 override. source Prefab 변경 불필요 |
| 검증 계약 갱신 | `Assets/GAME/Tests/Editor/CombatProductionCutoverRehearsalTests.cs`, SourceAssets_ReadOnlyFootprint... | 실제 두 Scene 제거가 승인·수행될 때만 기존 Debug Added/상속 존재 검사를 clean instance0/source BP1/보존된 나머지 참조로 갱신. 다른 Assertion 보존 |
| 데이터 준비 | `FinalCombatRuntimeConfig_AnandStart_PlaytestV1_Candidate.asset` 또는 별도로 승인한 정식 Config, EntryPoint.finalExchangeRuntimeConfig/skillDefinitions | 정식 Hand/BP flag,2/3 카드 규칙,MP2/BP/Clash 정책, 실제 세 Skill Registry, legacy 규칙을 확정. 후보 원본의 활성화 및 실제 Production 연결은 C1에서 미실행 |
| 시작 설정 | 승인된 CharacterStart, Title `RuntimeBootstrapper.characterStartDefinition` 및 Dungeon `Runtime` Bootstrapper 같은 필드 | 동일 Start를 첫 bootstrap부터 공급하고 초기 지급2개를 작성/연결할 권한. 기본 Start는 현재 지급 배열이 비어 있고, 이미 다른 Start를 채택한 소유자의 런타임 교체는 거부됨 |
| 호환 차단 | hero.test Character 정의/legacy.skill 등 저장 키 Registry·HandRule | ID/Save Schema를 보존하는 승인된 legacy mapping 필요. 테스트용 hero 정의를 임의 Production으로 승격하지 않음 |
| 콘텐츠 정책 | BP enhancement/Ultimate/Enemy Hand, 실제 Film/Unique | 미제작/미연결 항목은 계속 보류하거나 명시적 별도 승인. 테스트 fixture가 Production 콘텐츠를 대신하지 않음 |

`CombatRuntime.prefab` 원본 수정은 필요하지 않다는 격리 증거를 확보했다. 원본을 공유하는 `Testing_Dungeon_Template` 및 기존 `AnandAngel_IsolatedPlaytest`도 그대로 남겨 Debug/Demo 격리 용도를 보존한다. **현재 Production Config를 그대로 둔 채 제거만 하는 기능 전환은 준비 완료가 아니다.** 데이터/legacy 정책과 실제 활성화 권한을 먼저 확정해야 한다. C2는 이 조건과 두 Scene 수정에 대한 별도 승인이 충족될 때 실행 가능하다.

## 미실행 및 수동 절차

- 사람의 Unity Inspector/육안 Play Mode, 렌더된 UI 화면/애니메이션·카메라 구도, Player Build/실기기 검증은 미실행. Headless EnterPlayMode는 Scene Runtime 실행 증거이며 시각 QA는 아니다.
- 실제 보호 Production에서 Debug를 제거한 뒤 저장/재로드/정식 bootstrap 활성화/legacy 전투는 미실행(이번 금지). 격리 환경의 결과를 Production Cutover 완료로 기록하지 않는다.
- 실제 authored Film/Unique Production 콘텐츠 Play Mode, 아난 BP 투자/Ultimate, 실제 Enemy Hand 콘텐츠는 미검증/정책 보류. 임의 SO 작성으로 통과시키지 않았다.
- 수동 확인 순서: 새 `AnandAngel_DebugFree_Integration` 테스트 씬 및 Test Runner의 C1 클래스 실행 → runtime CombatRuntime에서 Debug0·core owners 확인 → Contact/Field Attack의 HP/MP/BP/Hand 표시와 Director 종료 확인 → Reward 닫기·카메라/입력 복구 → Abort 뒤 머무름/이탈/재접촉 확인. 테스트 밖에서 이 Scene을 직접 Play하면 test Setup이 없으므로 승인한 메모리 New Game/Config를 자동 공급하지 않는다. Test Runner 실행이 이 fixture의 정식 진입 방법이다.

## Development tracking

- System: Combat / Character Startup / Save / Film-Unique / Encounter / UI.
- Requirement: CBT-R2-08-P2B3D-4E1D-C1.
- Production owners: 기존 CombatEntryPoint/Session/Flow/Director, RuntimeBootstrapper/GameFlowController/SaveLoadService, CharacterSkillSaveParticipant, UIScreenRouter/Input Owner 유지.
- Implementation: Implementation Complete (C1 테스트/fixture/보고서). 실제 Production Debug 제거는 Not Implemented (이번 범위 금지).
- Compile / Serialized asset / Play Mode / Integration: Compile Verified / Editor API 직렬화 검사 Verified / 자동 EnterPlayMode Verified / 격리 Integration Verified. 사람의 Inspector·시각 QA 및 실제 Production 전환 검증은 미실행.
- Recommended tracking: C1 격리 검증 완료 여부와 실제 Production Debug 제거·기능 전환을 별도 상태로 관리. System Complete/Cutover Complete 아님.
- Remaining: 동일 Start 부팅 연결, legacy 정의/스킬/HandRule 승인, 실제 Config/Registry 활성화 승인, C2 Scene override/footprint 편집, 시각 QA/Player Build.
- Stale mapping: “Hand Debug 금지5건”의 현재 첫 검출은 상속 BP다. D4E1DB의1,041건에는 FilmUniqueProductionPlayModeTests가 없었으므로 이번에 실제 실행 범위에 추가했다.