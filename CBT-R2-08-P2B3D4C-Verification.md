# CBT-R2-08-P2B3D-4C — 아난 초기 스킬 Playtest V1

승인된 초기 스킬을 기존 SkillDefinitionSO로 제작하고 실제 신규 SO를 사용하는 격리 전투를 검증했다. 보호 Scene/Prefab/SO·Character SO·기존 Registry는 변경하지 않았으며 Production 초기화는 비활성이다.

## 실제 자산과 필드

| 자산 | skillId / persistentKey | 분류·소유 | Final MP / 합 위력 / 피해 | GUID |
|---|---|---|---|---|
| `Assets/GAME/Data/Skill/Skill_AnandBasic_PlaytestV1.asset` | 10001 / `skill.playtest.anand.basic` | Normal 예정 / Compatibility | 0 / 기존2 / 기존2 | `1b95a1b239871014c9e046b94233537e` |
| `Assets/GAME/Data/Skill/Skill_Zornhau_PlaytestV1.asset` | 10002 / `skill.playtest.anand.zornhau` | Clash 예정 / Compatibility | 1 / 3 / 4 | `0e7bed18e4f4da84494e294e444296bb` |

기존 `Skill_BasicAttack.asset`은 Final MP가 1이므로 승인된 0과 충돌한다. 원본을 건드리지 않고 Unity가 새 SO로 복제한 뒤 ID/key와 Final MP만 변경했다. 피해2, baseStagger1, 약점 보너스3, speed10 및 기존 공격 연출 문자열 등 나머지 원본 데이터는 유지한다. 따라서 원본은 기존 Registry와 loadout의 호환 자산으로 남고 새 기본 공격은 실제 연결 전 Playtest 후보이다.

촌하우의 displayName은 촌하우, tag Attack, targeting SingleEnemy, Compatibility, Unique owner 비어 있음, 상태 효과 비어 있음, baseStagger/weaknessStaggerBonus 0이다. Animator trigger는 빈 문자열, VFX/SFX 참조는 null이다. `speed=5` 등 명시되지 않은 필드는 기존 SkillDefinitionSO 타입 기본값이며 **사용자가 승인한 촌하우 최종 밸런스로 해석하지 않는다**. 특히 속도는 동시 소진 tie에 영향을 줄 수 있어 실제 전환 전 승인이 필요하다. 특수 효과는 추가하지 않았다.

Normal/Clash는 Skill SO의 ownership 필드가 아니라 `FinalCombatRuntimeConfigSO.handCardRules`의 CombatCardKind로 지정한다. 영구 Skill key와 전투 카드 RuntimeId는 별개이다. 새 두 SO는 실제 Config/Registry/Character/Scene에 연결하지 않았고, 동일 Skill을 양쪽 kind로 중복 배치하는 것은 기존 초기화 계약에서 거부한다. 빈 문자열 trigger는 특정 승인 연출을 뜻하지 않으며 실제 연출 검증은 미수행이다.

## 실행과 범위

루트 AGENTS.md 적용, 중첩 없음. branch main, HEAD `b42544824838e7de0e70b71afc2ca21a59db8192`. 기존 미커밋 Character SO 및 보고서 보존. Unity AssetDatabase로 고유 ID/key 충돌 검사를 선행하고 두 새 SO/meta를 생성했다. 추가 C#은 Editor 테스트 `Assets/GAME/Tests/Editor/AnandInitialSkillDataTests.cs`와 meta(GUID `ec294d9a32afd8d4e9d2e55f9b4e4111`)뿐이며 Runtime API/SerializeField·기존 GUID 변경 없음. 삭제/이동 없음.

Unity 6000.3.23f1 격리 집중 검사 2/2 PASS (`Logs/CBT-R2-08-P2B3D4C-Focused2.xml/.log`). 실제 새 SO와 `char.anand_de_morte`를 기존 EntryPoint/Session에 공급했다. 테스트는 기존 승인 Config를 일시 복제해 enable하고 Normal4장/Clash6장·MP3을 **fixture에만** 공급했다. 3/2 슬롯과 서로 다른 카드 RuntimeId, snapshot 이후 장착 변경 불변성, Normal MP0·discard, 촌하우 MP1·discard·중복 토큰 거부, MP 부족 시 카드/MP 무변경, 3회 합 최종 피해4(HP10→6)를 확인했다. 부족 검사 후 fixture에서 MP를 이전 값으로 복원해 최종 피해를 검증했으며 전투 중 MP 자동 회복은 추가하지 않았다.

첫 비교는 Normal 공격으로 시작하고 촌하우는 후속 Clash 선택에 사용했다. 촌하우를 최초 대응 카드로 선택하는 별도 실제 SO 조합 검사는 이번 집중 검사에서 직접 실행하지 않았다. 최초 비용 중복·동일 Skill 양쪽 배치·잘못된 ID 등의 기존 계약은 관련 기존 fixture 회귀에서 검증한다. fixture의 copies4/6와 seed는 정식 카드 제작 데이터가 아니다. 같은 Skill SO의 복수 카드 인스턴스로 슬롯을 채울 수 있어 서로 다른 5개 SO가 필수는 아니지만, 실제 copies/seed와 초기 획득·장착 정책은 Pending이다.

Unity 선택 회귀 **868 PASS / 6 FAIL / 1 IGNORED**, 총875 (`Logs/CBT-R2-08-P2B3D4C-Regression.xml/.log`, filter `...-TestFilter.txt`). 직전 D4B-R1과 실패 이름이 같다. Dungeon Hand Debug 선행 gate 5건과 Template 상속 BP Debug gate 1건으로 모두 **Asset Change Required**다. Ignored는 별도 Editor 재시작이 필요한 `PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners`다. Legacy 관련 220/220 PASS, 기존 Skill persistent identity 및 Hand/BP/Clash 회귀를 포함했다. 전체 회귀와 실제 Production Scene Play Mode는 수행하지 않았다.

Unity 신규 Editor C# 컴파일 및 테스트 실행에서 CS 오류/경고가 없었다. 시작 기록한 기존 파일 **2,618개 SHA-256 동일**. 새 SO/meta2쌍, Editor 테스트/meta와 보고서만 추가했다. GUID3개 고유, 새 SO를 Scene/Prefab/Production Config에서 참조하지 않는다. 기존 원본 기본 공격·아난 SO·사용자 미커밋 변경 보존, `git diff --check` 및 신규 텍스트 공백 검사 통과. commit/push/PR 없음.

## 연결 계획 및 차단 조건

1. 별도 승인 후 기존 CombatEntryPoint.skillDefinitions에 두 새 SO를 기존 등록 내용과 충돌 없이 추가하고, CharacterSkillRuntime에서 실제 초기 획득·장착 경로를 확정한다.
2. 실제 Config의 handCardRules에 기본 공격 Normal, 촌하우 Clash로 각각 단일 배치하고 승인된 copies/seed를 작성한다. 테스트 SO/fixture를 직접 연결하지 않는다.
3. 촌하우 speed 및 관련 기획 수치·Animator trigger/VFX/SFX를 결정하고, MP0 기본 공격의 신규 ID와 기존 loadout 전환 영향을 점검한다.
4. 별도 보호 자산 변경·초기화 승인 후 Inspector와 Contact/FieldAttack, 카드 소모·MP·반복 합·피해·인계·Terminal/Reward 및 Save/Continue를 실제 씬에서 검증한다. 05/06A의 보류 상태는 유지한다.

현재 Production 연결·정식 콘텐츠/카드 구성·Inspector/Play Mode는 미검증이며 Cutover 완료가 아니다.

## Development tracking

- System: Combat initial skills / Hand snapshot. Owner: CombatEntryPoint, CombatSession, CombatFlowOrchestrator, SkillRunner 및 CharacterSkillRuntime.
- Implementation: Playtest Skill SO2개 및 격리 fixture 작성 완료; Production Registry/Config/초기 장착 연결 Pending.
- Compile/isolated validation: 집중 검사 통과. 실제 Production Inspector/Play Mode/Integration Unverified.
- Recommended: **Initial Skill Data Implemented — Card Authoring / Presentation / Production Wiring Pending**.
