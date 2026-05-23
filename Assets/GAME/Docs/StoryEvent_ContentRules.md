# Story Event Content Rules

## Naming

- Event Id는 반드시 고유해야 한다.
- 테스트 이벤트는 `EVT_Test_` 접두어를 사용한다.
- 실제 챕터 이벤트는 `EVT_CH01_`, `EVT_CH02_` 접두어를 사용한다.
- 자동 트리거 이벤트는 `_Auto` 접미어를 사용한다.
- NPC 대화 이벤트는 `_NPC_` 이름을 사용한다.

## Event Authoring

- 시간제한 선택지 이벤트는 `StoryNode.UseTimedChoices`를 켠다.
- 시간제한 선택지는 기본 2개 기준으로 작성한다.
- 시간 초과 처리는 `TimeoutChoiceIndex` 또는 `TimeoutNodeId` 중 하나를 명확히 설정한다.
- 1회성 이벤트는 `StoryInteractable2D.BlockIfEventCompleted`를 켠다.
- 반복 대화는 `BlockIfEventCompleted`를 끈다.
- 진행도 해금은 `StoryInteractable2D.InteractionConditions`에서 `MainProgressAtLeast` 또는 `EventCompleted`를 사용한다.

## Persona Stat Note

- 현재 프로젝트의 `PersonaStat` enum은 `Courage`, `Deception`, `Persuasion`, `Intuition`, `Empathy`를 제공한다.
- `Knowledge` 보상이 필요한 샘플은 현재 enum 기준으로 `Intuition`에 매핑한다.

## Unity Setup Guide

- `EVT_CH01_Intro_Auto`는 `StoryEventTrigger2D`에 연결한다.
- `EVT_CH01_NPC_FirstTalk`는 첫 NPC의 `StoryInteractable2D`에 연결한다.
- `EVT_CH01_NPC_AfterProgress`는 두 번째 NPC의 `StoryInteractable2D`에 연결한다.
- 두 번째 NPC는 `Interaction Conditions`에 `MainProgressAtLeast 1`을 넣는다.
- 첫 NPC와 두 번째 NPC 모두 `StorySpeakerAnchor`를 가진다.
- `StoryEventRunner`에는 `StoryDialogueHUD`가 연결되어 있어야 한다.

## Test Checklist

1. Unity 컴파일 에러 없음 확인.
2. CH01 자동 트리거 실행 확인.
3. 첫 NPC에게 `E`로 대화.
4. 시간제한 선택지 표시 확인.
5. 선택 후 `MainProgress`가 1로 변경되는지 확인.
6. 두 번째 NPC가 `MainProgress 1` 이후 상호작용 가능해지는지 확인.
7. 두 번째 NPC 대화에서 시간제한 선택지가 작동하는지 확인.
