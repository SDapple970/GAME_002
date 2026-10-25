# GAME\_002 반복 일반임무 4종 — 배치 전 제작 가이드

2026-10-17. 이번 Batch는 기능, 미배치 데이터/Prefab, 자동 검증을 제공한다. 기존 Scene과 Prefab은 수정하지 않았다. 실제 맵 배치·참조 연결·실제 맵 Play Mode는 후속 작업이다.

## 확정 요구와 제공 기능

| 임무 | 판정 | 제작 데이터 | World 연결 |
| :---- | :---- | :---- | :---- |
| 탐사 | 지정 동일 면적 셀 90% 이상 방문, 올림 적용 | 전체 셀의 고정 ID 목록 | `GeneralMissionAreaTrigger` / SurveyCell |
| 소탕 | 지정 조우 전부 클리어 | 조우의 고정 ID 목록 | `GeneralMissionCombatEncounterAdapter` \+ 기존 Group/Trigger/CombatEntryPoint |
| 억제 장치 | 후보 중 선정된 서로 다른 3개 가동 | 후보 지점의 고정 ID 목록, 최소 3개 | `GeneralMissionSuppressor` |
| 구출 | 상호작용 후 동행 → 지정 출구에 함께 도착, HP 80% 이상 | 대상 ID, 출구 ID, 최대 HP | `GeneralMissionEscortActor` \+ RescueExit |

&nbsp;

모두 Repeatable=true, 소요 1일, 실패 시 RestartFromBeginning이다. 새 수락은 새 RunId, 실패 재도전은 같은 RunId와 증가한 Attempt를 사용한다. 일반임무 1개만 동시에 진행한다. 미정산 완료 결과가 있으면 재수락을 막는다.

## Production Owner와 호환

- 정의는 `QuestDefinitionSO.GeneralMission`; 상태와 판정·저장은 기존 `QuestRuntime`의 partial 구현 `QuestRuntime.GeneralMissions.cs`가 소유한다. 새로운 Quest Manager/Runtime을 만들지 않았다. SO에는 제작 데이터만 있다.  
- 일반 이벤트, `CompleteObjective`, `CompleteQuest`는 일반임무 조건을 우회하지 못한다. `QuestRuntime`의 임무별 scoped API가 대상/RunId/Attempt/중복을 검증한 후 기존 Objective progress·Completion 경로를 사용한다. 기존 일반 Quest의 이벤트 경로는 유지한다.  
- 보상은 `QuestCompletionFlow → RewardService`, 날짜는 `QuestCalendarIntegration → CalendarService`, 정산은 기존 DaySettlementFlow를 사용한다.  
- 전투는 기존 `CombatEncounterGroup/CombatEncounterTrigger2D → CombatEntryPoint`를 사용한다. 새 전투 시작 경로는 없다. Legacy `FieldEnemy`/DemoMission을 새 임무 진입점으로 사용하지 않는다.  
- 입력은 기존 `InteractionController`가 IInteractable 등록도 수용한다. 기존 InteractableObject용 공개 오버로드와 직렬화 필드는 유지한다. 새 컴포넌트는 직접 키를 읽지 않는다.  
- UI는 `GeneralMissionBoard`와 `GeneralMissionBoardRow`. 기존 Canvas/UIScreenRouter/GameUIRootController 아래에 연결한다. 새 UI가 전체 상태나 패널 표시를 결정하지 않는다. 별도 INonCombatInteractable 입력기와 기존 Production 입력기를 같은 플레이어에 중복 설치하지 않는다.

## 생성된 미배치 리소스

`Assets/GAME/Data/GeneralMissions/`

- CH01\_General\_Explore.asset: survey\_001 \~ survey\_100, 필요 90셀.  
- CH01\_General\_Clear.asset: encounter\_01 \~ encounter\_03, 필요 3개 전체. 이 수량은 제작 템플릿이며 실제 맵의 지정 조우 목록과 맞춰 조정한다.  
- CH01\_General\_Suppress.asset: suppressor\_01 \~ suppressor\_06 후보 중 3개 선정.  
- CH01\_General\_Rescue.asset: rescue\_target / rescue\_exit, 최대 HP 100\.  
- SurveyCell.prefab, Suppressor.prefab, RescueEscort.prefab, RescueExit.prefab, EscortHazard.prefab.  
- GeneralMissionBoard.prefab: 4종 수락 버튼과 진행 텍스트. 기존 DungGeunMo 폰트 사용. Canvas와 EventSystem은 기존 UI 소유자가 제공한다.

Editor 메뉴 `GAME > General Missions > Create Unplaced Starter Kit`는 없는 리소스만 만든다. 기존 제작 리소스를 덮어쓰지 않는다. 폰트 적용 메뉴는 명시적으로 실행했을 때 starter board 폰트만 변경한다.

보상 gold/EXP는 템플릿에서 0이다. 확정된 금액이 제공되지 않아 밸런스 값을 만들어 넣지 않았다. 실제 보상량은 각 Quest SO에서 지정한다. 임무 종류별 고정 판정과 보상 지급 기능은 구현되어 있다.

## 배치/연결 단계

1. 사용할 4종 SO를 영속 `QuestRuntime.questDefinitions`에 등록한다. 이 연결은 새 Editor 프로세스에서 저장을 읽을 때도 필요하다. 기존 Quest 정의를 지우지 않는다.  
2. 기존 QuestCompletionFlow와 QuestCalendarIntegration의 QuestRuntime, RewardService, CalendarService를 연결한다. 정산을 사용할 경우 DaySettlementFlow를 연결한다. 동일 시스템을 새로 중복 설치하지 않는다.  
3. Board prefab을 기존 라우팅된 Field Canvas 아래에 두고 QuestRuntime을 지정한다. 같은 BoardRow를 Button 없이 기존 Field HUD에 연결하면 진행 표시만 재사용할 수 있다.  
4. 맵의 수락/귀환 장소와 목적지를 기존 SceneFlow/DungeonCompletionFlow에 연결한다. `completionQuestId`는 해당 SO의 QuestId다. 이 Batch는 실제 목적지/SpawnPoint를 만들어 넣지 않는다.  
5. 모든 World 연결 컴포넌트의 QuestRuntime과 questId를 지정한다. SO의 ID와 오브젝트 ID는 대소문자까지 동일해야 한다. 실행 중 SO의 종류/목표 수/ID를 변경하지 않는다.

### 탐사

동일 면적의 서로 겹치지 않는 탐사 셀에 SurveyCell trigger를 배치하고 각 `areaId`를 전체 대상 목록과 일치시킨다. Player 태그와 Rigidbody2D/Collider2D의 Trigger 접촉이 필요하다. 같은 셀의 여러 Collider 접촉은 한 번만 집계한다. 서로 다른 면적의 임의 구역을 같은 셀로 취급하면 실제 면적 비율과 달라지므로 같은 면적 기준으로 제작한다. 전체 대상 수를 바꾸면 objective의 requiredCount도 ceil(대상 수 × 0.9)로 맞춘다.

### 소탕

각 지정 조우 Group에 GeneralMissionCombatEncounterAdapter를 추가한다. Group의 encounterId를 목록과 맞추고, 모든 조우 Trigger는 해당 Group을 참조하도록 한다. Group root는 유지하고 적 멤버만 전투 Presentation이 비활성화하도록 한다. 새 수락에서 해당 Group 멤버의 위치·회전·활성 상태·체력·Collider·Behaviour·Rigidbody 시뮬레이션을 기존 restore 경로로 되돌린다. 이 Group의 완료는 전역 `world.clearedEncounterIds`에 쓰지 않는다. 다른 지역의 영구 클리어 기록은 수정하지 않는다. 기존 EncounterQuestObjectivePublisher를 새 일반임무의 별도 집계 경로로 사용하지 않는다.

### 억제 장치

후보 6개 위치에 Suppressor를 두고 deviceId를 지정한다. 실제 활성 지점 3개는 수락 시 선정되어 저장된다. 실패 재도전/로드는 같은 선정, 새 RunId는 새 추첨이다. 같은 조합이 다시 나올 수 있다. `visualRoot`는 adapter가 붙은 root가 아닌 별도 자식으로 연결한다. `onActivationChanged(bool)`로 불빛/애니메이션을 갱신한다. root/component를 가동 후 꺼 버리면 다음 수락의 갱신을 받을 수 없으므로 유지한다.

### 구출

RescueEscort의 targetId와 followTarget을 플레이어에 연결한다. 필요하면 저장 복원 시 Player 태그로 한 번 탐색해 동행 대상을 다시 연결한다. 이동은 Rigidbody2D로 플레이어의 이동 궤적을 따라간다. gravityScale=0, 회전 고정, 벽과 충돌하는 body Collider와 상호작용 범위 Trigger를 사용한다. 구출 대상은 정규 3인 전투 편성에 넣지 않는 필드 동행자다. 파티 전투 슬롯/스킬 대상 체계는 변경하지 않았다.

RescueExit에 areaId=rescue\_exit와 escort 참조를 연결한다. 플레이어가 출구 Trigger에 있고 동행 대상이 escortExitDistance 이내에 있어야 한다. HP 79%면 완료되지 않고, 정확히 80%는 성공한다. HP 0은 실패하며 재도전 시 체력/동행/위치/경로를 초기화한다. 발견/대화만으로 완료되지 않는다.

필드 피해가 필요하면 EscortHazard를 배치해 고정 hazardId/피해량을 지정한다. 전투·서사·아이템의 별도 피해/회복 결과는 `ApplyDamage(amount, outcomeId)` / `ApplyHealing(amount, outcomeId)`로 전달한다. 같은 outcomeId의 중복 적용은 저장 후에도 거부한다. 구출 대상을 전투 편성에 자동 추가하거나 전투 피해를 임의 분배하는 규칙은 도입하지 않았다.

## 저장 계약

Schema 14\. 이전 1\~13 저장을 마이그레이션하며 기존 repeat run/reward/day 원장은 유지한다. 일반임무 상태는 QuestState 아래에 선택 지점, 집계된 ID, 체력 결과 ID, 동행/탈출, 동행 위치/이동 경로를 저장한다. 정의 에셋에는 쓰지 않는다. runtime 상태와 저장 DTO는 분리되어 있고 snapshot/저장 목록은 복사한다. 로드는 진행/완료 gameplay event를 재발행하지 않는다. 새로 수락하지 않고 현재 진행을 복원한다.

## 수동 확인 절차 — 실제 배치 이후

1. 탐사 89%에서는 미완료, 90%에서 완료, 같은 셀 재진입으로 중복 집계되지 않는지 확인한다.  
2. 소탕은 목록 외 적을 처치해도 목표가 오르지 않고 마지막 지정 조우로만 완료되는지 확인한다. 재수락 시 적이 정상 HP/위치로 돌아오고 기존 Contact/Field Attack으로 다시 전투가 시작되는지 확인한다.  
3. 장치 후보 중 정확히 3개만 활성화 대상인지, 중복 가동이 무효인지, 중간 저장/로드에 선정·가동이 유지되는지 확인한다.  
4. NPC가 벽 모서리·좁은 통로에서 플레이어의 실제 경로를 따라오는지, Combat/Dialogue/Reward/Pause에서 정지하는지 확인한다.  
5. NPC를 멀리 둔 출구 진입, HP 79% 출구, HP 80% 출구, 사망/재도전, 동행 중 저장/새 프로세스 Continue를 확인한다.  
6. 각 임무를 두 번 완료해 보상과 1일 비용이 각 run당 한 번 적용되는지, 완료/정산 중간 저장에서 중복되지 않는지 확인한다.  
7. UI가 기존 Router 표시 정책을 따르는지, 다른 상태에서 수락·필드 상호작용이 동작하지 않는지 확인한다. 한국어 줄바꿈/글꼴/버튼 크기를 실제 해상도로 확인한다.

자동 테스트 결과는 작업 완료 보고서를 따른다. 배치 전 기능 구현 완료와 실제 맵 통합 완료를 구분한다.