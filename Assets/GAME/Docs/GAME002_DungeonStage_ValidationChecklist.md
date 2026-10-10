# GAME_002 Dungeon / Stage 검증 체크리스트

- [ ] `Dungeon_Template`을 복제했고 template/original GUID·runtime owner를 변경하지 않음
- [ ] RuntimeBootstrapper, Combat/UI, EventSystem 등 canonical owner 중복 없음
- [ ] PlayerSpawn/camera/ground collision/encounter/NPC/interaction 참조가 완전함
- [ ] completion quest/destination/exit 정책이 승인 기획과 일치

| 테스트 | 통과 기준 | 결과 |
| --- | --- | --- |
| Scene load | Loading 후 올바른 initial state, PlayerSpawn/camera 정상 | |
| 이동/충돌 | player가 ground/collision에서 정상 이동 | |
| NPC/interaction | prompt·dialogue·input return 정상 | |
| enemy/combat | patrol/contact/field attack와 return 정상 | |
| quest/reward/UI | tracker/reward/router가 기존 owner로 동작 | |
| exit/reentry | 완료 조건과 authoring된 destination에서만 travel | |
| Save/Load | spawn·persistent interaction·encounter/quest가 cold load 뒤 일치 | |
| Validator | `GAME > Production Migration > Validate Dungeon 1 Production Scene`은 Dungeon 1 대상임을 기록 | |

구현 ___ / 컴파일 ___ / Inspector ___ / Play Mode ___ / 통합 ___ / 미검증 ___
