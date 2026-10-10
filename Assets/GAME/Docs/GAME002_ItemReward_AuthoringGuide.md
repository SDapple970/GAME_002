# GAME_002 Item / Reward 제작 가이드

## 1. 콘텐츠 개요

일반 item은 `ItemDefinitionSO`와 `ItemCatalogSO`로 정의하고 `InventoryService`가 수량/저장을 소유한다. Gold/EXP/item 지급은 `RewardService`가 stable source/action identity의 ledger로 한 번만 적용한다. Shared Film은 item이 아니라 별도 CharacterSkillRuntime 경로다.

## 2. 현재 구현 상태

item ID, 이름/설명/icon, 최대 stack, catalog 검증, inventory save, gold/EXP/item reward, partial grant와 duplicate blocking이 구현돼 있다. Item 사용 효과, 판매 가격, item type, inventory UI는 이 문서가 Inspector-only로 보장하지 않는다.

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

item 사용/장비/소비 효과, 가격/판매/상점 정책, rarity/type field, item reward editor UI는 현재 data fields로 제공되지 않는다.

## 15. 수정 시 주의사항

기존 item ID/catalog/Reward source ID는 Save와 ledger에 영향이 있다. item을 Film으로 대체 설명하거나 UI가 RewardService 소유권을 갖게 하지 않는다.

## 16. 관련 문서 링크

[Item/Reward 템플릿](GAME002_ItemReward_PlanningTemplate.md) · [검증 체크리스트](GAME002_ItemReward_ValidationChecklist.md) · [Quest 가이드](GAME002_Quest_AuthoringGuide.md) · [공통 표준](GAME002_ContentProductionStandards.md)
