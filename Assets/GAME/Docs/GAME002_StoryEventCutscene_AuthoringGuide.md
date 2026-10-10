# GAME_002 Story Event / Cutscene 제작 가이드

## 1. 콘텐츠 개요

Production Story Event는 `StoryEventDefinitionSO`의 node graph를 `StoryEventRunner`가 실행하는 데이터 콘텐츠다. 대화/선택/condition/effect는 이 graph에 포함된다. 별도 영상 cutscene은 일반 Story Event 표준으로 완성돼 있지 않다.

## 2. 현재 구현 상태

Story Event SO, node/choice timed branch, StoryCondition, StoryEffect(Set flag/progress, start/publish quest, reward 등), Interaction 시작 경로가 있다. `MissionCompleteCutsceneController` + `VideoPlayer`는 `MissionManager` 완료에 연결된 별도 구형 Mission cutscene 경로다. Scene-wide Production Story Event가 Video/Timeline을 실행하는 inspector 경로는 확인되지 않았다.

## 3. Production Owner

`StoryEventRunner`이 graph 진행, `StoryDialogueHUD`/`TimedChoicePanel`이 presentation, Story flag/progress service와 `RewardService`/`QuestRuntime`이 effect 결과를 각자 소유한다. `StoryEffect`가 사용하는 `StoryFlagManager`의 개별 flag 저장 범위는 이 authoring 절차만으로 보장하지 않으므로 Save/Load 요구가 있으면 실제 owner를 별도 검증한다. Story SO가 UI root나 GameState manager를 직접 새로 만들지 않는다.

## 4. 사용 코드/Asset 목록

| 역할 | 실제 타입 / 메뉴 |
| --- | --- |
| Story graph | `Game.Story.Data.StoryEventDefinitionSO` — Create `GAME/Story/Story Event` |
| Node | `StoryNode` (`nodeId`, speaker/portrait/body, choices, timing, next/effects/end) |
| Choice | `StoryChoice` (`choiceId`, text, next, conditions/effects, visibility) |
| effect | `StoryEffect` (`SetBoolFlag`, progression, Quest, Reward 등 enum) |
| start | `StoryInteractionEventSO`, `StoryEventRunner` |
| legacy cutscene | `Game.Cutscene.MissionCompleteCutsceneController`, `CutscenePlaybackRequest` |

## 5. 관련 시스템과 데이터 흐름

```text
Interaction / approved trigger → StoryEventRunner → StoryEventDefinitionSO
→ node/choice condition → StoryEffect → flags / QuestEvent / RewardService
→ Story UI closes → Exploration
```

## 6. 콘텐츠 제작 준비물

event 임시 ID, trigger/condition, speakers/nodes/choices, flag/quest/reward outcomes, one-shot policy, portrait/SFX requirements를 승인받는다. event/node/choice/flag/reward source IDs는 Unity 담당자가 existing save references를 확인한다.

## 7. 신규 콘텐츠 생성 방법

1. **Create > GAME > Story > Story Event** SO를 생성한다.
2. `eventId`, `startNodeId`, nodes를 작성한다. 모든 referenced next node ID가 존재하도록 graph를 검토한다.
3. 노드에 speaker/portrait/body, next/end, effects를 쓰고 choice가 있으면 choice ID/text/condition/effect/next를 입력한다.
4. timed choice에는 `useTimedChoices`, limit, timeout index/node를 승인 기획에 맞춰 입력한다.
5. NPC에서 시작하려면 StoryInteractionEventSO의 eventDefinition에 연결하고 Production `InteractableObject.events`에 추가한다. Story-trigger/Quest 연결은 existing supported event/effect로만 만든다.

## 8. Inspector 필드 설명

| 대상 | 필드 | 의미 |
| --- | --- | --- |
| Story Event | `eventId`, `startNodeId`, `nodes` | stable event graph identity/start/list |
| StoryNode | `nodeId`, `speakerName`, `portrait`, `body` | 표시 데이터 |
| StoryNode | choices/timing/timeout/next/effects/end | 분기·자동 진행·종료 |
| StoryChoice | `choiceId`, text, next, conditions/effects, hide/disabled reason | 조건부 선택과 결과 |
| StoryEffect | type/key/value/quest/reward fields | enum이 지원하는 결과만 authoring |
| Cutscene request | clip/url, useUrl, show reward, onFinished | MissionComplete legacy component에서만 확인됨 |

## 9. Scene/Prefab 연결 방법

Story Event에는 `StoryEventRunner`과 ready `StoryDialogueHUD`/TimedChoicePanel이 있는 existing scene을 사용한다. video cutscene을 요구하면 MissionCompleteCutsceneController의 VideoPlayer/RawImage/RenderTexture/audio/GameState/MissionManager wiring이 모두 필요하지만, 이 경로를 새 Production Story Event 표준으로 연결하지 않는다.

## 10. 기존 시스템 등록 방법

Story Event는 start source의 StoryInteractionEventSO로 연결한다. `StartQuest`, `PublishQuestEvent`, `GrantReward`는 StoryEffect가 기존 owner에 요청한다. effect source ID가 비어 있을 때 compatibility identity warning이 날 수 있으므로 Production reward에는 승인된 `rewardSourceId`를 authoring한다.

## 11. Play Mode 검증 방법

trigger에서 event를 시작하고 모든 node/choice/condition/timer/timeout branch를 실행한다. flag/Quest/Reward result가 한 번만 적용되고 UI/입력이 Exploration으로 복귀하는지 확인한다. Reward/Quest effect는 Save/Load 뒤 duplicate가 없는지 확인한다. flag persistence가 요구되면 해당 flag owner의 실제 save participant 여부를 별도 확인한다. Video cutscene 요구는 별도의 legacy Mission scene에서만 검증한다.

## 12. 통과 기준

graph가 dead end/missing reference 없이 시작→분기→효과→종료하고, 결과 owner가 ledger/Quest/flag를 중복 없이 저장하며 input/UI state가 복귀해야 한다.

## 13. 자주 발생하는 오류

| 증상 | 확인 |
| --- | --- |
| event 시작 안 됨 | eventDefinition/runner, Interaction policy, Exploration state |
| branch 이상 | node/choice/next ID, condition, timeout target |
| quest/reward 없음 | StoryEffect의 type/target/source ID 및 target owner availability |
| video 미재생 | legacy cutscene의 clip/url, VideoPlayer RenderTexture/audio refs |

## 14. 구현되지 않은 기능

Production Story graph에서 Timeline, camera shot track, generic VideoPlayer playback, skip/replay persistence, cutscene checkpoint/resume는 지원 절차가 확인되지 않았다. 이들은 기능 요청이다.

## 15. 수정 시 주의사항

기존 event/node/choice/flag/reward IDs 변경은 save/progress/dedupe에 영향을 준다. Story Event와 legacy DialogueRunner, MissionComplete video controller, Debug hotkey를 혼동하지 않는다.

## 16. 관련 문서 링크

[Story 템플릿](GAME002_StoryEventCutscene_PlanningTemplate.md) · [Story 체크리스트](GAME002_StoryEventCutscene_ValidationChecklist.md) · [NPC 가이드](GAME002_NPCDialogue_AuthoringGuide.md) · [공통 표준](GAME002_ContentProductionStandards.md)
