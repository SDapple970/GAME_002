# GAME_002 Quest / Mission 제작 가이드

## 1. 콘텐츠 개요

새 Production 퀘스트는 `QuestDefinitionSO`와 `QuestRuntime`을 사용한다. `MissionDefinitionSO`, `MissionManager`, DemoMission은 호환/기존 경로이며 신규 Production Quest의 기본 템플릿이 아니다.

## 2. 현재 구현 상태

Quest ID, objective의 event type/target/count, optional/group/visibility, active·completed·failed 상태, Retry 정책, Reward gold/EXP, Save/Load가 구현되어 있다. `QuestRuntime`은 persistent outcome identity가 없는 새 production event를 거부한다.

## 3. Production Owner

`QuestRuntime`이 상태·저장을, `QuestObjectiveTracker`가 `QuestEventChannel` 구독을, `QuestCompletionFlow`가 완료 reward/state 전환을 맡는다. Combat/Interaction/Story는 QuestEvent를 publish할 뿐 직접 완료 상태를 쓰지 않는다.

## 4. 사용 코드/Asset 목록

| 역할 | 실제 타입 / 메뉴 |
| --- | --- |
| 정의 | `Game.Quest.QuestDefinitionSO` — Create `GAME/Quest/Quest Definition` |
| 목표 | `QuestObjectiveDefinition` (`objectiveId`, `eventType`, `targetId`, `requiredCount`, `optional`, `groupIndex`, `visibility`, `description`) |
| runtime | `QuestRuntime`, `QuestObjectiveTracker`, `QuestCompletionFlow` |
| publisher | `CombatQuestObjectivePublisher`, `InteractionQuestObjectivePublisher`, Story `PublishQuestEvent` effect |
| 데이터 예시 | `Assets/GAME/Data/Quest/`의 `VALIDATION_PRODUCTION_*` assets — 검증용으로 명시 |

## 5. 관련 시스템과 데이터 흐름

```text
QuestDefinitionSO → QuestRuntime(시작/상태/저장)
Combat·Interaction·Story publisher → QuestEventChannel → QuestObjectiveTracker
→ QuestRuntime.ApplyEvent → QuestCompletionFlow → RewardService / GameFlow
```

## 6. 콘텐츠 제작 준비물

퀘스트 임시 ID, title/description, 각 objective의 event type·target·수량·순서, optional 여부, reward gold/EXP, 실패/재시도 요구, 시작/완료 이벤트를 승인받는다. runtime `questId`는 담당자가 기존 정의와 Save 참조를 확인해 배정한다.

## 7. 신규 콘텐츠 생성 방법

1. **Create > GAME > Quest > Quest Definition**으로 SO를 만든다.
2. `questId`, title, description, category, day cost, objectives, reward gold/EXP, retry policy를 입력한다.
3. objective마다 고유 objective ID와 기존 `QuestEventType`, target ID, count, group/visibility를 작성한다. group은 순차 진행 묶음이다.
4. Scene의 기존 `QuestRuntime.questDefinitions` 배열에 추가한다. `QuestRuntime`/tracker/completion flow를 새로 만들지 않는다.
5. 시작은 Production Story effect `StartQuest` 또는 기존 Production integration으로 요청한다. 진행은 publisher가 stable outcome identity를 갖는지 확인한다.

## 8. Inspector 필드 설명

| 필드 | 의미 |
| --- | --- |
| `questId` | Save/이벤트 연결에 쓰는 고유 불변 ID |
| `questTitle`, `description`, `category` | HUD/콘텐츠 표기와 분류 |
| `missionDayCost` | 일일/mission 연동 메타데이터; 기획 근거가 있을 때만 사용 |
| `objectives` | objective array. group/optional/visibility 포함 |
| `rewardGold`, `rewardExp` | QuestCompletionFlow가 RewardService로 전달하는 완료 보상 |
| `retryPolicy` | 현재 `NotRetryable` 또는 지원 enum의 재시작 정책 |

## 9. Scene/Prefab 연결 방법

Production Dungeon은 하나의 `QuestRuntime`, `QuestObjectiveTracker`, `QuestCompletionFlow`, publisher들을 재사용한다. Quest HUD가 있는 Scene은 기존 UI binding을 유지한다. World object/NPC에 QuestRuntime을 넣거나 직접 CompleteQuest를 호출하는 새 경로를 추가하지 않는다.

## 10. 기존 시스템 등록 방법

`QuestRuntime.questDefinitions`에 SO를 추가한다. objective target ID는 publisher가 발행하는 target ID와 정확히 맞춰야 한다. 완료 reward는 definition + `QuestCompletionFlow`의 기존 RewardService 경로가 owner다.

## 11. Play Mode 검증 방법

Quest를 시작하고 objective를 실제 Combat/Interaction/Story에서 진행한다. group 순서와 optional objective를 확인하고, 마지막 필수 목표가 완료될 때 Reward/HUD/Exploration 흐름을 본다. Save 후 cold load해 status/progress/reward dedupe를 확인한다.

## 12. 통과 기준

한 번의 같은 persistent event가 두 번 진행되지 않고, 목표 수량/순서/visibility와 completion reward가 기획대로이며 다른 active quest를 오염시키지 않아야 한다.

## 13. 자주 발생하는 오류

| 증상 | 확인 |
| --- | --- |
| 시작 안 됨 | questId, QuestRuntime registry, StartQuest effect/definition 참조 |
| 진행 안 됨 | objective event type/target ID, publisher, stable outcome identity |
| reward 없음/중복 | QuestCompletionFlow·RewardService·questId, game state/ledger |
| HUD 불일치 | QuestRuntime state restore와 tracker/HUD subscription |

## 14. 구현되지 않은 기능

Item 획득 objective가 어떤 production publisher로 발생하는지, 반복 가능한 completed Production quest, 임의 취소/분기형 실패 UX는 Inspector-only 표준으로 확정하지 않는다. 기획 요구는 별도 개발 요청으로 분리한다.

## 15. 수정 시 주의사항

기존 quest/objective/target ID와 reward 값 변경은 Save, 진행, dedupe에 영향이 있다. DemoMission/Mission data를 Production Quest에 복사하거나 World object가 독자적으로 상태를 소유하지 않게 한다.

## 16. 관련 문서 링크

[공통 표준](GAME002_ContentProductionStandards.md) · [Quest 템플릿](GAME002_Quest_PlanningTemplate.md) · [Quest 체크리스트](GAME002_Quest_ValidationChecklist.md) · [Reward 가이드](GAME002_ItemReward_AuthoringGuide.md)
