# GAME_002 Story Event / Cutscene 검증 체크리스트

> 최신 구현 대조: 2026-10-11 · Unity 6000.3.23f1 · 저장 schema 12. 기존 XML/완료 보고서를 확인했으며 이번 문서 갱신에서 Unity 테스트를 재실행하지 않았다. 정식 콘텐츠·Scene/Player 검증은 별도로 기록한다.

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

## Flag 저장 추가 검수 (2026-10-11)
- [ ] canonical StoryFlagManager만 저장 provider/consumer로 참여한다.
- [ ] bool false 및 signed int, typed key/존재 여부를 Save→Load 후 확인한다.
- [ ] 반복 Restore가 잔존 값을 남기지 않고 Story 효과/Reward를 재실행하지 않는다.
- [ ] New Game reset과 Continue 복원을 구분한다.
- [ ] graph 중간 resume·범용 Video/Timeline checkpoint를 별도 미검증/기능 요청으로 기록한다.
