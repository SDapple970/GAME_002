# GAME_002 Dungeon / Stage 제작 가이드

## 1. 콘텐츠 개요

새 Production dungeon의 기준은 `Assets/GAME/Scenes/Dungeon_Template.unity`다. 이 template에는 Runtime/Combat/UI, PlayerSpawn, interaction, quest, encounter의 canonical 연결이 있으며 `Assets/GAME/Scenes/Dungeon_1_Production.unity`는 실제 Production 적용 예다.

## 2. 현재 구현 상태

SceneFlowController의 async scene load, RuntimeBootstrapper의 core recovery, player spawn/camera/interaction, combat encounter, Quest/Reward/UI routing, DungeonCompletionFlow/ExitGate의 quest 완료 기반 destination contract가 있다. destination scene/spawn 값이 비어 있으면 dungeon completion은 scene travel을 하지 않는다.

## 3. Production Owner

`SceneFlowController`는 loading/scene load, `RuntimeBootstrapper`는 canonical global services, `CombatEntryPoint`는 전투, `UIScreenRouter`/GameUI root는 UI, `DungeonCompletionFlow`는 완료 contract, `DungeonExitGate`는 exit interaction만 담당한다. dungeon root에 duplicate manager를 추가하지 않는다.

## 4. 사용 코드/Asset 목록

| 역할 | 실제 경로 / 타입 |
| --- | --- |
| 시작 template | `Assets/GAME/Scenes/Dungeon_Template.unity` |
| Production 예 | `Assets/GAME/Scenes/Dungeon_1_Production.unity` |
| scene flow | `Game.Core.SceneFlowController` |
| spawn/travel | `Game.Story.SceneSpawnPoint`, `SceneTravelService` |
| completion/exit | `Game.World.DungeonCompletionFlow`, `DungeonExitGate` |
| player camera | `Assets/GAME/Scripts/Camera/CameraFollow2D.cs` |
| validator | `GAME > Production Migration > Validate Dungeon 1 Production Scene` |

## 5. 관련 시스템과 데이터 흐름

```text
SceneFlowController → load scene → RuntimeBootstrapper core services
→ PlayerSpawn/camera/UI → NPC/interaction + encounter + quest/reward
→ Quest completion → DungeonCompletionFlow → optional SceneFlow destination
```

## 6. 콘텐츠 제작 준비물

Dungeon 임시 ID/scene name, entry/exit, spawn IDs, tile/sprite/collision, NPC/encounter/interaction/quest/reward list, completion quest and destination, BGM/ambient assets를 승인받는다. Build Settings 편집/scene routing은 별도 승인 범위다.

## 7. 신규 콘텐츠 생성 방법

1. Unity Project 창에서 `Dungeon_Template.unity`를 복제한다. 원본 Scene과 `.meta`를 이동/이름 변경하지 않는다.
2. 새 Scene 이름과 저장 위치를 정하되, existing SceneFlow destination 또는 Build Settings 연결은 승인 후 수행한다.
3. template의 Runtime, World, Actors, Main Camera, Production UI hierarchy와 canonical components를 보존한다.
4. `World/Environment`, `World/Collision`, `World/SpawnPoints/PlayerSpawn`, Encounters, NPCs/Interactions를 콘텐츠로 채운다. Tile/physical collider를 분리한다.
5. Enemy/NPC/Quest/Reward는 각각의 가이드에 따라 연결한다. scene에 새 Core/Input/Reward/UI/Combat owner를 넣지 않는다.
6. dungeon 완료가 필요하면 `DungeonCompletionFlow`에 `dungeonId`, completion quest, destination scene/spawn, auto travel을 authoring하고 exit에 기존 `DungeonExitGate`를 연결한다.

## 8. Inspector 필드 설명

| 대상 | 필드 | 의미 |
| --- | --- | --- |
| SceneSpawnPoint | `spawnPointId` | travel destination이 참조하는 stable spawn ID |
| DungeonCompletionFlow | `dungeonId`, `questRuntime`, `completionQuestId` | 완료 조건. QuestRuntime이 진실 source |
| DungeonCompletionFlow | `destinationSceneName`, `destinationSpawnPointId`, `travelWhenCompletionReady` | 승인된 destination만 load. 빈 값은 authoring pending |
| DungeonExitGate | `dungeonCompletionFlow` | ready한 flow에게 travel 요청만 위임 |
| CombatEncounterGroup | encounterId/members/flow | encounter persistence와 start ownership |

## 9. Scene/Prefab 연결 방법

template hierarchy의 `Runtime`에는 RuntimeBootstrapper 하나만, combat/UI는 template instance 하나만 유지한다. PlayerSpawn은 Player root와 camera를 대신 소유하지 않는다. NPC는 interaction prefab/Story UI, 적은 encounter group, UI는 router root 아래에서 기존 참조를 사용한다.

## 10. 기존 시스템 등록 방법

새 Scene을 만드는 것만으로 title/scene travel이 등록되는 것은 아니다. existing `SceneFlowController`/SceneTravel route와 Build Settings가 실제로 해당 scene을 로드하도록 별도 확인한다. 이 문서가 Build Settings/mission destination을 변경하라고 승인하지 않는다.

## 11. Play Mode 검증 방법

Scene load에서 PlayerSpawn/camera/UI/Exploration을 확인한다. 이동과 ground collision, NPC interaction, contact/field attack, quest/reward, exit condition, UI router, scene exit/reentry, Save/Load를 실제로 순서대로 확인한다.

## 12. 통과 기준

중복 runtime owner 없이 scene이 load되고, player/camera/physics/UI/interaction/combat/quest/reward가 기존 flow를 유지하며 completion/exit은 authoring된 조건에서만 작동해야 한다.

## 13. 자주 발생하는 오류

| 증상 | 확인 |
| --- | --- |
| player/camera 이상 | PlayerSpawn, PlayerRoot, CameraFollow2D, duplicate runtime |
| UI/입력 없음 | RuntimeBootstrapper/ProductionDungeonUI/UIScreenRouter ownership |
| exit 잠김 | completion quest ID/status, destination authoring, Exploration state |
| scene 재진입 오류 | encounter/interaction Save state와 SceneFlow restore |

## 14. 구현되지 않은 기능

자동 scene registration, generic dungeon definition SO, procedural tile/nav generation, destination 없는 자동 exit, multi-stage checkpoint UX는 Inspector-only Production 기능으로 확인되지 않았다.

## 15. 수정 시 주의사항

template을 scene hierarchy의 source로 삼되 `Dungeon_1_Production`의 Systems/Demo/Debug/Legacy를 복제하지 않는다. Scene/Prefab/GUID, tile/collider, serialized references 변경 전 반드시 참조를 확인한다.

## 16. 관련 문서 링크

[Dungeon 템플릿](GAME002_DungeonStage_PlanningTemplate.md) · [Dungeon 체크리스트](GAME002_DungeonStage_ValidationChecklist.md) · [Enemy 가이드](GAME002_EnemyEncounter_AuthoringGuide.md) · [기존 Dungeon Template Setup](DungeonTemplateSetup.md)
