# GAME_002 Item / Reward 검증 체크리스트

> 최신 구현 대조: 2026-10-11 · Unity 6000.3.23f1 · 저장 schema 12. 기존 XML/완료 보고서를 확인했으며 이번 문서 갱신에서 Unity 테스트를 재실행하지 않았다. 정식 콘텐츠·Scene/Player 검증은 별도로 기록한다.

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

## 전투 사용·다인 EXP 추가 검수 (2026-10-11)
- [ ] Inventory/Combat Catalog의 stable itemId가 일치한다.
- [ ] Adapter → 기존 FinalExchange command 경로를 사용한다.
- [ ] RestoreHp/ApplyStatus 성공에서 수량 1회 차감/효과 1회, invalid/stale/full HP/Panic/중복에서 무소비를 확인한다.
- [ ] 효과 실패 시 inventory 반환과 상태·phase 일관성을 확인한다.
- [ ] Save/Load 후 수량이 복원된다. 정식 배치·디스크 재시작은 별도 증거를 남긴다.
- [ ] 전투 시작 참가자 전원 각각 TotalExp 전액, KO 포함/미참가 제외를 확인한다.
- [ ] 동일 Completion에서 Gold/Item/EXP 중복 지급이 없고 부분 EXP 실패는 미정산 수령자만 재시도한다.
- [ ] schema 12의 수령자 ledger JSON/DTO 복원과 실제 디스크/프로세스 검증을 구분한다.
