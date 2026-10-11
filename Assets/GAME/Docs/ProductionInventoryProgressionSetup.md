# Production Inventory and Character Progression

> 2026-10-11 문서 역할 갱신: 아래 본문은 기존 상세 Setup/호환성·당시 검증 참고로 보존한다. 현재 Production 연결은 [통합 Production Guide](https://docs.google.com/document/d/1-eHB2QEf9RuW6KK_7UM4zCR1YhOJuqzJTTsAW_Fk-Jo/edit)와 [콘텐츠 제작 인덱스](GAME002_ContentAuthoring_Index.md)를 우선한다. 현행 Unity 6000.3.23f1, FinalExchange, SceneTravel→SceneFlow 위임, 저장 schema 12, B01 Flag/Party EXP 확장을 확인했다. 아래 옛 Planning·schema·기본 EXP target·미구현 설명을 현행 상태로 그대로 사용하지 않는다. 정식 콘텐츠/Scene/Player 검증은 별도로 남긴다.

`RuntimeBootstrapper` installs services in this dependency order: `CurrencyWallet`, `InventoryService`, `CharacterProgressionService`, interaction services, `SaveLoadService`, `RewardService`, then UI. Existing authored instances are adopted; scenes do not need service components added manually.

## Ownership and content

- `InventoryService` alone owns item quantities. Normalize stable IDs by trimming whitespace. New Production content should use lowercase dotted IDs such as `item.consumable.bandage` and an authored `ItemCatalogSO`. A service with no catalog retains arbitrary string-ID compatibility.
- `ItemDefinitionSO.maximumStackCount` is optional. Zero means unlimited, preserving existing assets. Positive limits allow partial additions. Use `TryAddItem`, `TryRemoveItemDetailed`, `CanAdd`, `GetCount`, and `GetSnapshot`; UI subscribes to read-only change/refresh events.
- `CurrencyWallet` alone owns Gold. `TryAddGold` rejects overflow, spending is all-or-nothing, and restore emits only `Refreshed`.
- `CharacterProgressionService` alone owns character level and EXP. Each `CharacterProgressionDefinitionSO` uses a stable authored character ID, starting/max level, and positive per-level XP requirements. Missing curve entries repeat the last requirement. Maximum-level targets settle without retaining unusable EXP.
- `PersonaStatusManager` remains the separate social-stat owner. `PersonaSaveAdapter` only bridges its existing public state to canonical saves.

## Rewards and compatibility

`RewardGrantRequest.ProgressionTargetId` is consequence metadata and never changes `GameplayOutcomeIdentity.CanonicalId`. If omitted, `CharacterProgressionService.defaultRewardTargetId` is used. This repository currently has no safe authored protagonist identity, so leave the default empty until the project authors one; EXP remains pending instead of inventing an identity.

On Schema 5 to 6 migration, `PartySaveData.memberLevels` seed character level with zero EXP. The Party fields remain membership/compatibility mirrors, not a second writable level owner. Reward ledger `progressionTargetId` and `expSettled` allow requested-but-unapplied EXP to reconcile after progression restores. Reconciliation never calls Gold or Inventory and never presents normal reward UI.

Do not use `SupplyLoadoutService`, Search local counters, Shop/demo inventory data, Persona XP, or combat component level fields as new Production inventory/progression owners.

## Manual validation

1. Add one catalog and progression definition to authored service instances, using unique non-empty IDs and a positive XP curve.
2. Set the progression service default reward target to that character ID.
3. Enter Play Mode and confirm one Wallet, Inventory, and Progression service survives additive scene/bootstrap calls.
4. Grant a reward near an item stack limit and verify only the applied item count appears.
5. Save after a level-up, change runtime values, load, and verify level/EXP restore without reward or level-up presentation replay.
6. Load a Schema 5 save containing pending EXP twice; verify only EXP changes on the first load and Gold/items never change.
