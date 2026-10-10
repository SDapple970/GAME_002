# GAME_002 NPC / Dialogue / Choice 검증 체크리스트

## 제작·Inspector

- [ ] 기획 승인 및 필요한 portrait/sprite/SFX 준비
- [ ] Story Event / Story Interaction Event SO 생성, event ID와 start node 확인
- [ ] NPC Collider2D는 trigger이며 `InteractableObject`가 있다
- [ ] Production 정책과 interaction/action identity가 기획과 일치한다
- [ ] `events`에는 Production 실행 지원 Story event만 연결했다
- [ ] Story node/choice ID, 다음 노드, condition/effect 참조에 Missing이 없다
- [ ] 기존 NPC/Scene/Prefab/GUID를 덮어쓰지 않았다

## 자동 검증

| 테스트 | 통과 기준 |
| --- | --- |
| `GAME > Validation > Validate Production Interactions` | Persistent interaction ID/action ID 중복, Legacy-only event, NPC trigger/Story 연결 오류가 없다 |
| `Tools > GAME > Validate Production UI Routing` | 대화/상호작용 UI routing 오류가 없다 |
| 컴파일 | 새 문서 외 변경이 없고 프로젝트 컴파일 오류가 없다 |

## Play Mode

| 테스트 | 통과 기준 | 결과 |
| --- | --- | --- |
| NPC 접근/prompt | 범위 진입 시 Exploration에서만 prompt 표시 | |
| 실제 입력 시작 | InputService 경유로 대화가 시작 | |
| line/choice | 순서·조건·숨김/비활성·결과가 기획과 일치 | |
| timed choice | 선택과 timeout 기본 경로가 모두 한 번만 적용 | |
| 종료/입력 복귀 | UI가 닫히고 Exploration/상호작용이 정상 복귀 | |
| Quest/Reward | 연결된 effect가 한 번만 전달되고 기존 흐름 유지 | |
| Save/Load | PersistentOnce 상태가 필요한 경우 cold load 뒤 유지 | |

기록: 구현 상태 ___ / Inspector ___ / Play Mode ___ / 통합 ___ / 알려진 미검증 ___
