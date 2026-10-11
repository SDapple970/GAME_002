# GAME_002 Item / Reward 제작 가이드

> 최신 구현 대조: 2026-10-11 · Unity 6000.3.23f1 · 저장 schema 12. 기존 XML/완료 보고서를 확인했으며 이번 문서 갱신에서 Unity 테스트를 재실행하지 않았다. 정식 콘텐츠·Scene/Player 검증은 별도로 기록한다.

## 1. 콘텐츠 개요

일반 item은 `ItemDefinitionSO`와 `ItemCatalogSO`로 정의하고 `InventoryService`가 수량/저장을 소유한다. Gold/EXP/item 지급은 `RewardService`가 stable source/action identity의 ledger로 한 번만 적용한다. Shared Film은 item이 아니라 별도 CharacterSkillRuntime 경로다.

## 2. 현재 구현 상태

item ID, 이름/설명/icon, 최대 stack, catalog 검증, inventory save, gold/EXP/item reward, partial grant와 duplicate blocking이 구현돼 있다. FinalExchange의 HP 회복/상태 부여 전투 item 사용은 후속 구현에 포함된다(아래 절). 일반 필드 사용, 판매 가격, item type, 범용 inventory UI는 별도 범위다.

## 3. Production Owner

`InventoryService`가 item 수량을, `CurrencyWallet`이 gold를, `CharacterProgressionService`가 EXP를, `RewardService`가 지급 ledger를 소유한다. Combat/Quest/Interaction/Story는 `RewardGrantRequest`를 만들며 UI는 지급을 재결정하지 않는다.

## 4. 사용 코드/Asset 목록

| 역할 | 실제 타입 / 메뉴 |
| --- | --- |
| item definition | `ItemDefinitionSO` — Create `GAME/NonCombat/Item Definition` |
| catalog | `ItemCatalogSO` — Create `GAME/NonCombat/Item Catalog` |
| inventory | `InventoryService` (`itemCatalog`) |
| reward | `RewardService`, `RewardGrantRequest`, `RewardSourceType` |
| validator | `GAME > Validation > Inventory and Progression` |

## 5. 관련 시스템과 데이터 흐름

```text
ItemDefinitionSO → ItemCatalogSO → InventoryService
Combat / QuestCompletionFlow / StoryEffect / Interaction event
→ RewardService(ledger identity) → CurrencyWallet + InventoryService + Progression
→ existing Reward UI / SaveLoadService
```

## 6. 콘텐츠 제작 준비물

item 임시 ID, 이름/설명/icon, stack limit, 획득처, 지급 source, item/gold/EXP 수량, 중복 정책, 사용 효과 요구를 승인받는다. item ID·reward source/action ID는 담당자가 catalog/ledger와 대조한다.

## 7. 신규 콘텐츠 생성 방법

1. **Create > GAME > NonCombat > Item Definition**으로 SO를 만든다.
2. `itemId`, `displayName`, `description`, `icon`, `maximumStackCount`를 입력한다. 0은 무제한 호환 의미다.
3. **Create > GAME > NonCombat > Item Catalog** 또는 existing catalog에 item을 추가한다. `InventoryService.itemCatalog`가 이 catalog를 참조하는지 확인한다.
4. 지급은 owner별 기존 request로 연결한다. Quest는 definition reward gold/EXP, StoryEffect는 `GrantReward`, Interaction은 Production reward event를 사용한다. arbitrary MonoBehaviour에서 wallet/inventory를 직접 바꾸지 않는다.

## 8. Inspector 필드 설명

| 대상 | 필드 | 의미 |
| --- | --- | --- |
| ItemDefinitionSO | `itemId` | catalog/save의 고유 stable ID |
| ItemDefinitionSO | `displayName`, `description`, `icon` | 표시 메타데이터 |
| ItemDefinitionSO | `maximumStackCount` | 0=무제한, 양수=per-item cap |
| ItemCatalogSO | `items` | 가능한 item definitions. ID 중복/빈 값 금지 |
| RewardService | wallet/inventory/progression refs | service wiring; 콘텐츠가 교체하지 않음 |
| StoryEffect reward | `rewardSourceId`, gold/EXP/item ID/count | stable reward identity와 지급 값 |

## 9. Scene/Prefab 연결 방법

새 item은 Scene object가 아니라 catalog에 연결한다. reward UI/Toast는 기존 scene wiring을 재사용한다. reward 지급을 위해 새로운 UI, service, inventory instance를 만들지 않는다.

## 10. 기존 시스템 등록 방법

catalog 등록 뒤 `InventoryService.TryAddItem`이 resolved definition을 찾을 수 있다. RewardService는 source type/source ID/action ID가 유효해야 한다. 동일 canonical identity는 ledger가 duplicate로 차단하므로 retry를 위해 ID를 임의 변경하지 않는다.

## 11. Play Mode 검증 방법

각 source에서 지급해 count/gold/EXP를 확인한다. stack limit, unknown ID, duplicate identity, partial capacity를 확인하고 Save → cold load 후 inventory/wallet/ledger를 비교한다.

## 12. 통과 기준

정의된 item만 수량이 증가하고 stack cap을 넘지 않으며, reward source가 한 번만 지급되고 Save/Load 뒤 값이 일치해야 한다.

## 13. 자주 발생하는 오류

| 증상 | 확인 |
| --- | --- |
| UnknownDefinition | catalog에 item이 없거나 itemId가 다름 |
| partial/stack limit | `maximumStackCount`, 현재 count |
| 보상 미지급 | RewardService wiring, source/action identity, target progression |
| 보상 중복 차단 | 같은 canonical ledger identity가 이미 지급됨 |

## 14. 구현되지 않은 기능

일반 필드 item 사용, 장비, 가격/판매/상점 정책, rarity/type field, item reward editor UI는 이 절에서 보장하지 않는다. 전투 RestoreHp/ApplyStatus 소비 효과는 아래 후속 지원 범위를 따른다.

## 15. 수정 시 주의사항

기존 item ID/catalog/Reward source ID는 Save와 ledger에 영향이 있다. item을 Film으로 대체 설명하거나 UI가 RewardService 소유권을 갖게 하지 않는다.

## 16. 관련 문서 링크

[Item/Reward 템플릿](GAME002_ItemReward_PlanningTemplate.md) · [검증 체크리스트](GAME002_ItemReward_ValidationChecklist.md) · [Quest 가이드](GAME002_Quest_AuthoringGuide.md) · [공통 표준](GAME002_ContentProductionStandards.md)

## 2026-10-11 전투 아이템 제작 및 연결
1. 기존 ItemDefinitionSO/ItemCatalogSO에 stable inventory itemId를 등록한다. 수량 Owner는 InventoryService다.
2. **Create > Game > Combat > Item Definition**으로 `CombatItemDefinitionSO`를 만든다. `itemId`, `displayNameOverride`, `targetRule(Self/SingleAlly)`, `effectKind(RestoreHp/ApplyStatus)`, `hpRecovery` 또는 `appliedStatus`를 입력한다.
3. `CombatItemCatalogSO`에 Definition을 등록한다. `CombatItemUseAdapter`의 `flowOrchestrator`, `inventoryService`, `combatItemCatalog`를 명시 연결한다. Production Scene/Prefab 배치는 별도 authoring 작업이다.
4. FinalCombatUIBinder → FinalCombatPlayerCommandController → CombatFlowOrchestrator의 기존 명령 경로를 사용한다. Executor는 Inventory 수량/효과 연결만 하며 전투 phase Owner가 아니다.
5. 올바른 현재 명령·생존 아군·실제 roster·대상/효과 조건을 검증한 뒤 1개 차감한다. stale/중복/잘못된 대상/회복 불필요는 소비하지 않고 효과 실패 시 반환을 시도한다. Panic 사용자는 기존 eligibility로 거부한다.
6. 격리 Editor EnterPlayMode의 동일 Item 테스트는 최신 CBT_PARTY_05_Regression.xml에서 PASS다. 이전 CBT_ITEM_01_PlayMode_Final.xml은 FAIL이므로 이름만 보고 통과로 쓰지 않는다. 실제 Production Catalog/Scene 및 디스크 재시작은 별도로 검증한다.
전투 사용 2종을 일반 필드 사용·장비·상점·희귀도·범용 Inventory UI 완료로 확대하지 않는다.
