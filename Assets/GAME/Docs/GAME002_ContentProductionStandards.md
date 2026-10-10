# GAME_002 공통 콘텐츠 제작 표준

> 이 문서는 콘텐츠 authoring의 공통 계약이다. 실제 Production 코드와 serialized asset이 우선이며, 새 Manager/EntryPoint/ID 체계를 만드는 설계 문서가 아니다.

## 1. 제작 단계와 완료 정의

```text
기획 초안 → 기획 검토 → 기획 승인 → Unity Asset/Inspector 적용
→ 자동 검증 → Play Mode 검증 → 통합/SaveLoad 검증 → 개발현황 기록
```

기획 승인, 구현 상태, 검증 상태를 하나의 “완료”로 합치지 않는다.

| 구현 상태 | 의미 | 검증 상태 | 의미 |
| --- | --- | --- | --- |
| Not Implemented | Production 지원 없음 | Unverified | 증거 없음 |
| In Progress | 작업 중 | Compile Verified | 실제 compiler 성공 |
| Implementation Complete | 요청 범위 적용 완료 | Inspector Verified | serialized refs/data 점검 |
| Needs Fix | 결함/연결 실패 | Play Mode Verified | 실제 scene/input/physics/UI 확인 |
|  |  | Integration Verified | Save/Load 및 연계 포함 |

## 2. ID·저장 호환 정책

| 범주 | 실제 ID / owner | 규칙 |
| --- | --- | --- |
| 기획 임시 ID | 문서 관리 | 사람이 읽는 추적용. runtime 참조로 사용하지 않음 |
| Skill | `SkillDefinitionSO.persistentKey`, `skillId` | Save/Registry 참조. 기존 값 변경 금지 |
| Enemy source | `EnemyDefinitionSO.persistentKey` | Film mapping/defeated provenance용 stable key |
| Character | `CharacterProgressionDefinitionSO.characterId` | Unique owner/progression key |
| Interaction | `InteractableObject.interactionId`, event `actionId` | PersistentOnce/save outcome identity |
| Quest | `questId`, `objectiveId`, target ID | runtime progress/publisher match |
| Item | `ItemDefinitionSO.itemId` | catalog/inventory save key |
| Story | event/node/choice/flag/reward source IDs | graph/result identity 참조. flag의 save coverage는 owner별 확인 필요 |
| Unity GUID | `.meta` | Unity asset reference. 생성·교체·재사용 금지 |

새 Production ID는 Unity 적용 담당자가 현재 asset, Registry, Scene serialized ref, save migration 영향을 조사한 뒤 배정한다. 기존 ID 변경은 migration 작업이며 콘텐츠 수치 조정과 분리한다.

## 3. 실제 폴더·Asset 정책

- Script root: `Assets/GAME/Scripts`; production data root: `Assets/GAME/Data`; scenes: `Assets/GAME/Scenes`; prefabs: `Assets/GAME/Prefabs`; docs: `Assets/GAME/Docs`.
- 확인된 기존 data 예: Skill은 `Assets/GAME/Data/Skill/`, Quest는 `Assets/GAME/Data/Quest/`, Interaction/Story는 `Assets/GAME/Data/Interaction/` 및 `Assets/GAME/Data/Story/`.
- EnemyDefinition/Production CharacterProgression의 authoring asset directory는 아직 Production 표준으로 확정되지 않았다. 새 폴더를 문서만으로 강제하지 말고 첫 authoring 전에 승인한다.
- 기존 asset을 복제/수정하기 전 Ref, Scene/Prefab, `.meta` GUID를 확인한다. 새 Markdown의 Unity `.meta` 생성만 이 문서 작업에서 허용된다.

## 4. Production Owner 경계

| 시스템 | Production owner | 콘텐츠가 해야 할 일 | 하면 안 되는 일 |
| --- | --- | --- | --- |
| global flow/save | `GameFlowController`, `SceneFlowController`, `SaveLoadService` | 기존 요청/definition 연결 | feature가 GameState 직접 소유 |
| input/UI | Input layer, `UIScreenRouter` | 기존 routed UI 참조 사용 | keyboard polling/새 UI state owner |
| combat | `CombatEntryPoint` | encounter/loadout/data 연결 | 두 번째 combat start path |
| interaction/story | `InteractionRunner`, `StoryEventRunner` | event graph 연결 | NPC가 UI/flow 직접 제어 |
| quest | `QuestRuntime`, tracker/completion flow | definition/publisher target 연결 | world object가 독립 Quest 상태 보유 |
| reward/inventory | `RewardService`, `InventoryService`, wallet | valid request/catalog 연결 | UI/object가 currency/inventory 직접 변경 |

## 5. Legacy / Demo / Debug 구분

Production authoring은 production Scene/owner와 validator가 확인한 경로만 사용한다. 다음은 새 표준이 아니다: `DemoMission`, `MissionManager`/`MissionDefinitionSO` compatibility, `Game.Battle.FieldEnemy`, `TimedChoiceDialogueEventSO` legacy execution, `DialogueRunner` compatibility, debug hotkey/test Scene, MissionComplete video cutscene. 삭제하지 말고 신규 의존성만 만들지 않는다.

## 6. 변경 관리

1. 기획서와 owner/Asset path/ID 영향 범위를 먼저 기록한다.
2. `git status`에서 기존 변경을 확인하고 관계없는 파일을 건드리지 않는다.
3. Scene/Prefab/SO/serialized field 변경 전 YAML/reference/call site를 확인한다.
4. Inspector 적용 뒤 validator → compile → Play Mode → integration 순서로 증거를 남긴다.
5. 실패를 ID 변경이나 새 Manager로 우회하지 않는다. source/owner/serialized ref를 고친다.

## 7. 기능 추가 요청 표준

Inspector에 없는 요구는 아래 표로 개발 요청을 만든다.

| 항목 | 원하는 게임 규칙 | 현재 지원 여부/근거 | 필요한 Production owner | UI/Input/Save 영향 | 테스트 통과 기준 |
| --- | --- | --- | --- | --- | --- |
| | | 지원/일부/미지원/미확인 | | | |

예: 확률 drop table, 새 Quest objective type, item use effect, generic Story Timeline/cutscene, custom skill targeting은 authoring 데이터가 아니라 추가 개발 요청이다.

## 8. 최소 검증 규칙

- 콘텐츠 asset/Inspector: ID와 Missing reference, Registry/catalog/owner 연결 확인.
- 자동: 해당 validator menu를 실제로 실행하고 Console 결과를 기록.
- Play Mode: 입력·physics·UI·coroutine·scene에 의존하면 실제로 실행.
- 통합: Quest/Reward/Story/Combat/Exploration 및 필요한 Save/Load를 통과.
- 문서에는 실행한 검증만 PASS라고 적고, 검증하지 않은 항목은 미실행/미확인으로 남긴다.

분야별 절차는 [전체 인덱스](GAME002_ContentAuthoring_Index.md)를 사용한다.
