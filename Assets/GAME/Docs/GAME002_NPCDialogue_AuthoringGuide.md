# GAME_002 NPC / Dialogue / Choice 제작 가이드

> 대상: 기획자, Unity 콘텐츠 작업자. 기준은 현재 Production Interaction/Story 경로다.

## 1. 콘텐츠 개요

Production NPC는 가까이 가서 상호작용 입력을 받는 `InteractableObject`이며, Story 대화는 `StoryInteractionEventSO`가 `StoryEventRunner`에 `StoryEventDefinitionSO`를 요청하는 방식으로 시작한다. 일반 NPC 대화/선택지는 이 경로를 사용한다.

## 2. 현재 구현 상태

대사 노드, 조건부 선택지, 노드 효과, 시간제한 선택지, Interaction의 반복/일회성 정책과 저장된 PersistentOnce 상태가 구현되어 있다. `TimedChoiceDialogueEventSO`는 구형 `InteractionEventSO.Execute` 경로이며 Production persistent interaction의 표준이 아니다. `DialogueRunner`, Demo NPC, Debug trigger도 Production 표준으로 사용하지 않는다.

## 3. Production Owner

`InteractionController`는 InputService의 ExplorationInteract 명령과 prompt를, `InteractionRunner`/`InteractionRuntime`은 상호작용 실행·저장을, `StoryEventRunner`은 Story 노드 진행과 GameState를, `StoryDialogueHUD`/`TimedChoicePanel`은 표시만 담당한다. NPC나 SO가 UI를 직접 켜거나 GameState를 직접 쓰지 않는다.

## 4. 사용 코드/Asset 목록

| 역할 | 실제 타입 / 위치 |
| --- | --- |
| NPC trigger | `Game.Interaction.InteractableObject` |
| NPC event | `Game.Interaction.StoryInteractionEventSO` — Create `GAME/Interaction/Story Event` |
| 대화 데이터 | `Game.Story.Data.StoryEventDefinitionSO` — Create `GAME/Story/Story Event` |
| 노드/선택 | `StoryNode`, `StoryChoice`, `StoryCondition`, `StoryEffect` |
| Production NPC prefab | `Assets/GAME/Prefabs/Interaction/ProductionNpcInteraction.prefab` |
| 기준 씬 UI | `Assets/GAME/Scenes/Dungeon_Template.unity`의 `StoryEventRunner`, `StoryDialogueHUD`, `TimedChoicePanel` |

## 5. 관련 시스템과 데이터 흐름

```text
Player 범위 진입 → InteractableObject 등록 → InteractionController + ExplorationInteract
→ InteractionRunner → StoryInteractionEventSO → StoryEventRunner
→ StoryEventDefinitionSO의 StoryNode / StoryChoice → HUD·TimedChoicePanel
→ StoryEffect (Quest/Reward/flag 등) → 종료 후 Exploration
```

## 6. 콘텐츠 제작 준비물

승인된 NPC 임시 ID, 등장 위치, 대사/선택지 표, 조건과 결과, portrait·sprite·SFX 요구사항을 준비한다. persistent interaction ID와 event/action ID는 Unity 담당자가 기존 Scene/Save 참조와 중복을 확인해 정한다.

## 7. 신규 콘텐츠 생성 방법

1. 기존 `ProductionNpcInteraction.prefab`을 기준으로 배치한다. 새 NPC root에는 `Collider2D` trigger와 `InteractableObject`가 필요하다.
2. **Create > GAME > Story > Story Event**로 Story Event SO를 만들고 event ID, start node, nodes를 작성한다.
3. **Create > GAME > Interaction > Story Event**로 event SO를 만들고 `eventDefinition`에 2번 SO를 연결한다. Runner는 씬의 유일한 `StoryEventRunner`을 사용한다.
4. NPC `InteractableObject.events`에 3번 event를 넣는다. Production 실행에는 LegacyCompatibility가 아닌 `Repeatable`, `OncePerSession`, `PersistentOnce` 정책을 사용한다.
5. `PersistentOnce`라면 고유 `interactionId`, 고유 event `actionId`를 반드시 작성한다. Scene을 저장하고 validator를 실행한다.

## 8. Inspector 필드 설명

| 대상 | 필드 | 의미 |
| --- | --- | --- |
| InteractableObject | `promptText`, `playerTag` | 표기 문구와 Player 판정. canonical input은 Input 레이어가 처리 |
| InteractableObject | `interactionId`, `usePolicy` | 저장/중복 방지용 identity와 사용 정책 |
| InteractableObject | `conditions`, `events` | 조건을 모두 만족할 때 이벤트 순서대로 실행 |
| Story Event | `eventId`, `startNodeId`, `nodes` | Story graph의 안정 ID, 시작 노드, 노드 목록 |
| StoryNode | `nodeId`, `speakerName`, `portrait`, `body` | 노드 식별자와 화면 표시 |
| StoryNode | `choices`, `useTimedChoices`, `choiceTimeLimitSeconds`, `timeoutChoiceIndex`, `timeoutNodeId` | 선택과 타이머. 타임아웃 정책도 기획에 명시 |
| StoryNode | `nextNodeId`, `effects`, `endEvent` | 선택이 없을 때 다음 노드·효과·종료 |
| StoryChoice | `choiceId`, `text`, `nextNodeId`, `conditions`, `effects`, `hideIfConditionNotMet`, `disabledReason` | 분기와 조건부 노출/비활성 표시 |

## 9. Scene/Prefab 연결 방법

NPC에는 물리 이동을 막지 않는 trigger collider를 사용한다. Player root의 `InteractionController`가 prompt UI에 연결돼 있어야 한다. Story UI는 `Dungeon_Template`/Production scene의 유일한 `StoryEventRunner`과 ready 상태의 `StoryDialogueHUD`, `TimedChoicePanel`을 재사용한다. NPC에 `InteractionController`, `GameStateMachine`, 별도 UI를 추가하지 않는다.

## 10. 기존 시스템 등록 방법

별도 NPC Registry는 없다. Story event SO를 `events` 목록에 연결하는 것이 등록이다. Quest 시작/진행/보상은 `StoryEffect`의 기존 `StartQuest`, `PublishQuestEvent`, `GrantReward`만 사용한다. 직접 QuestRuntime/RewardService를 대체하지 않는다.

## 11. Play Mode 검증 방법

Player로 NPC trigger에 진입해 prompt가 표시되는지 확인하고 실제 Exploration interact 입력으로 시작한다. 모든 line/choice를 진행하고, timed node는 선택·시간 만료 양쪽을 확인한다. 종료 뒤 `Exploration`으로 돌아오며 prompt가 갱신되는지 본다. PersistentOnce는 Save → cold load 뒤 재실행되지 않아야 한다.

## 12. 통과 기준

NPC 하나당 trigger, production event, event definition, Story UI 경로가 모두 연결되고 Console 오류 없이 대화·선택·효과·입력 복귀가 기획과 일치해야 한다.

## 13. 자주 발생하는 오류

| 증상 | 먼저 확인할 곳 |
| --- | --- |
| prompt 없음 | NPC Collider2D가 trigger인지, tag/범위, Player의 InteractionController prompt 참조 |
| 대화 시작 거부 | GameState가 Exploration인지, StoryEventRunner/eventDefinition 연결 |
| Persistent NPC validator 실패 | interactionId/actionId 중복, Legacy-only event 사용 여부 |
| 선택이 안 보임 | node choices/conditions, StoryDialogueHUD의 TimedChoicePanel 연결 |

## 14. 구현되지 않은 기능

노드별 음성 재생, 대화 진행 자체의 저장/재개 정책, 임의의 custom 조건/효과, 다국어 테이블 import는 이 경로의 Inspector-only 기능으로 확인되지 않았다. 요구사항은 별도 개발 요청으로 작성한다.

## 15. 수정 시 주의사항

기존 `eventId`, node/choice ID, persistent `interactionId`, action ID는 Save/중복 보상 identity에 영향이 있으므로 변경 전 참조를 조사한다. Demo/Legacy NPC, `TimedChoiceDialogueEventSO`, `StoryDialogueTrigger2D`는 Production NPC 표준으로 복제하지 않는다.

## 16. 관련 문서 링크

[공통 표준](GAME002_ContentProductionStandards.md) · [NPC 기획 템플릿](GAME002_NPCDialogue_PlanningTemplate.md) · [NPC 검증 체크리스트](GAME002_NPCDialogue_ValidationChecklist.md) · [Quest 가이드](GAME002_Quest_AuthoringGuide.md)
