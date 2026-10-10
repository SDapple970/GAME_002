# GAME_002 Quest 검증 체크리스트

## 제작·연결

- [ ] 기획 승인, objective와 target/수량/순서/optional 확정
- [ ] QuestDefinition SO의 questId/objectiveId가 비어 있거나 중복되지 않음
- [ ] `QuestRuntime.questDefinitions`에 등록, 기존 runtime/flow/publisher 재사용
- [ ] 시작/Combat/Interaction/Story publisher가 objective event와 target에 맞음
- [ ] reward Gold/EXP와 Retry policy가 승인 기획과 일치

## 검증

| 테스트 | 통과 기준 | 결과 |
| --- | --- | --- |
| compile / serialized reference | 컴파일 오류·Missing reference 없음 | |
| quest 시작 | 정의 한 번만 active가 되고 HUD가 갱신 | |
| objective progress | 실제 이벤트가 해당 active objective만 한 번 진행 | |
| 순서·optional | group 및 optional 정책이 기획대로 적용 | |
| completion/reward | 완료 한 번, RewardService ledger가 중복 지급 차단 | |
| Combat/Interaction/Story 통합 | 다른 quest/flow에 직접 상태 write 없음 | |
| Save/Load | active/completed/failed·progress·reward ledger가 cold load 뒤 일치 | |

알려진 미검증: ___ / 구현 상태: ___ / Inspector: ___ / Play Mode: ___ / 통합: ___
