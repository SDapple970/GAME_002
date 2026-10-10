# GAME_002 Enemy / Encounter 검증 체크리스트

- [ ] 기획 승인, 필요 sprite/animation/SFX/수치 준비
- [ ] enemy HP/loadout/motor/patrol/collider/encounter group 연결
- [ ] EnemyDefinition/EnemySource는 Film 기획이 승인된 경우만 추가
- [ ] Film skill은 Registry에 있고 mapping은 Film key만 사용
- [ ] `GAME > Validation > Enemy Skill Acquisition` PASS
- [ ] 필요 시 `GAME > Validation > Combat Skill Persistent Identities` PASS

| Play Mode 테스트 | 통과 기준 | 결과 |
| --- | --- | --- |
| spawn/ground/patrol | 바닥 충돌과 Exploration-only 이동 정상 | |
| Contact / Field Attack | 둘 다 canonical CombatEntryPoint에서 한 번만 시작 | |
| 전투/loadout | 적 skill 선택/실행과 종료 흐름 정상 | |
| Victory/Film | 실제 defeated mapped source만 공유 Film 획득 | |
| defeat/abort/dedupe | Film/reward/quest 중복 또는 오지급 없음 | |
| Quest/Reward/Exploration | 기존 owner 경유, 종료 후 정상 복귀 | |
| encounter save/load | cleared/rearm 상태가 cold load 뒤 일치 | |

구현 상태 ___ / 컴파일 ___ / Inspector ___ / Play Mode ___ / 통합 ___ / 미검증 ___
