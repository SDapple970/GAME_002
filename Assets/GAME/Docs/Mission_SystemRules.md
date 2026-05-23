# Mission System Rules

## Scope

- `MissionManager`는 플레이어에게 보이는 현재 임무와 목표만 관리한다.
- `StoryProgressManager`는 챕터와 메인 진행도를 관리한다.
- `StoryFlagManager`는 자유 플래그를 관리한다.
- 세 시스템은 합치지 않는다.
- Save/Load는 아직 구현하지 않는다.

## Naming

- Mission Id는 고유해야 한다.
- 챕터 1 임무는 `MSN_CH01_` 접두어를 사용한다.
- Objective Id는 해당 Mission 내부에서 고유해야 한다.

## Authoring

- 임무 시작은 `StoryEffect.StartMission`과 `MissionDefinitionSO` 참조로 처리한다.
- 목표 완료는 `StoryEffect.CompleteMissionObjective`에 `missionId`와 `objectiveId`를 넣어 처리한다.
- 임무 완료는 `StoryEffect.CompleteMission`에 `missionId`를 넣어 처리한다.
- 모든 필수 목표 완료 시 자동 완료하려면 `MissionDefinitionSO.AutoCompleteWhenAllObjectivesComplete`를 켠다.
- Mission 조건 분기는 `StoryCondition.MissionActive`, `MissionCompleted`, `MissionObjectiveCompleted`를 사용한다.

## Sample Setup

- `MSN_CH01_FindFirstNPC`는 `EVT_CH01_Intro_Auto` 마지막 노드에서 시작한다.
- `EVT_CH01_NPC_FirstTalk` 결과 노드에서 `talk_first_npc` 목표를 완료한다.
- `MissionHUD`는 `Canvas_Overworld` 아래에 둔다.
- `MissionManager`는 `Systems/MissionSystem` 아래에 둔다.
