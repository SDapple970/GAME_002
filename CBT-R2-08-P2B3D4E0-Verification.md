# CBT-R2-08-P2B3D-4E0 검증 기록

## 결과와 범위

격리 씬 `Assets/GAME/Tests/PlayMode/AnandAngel_IsolatedPlaytest.unity`과 후보 `Assets/GAME/Tests/PlayMode/Enemy_Angel_Playtest_Candidate.prefab`을 Unity 6000.3.23f1 Editor로 생성했다. 원본 Dungeon_Template 복사본이며 Build Settings/Title에 등록하지 않았다. 실제 Production 통합은 미수행이다.

Prefab은 HP 14, 기존 loadout ID 10003, Enemy_Angel source, Rigidbody2D/Collider2D, 기존 motor/patrol, 기존 천사 sprite와 Animator controller를 사용한다. ContactTrigger는 Prefab에 있으나 EntryPoint와 EncounterGroup은 씬에서 연결해야 한다. 순찰 좌표는 Prefab이 아니라 씬의 `TestEncounter_01/TestOnly_leftPoint`, `TestOnly_rightPoint`에 있다. 씬의 두 번째 조우와 이전 EnemyRoot는 비활성이고 조우 ID는 `testonly.anand_angel`이다. Prefab은 씬 연결 전 비활성 후보이며 단독 배치하면 순찰점 등 연결이 필요하다.

씬의 RuntimeBootstrapper는 CharacterStart_AnandDeMorte, CombatEntryPoint registry는 실제 AnandBasic, Zornhau, AngelTheProphecy를 참조한다. PlayerRoot HP는 320이다. `AnandAngelPlaytestSetup`은 Editor 격리 전용이다. 비활성 `AnandHand_C2_TestOnly`를 메모리에서 복제해 초기화만 켜고, 아난 두 스킬을 테스트 런타임에 장착한다. 2/3 카드 장수는 테스트 fixture이며 Production 덱 승인이 아니다. Production 후보 SO와 기존 자산은 변경하지 않았다.

Production owner는 CombatEntryPoint, CombatSession/CombatStateMachine/CombatFlowOrchestrator, SkillRunner/기존 executor, GameFlowController, UIScreenRouter 및 기존 Input owner다. 새로운 전투 owner, 공개 API 또는 기존 SerializeField 변경은 없다. 새 prefab GUID `bf2104cf3ad121742b8bc9a3733159b4`, 씬 GUID `18d08c5264cbde046ad4ccb544875477`이다.

## 실제 자동 검증

- 최종 집중 검사 `Logs/CBT-R2-08-P2B3D4E0-FinalTests3.xml`: **2 PASS / 0 FAIL**. Editor 컴파일 및 EditMode 검사와 EnterPlayMode를 통한 격리 씬 실행이다.
- Prefab 구성, HP/source/loadout, Collider/Rigidbody, 누락 스크립트 없음, Prefab 순찰점 미포함을 검사했다.
- 플레이어 위치를 테스트 코드로 ContactTrigger에 이동시켜 실제 접촉으로 CombatEntryPoint를 시작했다. HP 320/14, 실제 적 스킬 ID 및 source key, 아난 MP2와 Hand, 기본 공격 및 이후 촌하우 비용을 검사했다.
- 실제 CombatDirector가 접근·합·결과를 진행해 Chain에 도달하고 피해가 발생했다. 테스트가 연출 완료 이벤트를 직접 호출하지 않았다. 애니메이션 이벤트와 타임아웃 중 어느 쪽이 각 연출을 완료했는지는 별도 판별하지 않았다.
- 강제 Abort 이후 CombatResult 1회, timeScale/fixedDeltaTime 복구, 기존 Reward 화면 닫기 버튼을 통한 Exploration 복귀 및 FieldLock 해제를 검사했다. **복귀 직전과 직후 테스트가 접촉 Collider를 일시 비활성화했다**. 이 결과는 겹친 접촉의 재조우 방지 검증이 아니다.
- 선택 회귀 `Logs/CBT-R2-08-P2B3D4E0-Regression.xml`: **874 PASS / 6 FAIL / 1 IGNORED**, 총 881. 이는 최종 격리 Prefab/씬 정리 전에 실행했으며 기존 runtime 코드는 이후 변경하지 않았다. D4D 선택 회귀와 수치 및 실패 이름이 같다. 이름에 LegacyPlanning이 들어간 3건은 통과했다. 전체 회귀 아님.
- 기존 2,635개 tracked/untracked 파일의 작업 전후 SHA-256이 일치한다. `git diff --check`는 오류 없이 기존 TimeManager 줄바꿈 안내를 출력했다.

### 유지된 실패 6건

`Game.Tests.Integration.DungeonOneProductionSceneTests`의 다음 다섯 건은 기존 Production Hand Debug 선행 gate, **Asset Change Required**다.

1. `ProductionDungeonOne_HasCanonicalCombatRewardUiCameraAndBootstrapOwners`
2. `ProductionDungeonOne_HasExplicitInteractionOwnersWithoutLegacyInteractionPaths`
3. `ProductionDungeonOne_HasOneCanonicalRepeatableNpcInteraction`
4. `ProductionDungeonOne_HasQuestDerivedCompletionContractWithoutLegacyExitOrFallbackDestination`
5. `ProductionDungeonOne_IsCleanEnvironmentMigration`

`Game.Tests.Integration.ProductionSceneWiringTests.DungeonTemplate_IsCanonicalSingleOwnerProductionSource`는 Template → CombatRuntime Prefab의 기존 BP Debug gate, **Asset Change Required**다. 무시 1건은 `Game.Tests.Integration.PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners`이며 디스크 재시작은 검증하지 않았다.

## 남은 검증과 위험

실제 사용자 입력 이동/순찰/지면 충돌, FieldAttack, 양측 선공, 승리/패배 Terminal, 실제 애니메이션 이벤트와 표현, UI 클릭/표시, 다른 조우 재사용, 자연 상태의 조우 중복 방지, Quest/Reward/EXP 중복, 디스크 재시작 및 수동 Play Mode는 이번에 검증하지 않았다. 초기 격리 구성의 Abort 복귀 실험에서는 접촉 중첩으로 즉시 재조우가 발생했다. 이후 조우 그룹 연결을 수정했으나 자연 중첩 복귀는 재검증하지 않았다. 결과 패널의 한글 글리프 경고도 로그에 나타났다.

회피 20%, 방어력 0, 캐릭터 속도 9는 실제 런타임 적용 경로 미확인으로 Feature Gap이다. 스킬 속도와 혼동하지 않았다. 정식 덱 장수, 초기 스킬 지급, Film mapping, 표현 세부 설정은 Pending이다. 05/06A Production 수동 검증, Debug 의존 제거와 Cutover도 보류다.

## 수동 확인 순서

Unity Editor에서 새 격리 씬을 단독으로 열고 기존 저장 상태와 분리된 테스트 환경에서 Play한다. 아난과 천사의 이동·순찰·지면 접촉을 확인한 뒤 Contact로 진입하고, 기본 공격 및 촌하우, 천사 기술, HP/MP, 합과 Chain, 승리·패배·중단을 확인한다. 결과 화면을 닫은 후 필드 입력/카메라/UI/lock 및 timeScale 복귀와 접촉 중첩 재조우 여부를 확인한다. 별도로 FieldAttack 진입과 두 번째 조우를 검증한다. Player Build와 Production 씬 테스트로 간주하지 않는다.

## Production 후속 연결 대상

별도 승인·데이터 준비 후 `Assets/GAME/Scenes/Dungeon_1_Production.unity`의 실제 RuntimeBootstrapper, Player/Enemy 및 조우 인스턴스와 `Assets/GAME/Prefabs/CombatRuntime.prefab`의 EntryPoint/config 참조를 실제 Inspector 기준으로 확인해야 한다. 정확한 변경 구성은 당시 prefab override와 승인된 데이터에 따라 확정한다. 아난 Start, 승인된 Production config/덱, 세 스킬 Registry, 천사 source/HP/loadout, 순찰점 및 그룹·Contact/FieldAttack, HUD/Input/Reward를 연결하고 Inspector/Play Mode를 검증한다. TestOnly helper와 fixture를 Production에 등록하지 않는다.

## 상태

루트 AGENTS.md 적용, 중첩 지침 없음. branch `main`, HEAD `b42544824838e7de0e70b71afc2ca21a59db8192`. 기존 사용자 변경과 기존 GUID 보존, commit/push 없음. 구현: 격리 후보 및 자동 fixture 작성. 검증: 격리 자동 Play Mode 일부 통과, 수동 Inspector/Production Play Mode/통합 미검증. 개발현황 권장: **Isolated Playtest Partial Validation — Production Wiring / Manual Validation Pending**.
