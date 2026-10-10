# GAME_002 Item / Reward 검증 체크리스트

- [ ] 기획 승인 및 icon/이름/설명/stack 정책 준비
- [ ] itemId가 비어 있지 않고 catalog 내 중복 없음
- [ ] item이 `ItemCatalogSO.items`와 existing InventoryService catalog에 등록
- [ ] reward source/action identity와 item ID/count가 승인 기획과 일치
- [ ] `GAME > Validation > Inventory and Progression` 실행

| 테스트 | 통과 기준 | 결과 |
| --- | --- | --- |
| item 획득 | valid catalog item만 inventory에 증가 | |
| stack/partial | cap과 partial result가 예상대로 | |
| Gold/EXP/item reward | RewardService가 wallet/inventory/progression에 적용 | |
| duplicate | 같은 identity는 한 번만 지급 | |
| UI/flow | 기존 reward UI/state ownership 유지 | |
| Save/Load | inventory, currency, progression, ledger가 cold load 뒤 일치 | |

컴파일 ___ / Inspector ___ / Play Mode ___ / 통합 ___ / 미검증 ___
