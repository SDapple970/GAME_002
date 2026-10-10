# GAME_002 콘텐츠 제작 문서 인덱스

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

현재 지원: catalog/stack/save/ledger reward. 미구현 표준: item use, shop price/type/rarity, generic inventory UI.

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
