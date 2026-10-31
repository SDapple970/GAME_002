# CBT-R2-08-P2B3D-4D — 천사 Playtest V1 전투 콘텐츠

승인된 The Prophecy Skill SO를 제작하고 실제 아난 스킬과 기존 Enemy AI로 격리 전투를 실행했다. 천사 Prefab 후보는 순찰/배치 및 표현 참조가 확정되지 않아 생성하지 않았다. 실제 Dungeon 연결이나 Cutover는 수행하지 않았다.

## 신규 스킬 및 기존 source

`Assets/GAME/Data/Skill/Skill_AngelTheProphecy_PlaytestV1.asset`, Unity GUID `d58afbdd7afcab242878d3b1354d03ea`. 기존 SkillDefinitionSO를 사용하고 skillId **10003**, persistentKey **skill.playtest.angel.prophecy**를 배정했다. 전역 Skill asset 중복 검사 통과. Final MP1, Clash Power2, Base Damage3, Attack/SingleEnemy, Compatibility, Unique owner 없음, 상태 효과 없음이다. Hand kind는 SO가 아니라 Hand Config 소유이며 천사 Hand는 opt-out이다.

명중75·치명 보정3%는 적용하지 않았다. baseStagger/weaknessStaggerBonus0, Animator trigger 빈 문자열 및 VFX/SFX null이다. SkillDefinition의 `speed=5`는 기존 타입 기본값이며 **승인된 천사 개체 속도9를 뜻하지 않는다**. 이 Skill speed는 합 동시 소진 tie에 영향을 줄 수 있으므로 제작 확정 전 별도 결정이 필요하다.

기존 `Assets/GAME/Data/Enemy/Enemy_Angel.asset`의 `enemy.chapter1.angel`, GUID, 빈 Film 목록과 null Profile은 그대로다. EnemyDefinition은 provenance/획득 source이며 HP나 전투 loadout이 아니다. 기존 Angel_Skill/Angel_Skill2 및 모든 기존 자산은 유지했다.

## Prefab 및 스탯

천사 Prefab은 **미생성 / Field placement·presentation Pending**이다. 기존 `Assets/GAME/Prefabs`에서 재사용 가능한 천사/적 Prefab을 확인하지 못했다. 현 Production Dungeon은 Enemy_01~03을 Scene 객체로 두고 HP10·loadout11/12를 사용한다. 새 후보에 필요한 patrol 양 끝점, player/encounter 연결과 위치, Collider 설정 및 실제 animation binding은 승인/검증된 제작값이 없다. `FieldEnemyPatrolAI2D`는 patrol 지점이 없으면 경고 후 정지하므로 빈 참조를 가진 불완전한 Prefab을 완성품처럼 저장하지 않았다.

격리 테스트의 임시 천사 GameObject에는 기존 CombatHpComponent의 HP/maxHP14, CombatSkillLoadoutComponent의 ID10003, EnemySourceComponent의 기존 Enemy_Angel 참조를 설정했다. 이는 저장된 Prefab/Scene 연결이 아니다. 로컬 `Assets/GAME/Image/Enemy/angel/`의 sprite·animation/controller는 존재하나 이번에 실제 객체에 연결하거나 시각 검증하지 않았다.

| 승인 Playtest 값 | 현재 적용/제약 |
|---|---|
| HP14 | 기존 HP 컴포넌트로 격리 객체에서 적용/검증. 저장된 Prefab 없음 |
| 회피20%, 방어0, 개체 속도9 | FieldCombatantAdapter/CombatSession의 해당 개체 스탯 authoring·적용 경로를 확인하지 못함. 임의 필드/공식 추가 없음 |
| 스킬 속도 | 위의 개체 속도와 별개인 기존 SkillDefinition.speed 기본5. 9를 임의 대입하지 않음 |
| 초기 MP2 | 기존 Playtest Config의 Session 설정을 사용 |

## 격리 전투와 실행 범위

`Assets/GAME/Tests/Editor/AngelProphecyPlaytestTests.cs`(GUID `6f8f0a36988ba464d8033220086bc565`)는 빈 Scene에서 EnterPlayMode를 사용하고 기존 EntryPoint·Session·Orchestrator·FinalCombatEnemyCommandController 및 기존 AI 정책을 사용한다. 아난의 실제 Normal/촌하우 SO와 The Prophecy, 공식 아난 ID, 기존 Enemy_Angel source를 일시적인 요청/객체에 공급한다. 기존 TestOnly Hand Config 복제본의 Normal2/Clash3 copies는 **fixture만을 위한 수량**, 아난/적 초기 MP는 2이며 Enemy Hand는 비활성이다.

- 적 선공: 기존 AI가 유일한 실제 Prophecy를 선언하고 아난이 실제 촌하우로 최초 대응. 양측 MP2→1, 중복 대응 거부 및 첫 비교 확인. HP14와 적 Hand 미적용 확인.
- 아군 선공: 실제 기본 공격 MP0으로 선언, 기존 적 AI가 Prophecy로 대응해 적 MP2→1. 다음 촌하우/Prophecy 비교 후 적 MP0이며, 이후 적은 유료 Prophecy로 다시 대응하지 못한다. 아난 최종 피해4로 적 HP14→10, ChainDecision 진입 및 중복 요청 거부.
- 적 선공 테스트에서 Abort 후 Session 정리와 timeScale1/fixedDeltaTime 보존을 확인했다. 실제 대상 적 Prefab·FieldLock, 승리/패배 CombatResult 및 양측 실제 스킬을 사용한 Terminal은 **직접 미검증**이다.
- 두 테스트 모두 Director presentation gate를 수동 완료한다. 실제 Director 연출/애니메이션 완료 신호 및 Terminal 이후 복귀는 **미검증**이다. 기존 fixture의 Director/Terminal 회귀는 이 신규 콘텐츠의 실제 연출 검증을 대체하지 않는다.
- 별도의 아난 MP 부족 대응·stale token 및 FieldLock 복구는 D4C2/D2 관련 기존 fixture 회귀로 확인하며, 이번 천사 SO 조합에 대해 직접 입증한 범위와 구분한다.

Unity 6000.3.23f1 선택 회귀 **874 PASS / 6 FAIL / 1 IGNORED**, 총881 (`Logs/CBT-R2-08-P2B3D4D-Regression.xml/.log`, filter `...-TestFilter.txt`). 직전 D4C2와 실패 이름이 같은 Dungeon Hand Debug 선행 gate5건 및 Template 상속 BP Debug gate1건이며 **Asset Change Required** 유지다. 별도 Editor 재시작 Save 검사1건은 Ignored. Legacy 관련 220/220 PASS. 기존 Skill/Enemy/Hand/Clash 회귀와 격리 Director fixture를 포함한다.

회귀 실행 후 새 테스트에 source key snapshot 및 첫 비교의 **Prophecy2 대 촌하우3, 공격자 강인도3→2·대응자3 유지** assertion을 추가했고, 최종 집중 검사 **3/3 PASS** (`...-Focused3.xml/.log`). 회귀 수치는 이 추가 assertion 이전 실행이며, 최종 테스트 코드는 집중 검사로 재컴파일·재실행했다. C# 오류/경고 없음. 전체 회귀·실제 Production Scene Play Mode·Player Build·디스크 재시작은 수행하지 않았다.

## 작업 및 Inspector 후속

루트 AGENTS.md 적용, 중첩 없음. branch main / HEAD `b42544824838e7de0e70b71afc2ca21a59db8192`. 시작 tracked/untracked 변경을 보존했다. 추가: 새 Skill SO/meta, 신규 Editor 테스트/meta, 이 보고서. 기존 Runtime 공개 API/SerializeField·GUID, Scene/Prefab/SO, hero.test, Quest/Reward/Save 및 Unity/Package 버전 변경 없음.

시작 기록 기존 파일 **2,630개 해시 동일**, 새 GUID2개 중복 없음. 신규 텍스트 공백 검사와 `git diff --check` 통과(기존 Git 줄바꿈 안내 별도). commit/push/PR 없음.

향후 별도 승인 후: (1) 실제 적 root의 CombatHpComponent HP/maxHP14, CombatSkillLoadoutComponent에10003, EnemySourceComponent.definition에 기존 Enemy_Angel 설정. (2) 기존 CombatEntryPoint.skillDefinitions에 Prophecy와 아난 두 Skill을 중복 없이 등록. (3) 실제 Rigidbody2D/collider, motor/patrol 지점·player·encounter 및 시각/Animator 참조를 확정. (4) Enemy Hand opt-out과 MP2, 정상 UI/Input/Director·FieldLock, Victory/Defeat/Abort 및 Reward/EXP를 Inspector와 실제 Play Mode에서 검증. 기존 TestOnly Hand fixture를 Production에 직접 연결하지 않는다.

천사 Film mapping, 실제 deck copies/seed, 표현 자료 및 회피/방어/속도 적용은 Pending이다. 기존6개 Debug gate와 05/06A 수동 보류를 유지한다. D4E는 **격리 데이터 검토 준비 가능**, 실제 Production 전환 단계로의 진입은 Prefab/참조·미지원 스탯·콘텐츠·수동 검증 및 별도 자산 변경 승인이 남아 차단된다.

## Development tracking

- System: Combat Enemy / Chapter1 Playtest. Owner: CombatEntryPoint, Session, StateMachine, Orchestrator, 기존 Enemy AI와 SkillRunner/Outcome.
- Implementation: Prophecy SO 작성 완료, HP/loadout/source는 격리 구성만 검증. Prefab 후보·스탯 경로·Production 연결 Pending.
- Validation: 실제 실행 결과 아래 기록. 실제 Inspector/Director/Production Play Mode/Integration 미검증.
- Recommended: **Enemy Skill Data Implemented — Prefab / Stats / Presentation / Production Wiring Pending**.
