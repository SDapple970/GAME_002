# CBT-R2-08-P2B3D-4B — 아난 드 모르테 캐릭터 데이터

공식 ID `char.anand_de_morte`, 시작 레벨 1·EXP 0과 요청에 명시된 Playtest V1 수치는 승인되었다. 그러나 현재 SO 계약에 필요한 최대 레벨과 EXP 곡선이 미승인이므로 유효한 Production Character SO는 생성하지 않았다. 시작 SO도 유효한 Progression 정의를 필수 참조한다. `maximumLevel=1`은 성장을 차단하는 정책이고 기본값 `99`·EXP `100`은 미승인 수치이므로 사용하지 않는다.

## 데이터와 소유권

| 승인 항목 | 현행 계약 / 상태 |
|---|---|
| ID `char.anand_de_morte` | Progression의 `characterId`, Party·Save·Combat 결과의 식별자. 실제 SO 미생성, 기존 자산에서 중복 ID 미발견 |
| 표시명 아난 드 모르테 | 현 Start/Progression SO에 표시명 필드 없음 |
| 시작 레벨 1 | Progression `startingLevel`에서 지원, SO 생성 보류 |
| 시작 EXP 0 | ProgressionService가 신규 상태 생성 시 0으로 초기화. SO에 런타임 EXP를 저장하지 않음 |
| 보유/리더/전투편성 각 1명 | CharacterStart의 definitions, initialMemberIds, initialLeaderId, initialCombatMemberIds 및 defaultRewardTargetId에 동일 ID 연결 가능. 유효한 Progression이 필요 |
| HP 320 | CharacterPartyCombatAdapter가 field HP source에서 현재/최대 HP를 snapshot. Character SO에 해당 필드 없음; 실제 field 연결은 미실행 |
| 정신력 100 / 초기 MP 2 | FinalCombatRuntimeConfig의 initialMental/initialMp가 Session 상태에 적용. 기존 비활성 Playtest 후보에 각각 100/2가 있으나 캐릭터별 저장값이나 실제 Scene 연결은 아님 |
| 공격 18, 방어 11, 속도 16, 정밀도 20, 치명타 14, 회피 9 | 현재 CharacterProgression SO에 해당 필드 및 승인된 성장→전투 snapshot 연결 없음. 수치 변환 또는 임의 적용 없음 |

`RuntimeBootstrapper`는 CharacterStart를 검증한 뒤 `CharacterProgressionService`와 `PartyRuntime`을 구성한다. `CharacterPartyCombatAdapter`는 편성 ID, 진행도, field object 및 유효 HP를 검증한다. EntryPoint는 장착 persistent key를 기존 bridge로 snapshot하며, 정식 Hand/BP 활성 경로는 유효한 참가자 ID와 비어 있지 않은 스킬 snapshot을 요구한다. Session/CombatResult가 참가자 ID를 유지하고 RewardService가 기존 EXP 계약을 사용한다. 실제 아난 데이터·스킬이 없어 이 ID를 사용하는 실제 전투 성공이나 EXP 지급은 미검증이다. 기존 LegacyPlanning 및 `hero.test` 경로는 유지한다.

## 조사 및 범위

- 적용 지침: 루트 `AGENTS.md`, 중첩 지침 없음. Branch `main`, HEAD `b42544824838e7de0e70b71afc2ca21a59db8192`, 시작 Git status clean.
- CharacterStart/Progression, PartyRuntime, Bootstrapper, CharacterPartyCombatAdapter, EntryPoint/Session, CharacterSkillRuntime, Save 및 기존 후보 Config·Scene 참조를 조사했다. Save schema는 기존 14다.
- 추가: 이 보고서. Character SO/GUID 추가 0, 기존 파일 수정·삭제·이동 0, API/SerializeField/기존 GUID 변경 0. Scene/Prefab/SO 연결, Production 활성화 및 Debug 제거 없음.
- 미승인: 최대 레벨, 필요 EXP, 성장 곡선·능력치 증가, 최대 HP 변경 시 현재 HP, 영구 HP/MP와 Session 연결, Unique 매핑 및 초기 Normal/Clash 스킬. 공식 ID·시작 능력치 자체는 승인된 상태로 유지한다.

## 연결 및 수동 검증 계획

1. 최대 레벨과 레벨별 필요 EXP를 승인하고 실제 Progression SO를 작성한다. 같은 ID로 CharacterStart의 1인 보유·리더·편성·EXP 대상과 definitions를 연결한다.
2. 초기 Normal/Clash Skill, persistentKey, 비용·효과, 카드 분류/copies 및 초기 획득·장착을 승인하고 기존 Registry·snapshot 계약으로 검증한다.
3. 별도 보호 자산 변경 승인 후 Title/Dungeon RuntimeBootstrapper의 `characterStartDefinition`과 Progression 참조, Party field binding 및 HP source를 Inspector에서 확인한다. 320 HP와 Config의 MP/Mental 적용 경계는 별도 연결 검증한다.
4. 실제 New Game에서 1인 ID/레벨/EXP, Contact·FieldAttack, 스킬 snapshot, 참가자 EXP, Save/Continue 및 다음 전투 복귀를 확인한다. Scene Play Mode와 Production 통합 검증은 이번에 수행하지 않았다.

## Development tracking

- System: Character / Party / Combat participant snapshot. Owner: RuntimeBootstrapper, CharacterProgressionService, PartyRuntime 및 CombatEntryPoint.
- Implementation: **Blocked — 승인된 ID/시작값 확보, 필수 성장 데이터 미승인으로 SO 미생성**.
- Compile/자동 검증: 아래 실제 실행 기록. Inspector/실제 Production Play Mode/통합: Unverified.
- Recommended: **Character Content In Progress — Growth Contract / Initial Skills / Production Wiring Pending**.

## 실제 실행 및 보존 결과

Unity 6000.3.23f1 기존 선택 회귀 **864 PASS / 6 FAIL / 1 IGNORED**, 총 871 (`Logs/CBT-R2-08-P2B3D4B-Regression.xml/.log`). 직전 4A 실행과 실패 이름이 동일하다. 5건은 Dungeon의 Hand Debug 선행 검사, 1건은 Template의 상속 BP Debug 검사로 **Asset Change Required** 유지. Ignored는 별도 Editor 재시작이 필요한 `PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners`다. 실제 Production Scene Play Mode 및 보호 자산 저장 검사는 제외했다.

기존 Character/Party/Save/EXP, Hand/BP/Clash 및 Legacy 관련 fixture가 선택 범위에 포함된다. 이 결과는 실제 아난 SO의 고유성·직렬화·등록·초기화 통과를 뜻하지 않는다. 새 C#이 없으며 Unity 테스트 실행에서 C# 오류/경고는 확인되지 않았다. 별도의 강제 전체 재컴파일은 수행하지 않았다.

시작 시 기록한 기존 파일 2,609개 해시가 모두 동일하다. `git diff --check` 및 신규 보고서 공백 검사 통과. 변경 상태는 이 보고서 추가 1건뿐이다. 기존 자산·사용자 변경 보존, commit/push/PR 없음.
