# GAME_002 Story Event / Cutscene 검증 체크리스트

- [ ] event/node/choice/flag/reward source ID와 기획 분기 승인
- [ ] Story Event SO graph에 missing next node, duplicate/empty required ID 없음
- [ ] start source가 existing StoryInteractionEventSO/runner/UI를 사용
- [ ] StoryEffect가 지원 enum 결과만 사용하고 owner를 직접 교체하지 않음

| 테스트 | 통과 기준 | 결과 |
| --- | --- | --- |
| event trigger | Exploration에서 한 번만 정상 시작 | |
| node/choice | 모든 condition·hidden/disabled·next branch 정상 | |
| timed choice | 선택과 timeout 경로가 승인 결과로 한 번 적용 | |
| effects | flag/Quest/Reward가 correct owner/identity로 적용 | |
| 종료/input/UI | Story UI가 닫히고 state/input이 정상 복귀 | |
| Save/Load | Quest/Reward owner가 저장하는 결과는 cold load 뒤 유지. flag는 실제 save participant가 확인된 경우에만 주장 | |
| video 요구 | legacy Mission controller라면 별도 wiring/Play Mode 증거 기록 | |

컴파일 ___ / Inspector ___ / Play Mode ___ / 통합 ___ / 미구현/미검증 ___
