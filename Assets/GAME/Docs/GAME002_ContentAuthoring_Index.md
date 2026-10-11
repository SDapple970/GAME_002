# GAME_002 콘텐츠 제작 문서 인덱스

> 최신 구현 대조: 2026-10-11 · Unity 6000.3.23f1 · 저장 schema 12. 기존 XML/완료 보고서를 확인했으며 이번 문서 갱신에서 Unity 테스트를 재실행하지 않았다. 정식 콘텐츠·Scene/Player 검증은 별도로 기록한다.

> 문서 상태: DOC-CONTENT-01에서 현재 repository 코드·asset 참조를 조사해 작성. 문서 링크/구조와 Unity import/compile은 검증 대상이며, 각 콘텐츠의 Play Mode 검수는 해당 체크리스트에서 별도로 기록한다.

## 공통

| 문서 | 대상 | Production owner/연결 | 검증 |
| --- | --- | --- | --- |
| [공통 제작 표준](GAME002_ContentProductionStandards.md) | 전원 | Core/UI/Save/ID 경계 | 문서 구조/링크 |
| [기존 Skill 가이드](GAME002_SkillContent_AuthoringGuide.md) | 기획/Unity | CombatEntryPoint, Film/Unique | skill validator + Play Mode 별도 |
| [Skill 템플릿](GAME002_SkillContent_PlanningTemplate.md) | 기획 | Skill data | 복사 가능 |
| [Skill 체크리스트](GAME002_SkillContent_ValidationChecklist.md) | Unity/QA | Combat/Save | checklist 증거 |

## NPC / Dialogue / Choice

| 가이드 | 템플릿 | 체크리스트 | owner/Asset | validator |
| --- | --- | --- | --- | --- |
| [가이드](GAME002_NPCDialogue_AuthoringGuide.md) | [기획](GAME002_NPCDialogue_PlanningTemplate.md) | [검증](GAME002_NPCDialogue_ValidationChecklist.md) | InteractionRunner, StoryEventRunner, `ProductionNpcInteraction.prefab` | `GAME > Validation > Validate Production Interactions` |

현재 지원: Story node/choice/timed choice/StoryEffect. 미확정: generic NPC DB, dialogue resume/localization import/custom event effect.

## Quest / Mission

| 가이드 | 템플릿 | 체크리스트 | owner/Asset | validator |
| --- | --- | --- | --- | --- |
| [가이드](GAME002_Quest_AuthoringGuide.md) | [기획](GAME002_Quest_PlanningTemplate.md) | [검증](GAME002_Quest_ValidationChecklist.md) | QuestRuntime, tracker, completion flow, QuestDefinitionSO | related scene/compile; Inventory/Progression validator for reward dependencies |

현재 지원: QuestDefinition objective/event/reward/save. Legacy: Mission/DemoMission. 미확정: item objective publisher, repeated completed production quest UX.

## Enemy / Encounter

| 가이드 | 템플릿 | 체크리스트 | owner/Asset | validator |
| --- | --- | --- | --- | --- |
| [가이드](GAME002_EnemyEncounter_AuthoringGuide.md) | [기획](GAME002_EnemyEncounter_PlanningTemplate.md) | [검증](GAME002_EnemyEncounter_ValidationChecklist.md) | CombatEncounterGroup/Trigger, CombatEntryPoint, EnemyDefinitionSO | `GAME > Validation > Enemy Skill Acquisition` |

현재 지원: contact/field attack/victory Film mapping. 미확정: approved production Enemy→Film mapping, random drop/spawn table.

## Item / Reward

| 가이드 | 템플릿 | 체크리스트 | owner/Asset | validator |
| --- | --- | --- | --- | --- |
| [가이드](GAME002_ItemReward_AuthoringGuide.md) | [기획](GAME002_ItemReward_PlanningTemplate.md) | [검증](GAME002_ItemReward_ValidationChecklist.md) | ItemDefinition/Catalog, InventoryService, RewardService | `GAME > Validation > Inventory and Progression` |

현재 지원: catalog/stack/save/ledger reward, FinalExchange RestoreHp/ApplyStatus 전투 item 사용. 별도 범위: 일반 필드 item use, shop price/type/rarity, generic inventory UI.

## Dungeon / Stage

| 가이드 | 템플릿 | 체크리스트 | owner/Asset | validator |
| --- | --- | --- | --- | --- |
| [가이드](GAME002_DungeonStage_AuthoringGuide.md) | [기획](GAME002_DungeonStage_PlanningTemplate.md) | [검증](GAME002_DungeonStage_ValidationChecklist.md) | `Dungeon_Template`, SceneFlow, RuntimeBootstrapper, completion flow | `GAME > Production Migration > Validate Dungeon 1 Production Scene` (Dungeon 1 전용) |

현재 지원: template-based scene, completion contract. 미확정: generic dungeon definition/automatic Build registration/procedural staging.

## Story Event / Cutscene

| 가이드 | 템플릿 | 체크리스트 | owner/Asset | validator |
| --- | --- | --- | --- | --- |
| [가이드](GAME002_StoryEventCutscene_AuthoringGuide.md) | [기획](GAME002_StoryEventCutscene_PlanningTemplate.md) | [검증](GAME002_StoryEventCutscene_ValidationChecklist.md) | StoryEventRunner, StoryEventDefinitionSO, StoryDialogueHUD | Production Interaction validator for start source |

현재 지원: Story node/choice/effect. 일부: MissionComplete VideoPlayer cutscene compatibility. 미구현 Production standard: Story Event→Timeline/Video cutscene.

## 관련 기존 문서

- [Dungeon Template Setup](DungeonTemplateSetup.md): hierarchy/wiring reference.
- [Production Interaction Setup](ProductionInteractionSetup.md): historical setup detail; current code and this index take priority where they differ.
- [Narrative Production Setup](NarrativeProductionSetup.md): historical reference, not new authoring authority.
- `Assets/GAME/Docs/Architecture/`의 reports: 감사/이력 자료이며 새로운 콘텐츠 제작 절차의 source of truth가 아니다.

## 유지관리

새 Create menu, serialized field, owner, Scene path 또는 validation menu가 변경될 때 해당 분야 가이드·템플릿·체크리스트·이 인덱스를 한 변경으로 갱신한다. 각 새 콘텐츠는 checklist 사본과 실제 validation evidence를 함께 남긴다.

## 2026-10-11 현행 구현 반영
- Save: schema 12. B01 bool/int Story Flag, Party 참가자별 EXP ledger가 추가돼 있다. schema 10/11 언급은 해당 Batch 당시 검증 이력이다.
- Combat: FinalExchange Production 경로, 선택적 1~3 Party roster, 참가자별 전액 EXP(KO 포함), Status damage/clash/posture 및 duration, 전투 Item 2종을 지원한다.
- 세부 절차는 업데이트된 Skill/EnemyEncounter/ItemReward/StoryEventCutscene/DungeonStage 가이드를 따른다. 기존 Owner를 복제하지 않는다.
- 최신 전체 XML은 1343/1343 PASS이며 일부 Editor EnterPlayMode 테스트를 포함한다. 이번 문서 수정에서 재실행하지 않았다. 정식 콘텐츠·Scene/Player·신규 다인 디스크 재시작은 별도다.
- 현행 통합 연결: [GAME_002_PRODUCTION_GUIDE](https://docs.google.com/document/d/1-eHB2QEf9RuW6KK_7UM4zCR1YhOJuqzJTTsAW_Fk-Jo/edit).
