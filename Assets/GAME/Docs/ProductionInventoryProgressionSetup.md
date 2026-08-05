# Production Inventory and Character Progression

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
