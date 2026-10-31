# CBT-R2-08-P2B3D-4B-R1 — 아난 Character SO

승인된 Playtest V1의 공식 ID, 시작 레벨1, 최대60 및 59구간 EXP를 기존 Production 타입으로 작성했다. 기존 Scene 연결·Production 전투 활성화·Cutover는 수행하지 않았다.

## 생성 자산과 계약

- `Assets/GAME/Data/Character/CP_AnandDeMorte.asset`: CharacterProgressionDefinitionSO, GUID `65c6507dcbc40984fa36ade50b89e9aa`; characterId `char.anand_de_morte`, startingLevel1, maximumLevel60, 59개 EXP, Unique 목록 미지정.
- `Assets/GAME/Data/Character/CharacterStart_AnandDeMorte.asset`: CharacterStartDefinitionSO, GUID `5b2e29e279d96f64291e0d33e37f39a2`; 위 정의 참조, 초기 보유·리더·전투편성·기본 EXP 대상은 모두 같은 ID 1명.
- 새 폴더 meta GUID `c39dd89680bc0e748a71945cf1379ae0`. Unity AssetDatabase가 독립 GUID를 생성했다. 두 SO는 Playtest V1로 표시되며 정식 밸런스 확정이 아니다.
- 시작 EXP0은 기존 CharacterProgressionService가 신규 runtime state를 만들 때 적용한다. SO에 진행 상태를 저장하지 않는다.
- CharacterProgressionDefinitionSO 배열 인덱스0은 1→2, 인덱스58은 59→60이다. 마지막 값 반복 없이 모든 59개 구간을 작성했다. 누적 필요 EXP 177,000.

## EXP 전체 표

표의 시작 레벨 L은 L→L+1 구간이다.

| 시작 레벨 | 필요 EXP | 시작 레벨 | 필요 EXP | 시작 레벨 | 필요 EXP |
|---:|---:|---:|---:|---:|---:|
| 1 | 100 | 21 | 2,100 | 41 | 4,100 |
| 2 | 200 | 22 | 2,200 | 42 | 4,200 |
| 3 | 300 | 23 | 2,300 | 43 | 4,300 |
| 4 | 400 | 24 | 2,400 | 44 | 4,400 |
| 5 | 500 | 25 | 2,500 | 45 | 4,500 |
| 6 | 600 | 26 | 2,600 | 46 | 4,600 |
| 7 | 700 | 27 | 2,700 | 47 | 4,700 |
| 8 | 800 | 28 | 2,800 | 48 | 4,800 |
| 9 | 900 | 29 | 2,900 | 49 | 4,900 |
| 10 | 1,000 | 30 | 3,000 | 50 | 5,000 |
| 11 | 1,100 | 31 | 3,100 | 51 | 5,100 |
| 12 | 1,200 | 32 | 3,200 | 52 | 5,200 |
| 13 | 1,300 | 33 | 3,300 | 53 | 5,300 |
| 14 | 1,400 | 34 | 3,400 | 54 | 5,400 |
| 15 | 1,500 | 35 | 3,500 | 55 | 5,500 |
| 16 | 1,600 | 36 | 3,600 | 56 | 5,600 |
| 17 | 1,700 | 37 | 3,700 | 57 | 5,700 |
| 18 | 1,800 | 38 | 3,800 | 58 | 5,800 |
| 19 | 1,900 | 39 | 3,900 | 59 | 5,900 |
| 20 | 2,000 | 40 | 4,000 | — | — |

레벨60에는 다음 구간이 없다. 기존 서비스는 각 구간을 차례로 차감하며 잔여 EXP를 다음 구간에 이월하고, 최대 레벨 도달 시 미적용 초과분을 적용량에서 제외하며 남은 EXP를 0으로 둔다.

## 승인 스탯의 적용 범위

기존 Character SO는 HP·MP·정신력·공격·방어·속도·정밀도·치명타·회피 필드를 가지지 않는다. 따라서 승인된 HP320, 정신력100, 공격18, 방어11, 속도16, 정밀도20, 치명타14, 회피9, MP2는 새로운 직렬화 필드나 임의 변환 없이 승인 상태로 유지한다. 실제 HP는 필드 HP source, MP/정신력은 기존 전투 Config에서 Session에 들어간다. 기존 비활성 Playtest Config의 initialMP2/initialMental100은 이미 존재하지만 캐릭터 전용 연결을 검증한 것은 아니다. 스탯 성장·영구 HP/MP 및 스킬/Unique 내용은 Pending이다.

## Inspector 및 검증 경계

별도 연결 Batch에서 Title/Dungeon의 RuntimeBootstrapper.characterStartDefinition을 새 Start SO로 연결하고, Progression/Party owner 및 아난 field binding·HP source를 확인한다. 초기 스킬 획득·장착/Registry/Hand 규칙과 Combat Session snapshot, Contact/FieldAttack·Reward EXP·Save/Continue는 별도 승인 데이터와 실제 씬 검증이 필요하다. 기존 hero.test와 보호 Scene/Prefab/SO를 그대로 유지했다.

읽은 지침은 루트 AGENTS.md, 중첩 없음. Branch main / HEAD b42544824838e7de0e70b71afc2ca21a59db8192. 기존 미커밋 D4B 보고서를 보존했다. 신규 파일: 두 SO와 각각의 meta, Character 폴더 meta, Editor 테스트와 meta, 이 보고서. 기존 파일 변경/삭제/이동 및 Runtime 공개 API/SerializeField 변경 없음. 저장 schema 변경 없음.

## 실제 검증

Unity 6000.3.23f1에서 AssetDatabase 생성 및 새 Editor 코드 컴파일, 실제 SO 집중 검사 2/2 PASS. 전체 59구간과 ID 중복·Party 초기화, 350 EXP→3레벨 잔여50, 메모리 Save 복원, 초과 EXP의 60레벨 상한을 확인했다. 실제 Scene Inspector·Production Play Mode·Player Build·디스크 재시작은 미실행이다.

기존 범위에 신규 검사 2건을 더한 선택 회귀는 **866 PASS / 6 FAIL / 1 IGNORED**, 총873 (`Logs/CBT-R2-08-P2B3D4BR1-Regression.xml/.log`, filter `...-TestFilter.txt`). 기존 실패6건 이름은 D4A와 동일하다: Dungeon의 Hand Debug 선행 검사5건 및 Template의 상속 BP Debug 검사1건이며 **Asset Change Required** 유지. Ignored는 별도 Editor 재시작이 필요한 `PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners`다. Legacy 관련 기존220건도 PASS. 보호 Scene을 저장하거나 실제 Production Scene Play Mode로 실행하는 테스트는 제외했다.

Unity 로그에서 C# 오류/경고는 확인되지 않았다. 생성 전 해시 기록의 기존 파일 **2,610개 모두 동일**하며 기존 미커밋 보고서, hero.test와 보호 Scene/Prefab/SO 및 기존 meta를 보존했다. 새 SO/폴더/테스트의 GUID는 각각 고유하고 기존 Scene/Prefab에 새 ID 또는 SO 참조는 없다. `git diff --check` 통과, Git 줄바꿈 안내 1건은 별도다. commit/push/PR 없음.

## Development tracking

- System: Character / Party / Progression; owner: RuntimeBootstrapper, CharacterProgressionService, PartyRuntime 및 기존 CombatEntryPoint.
- Implementation: 승인 Playtest V1 Character start/progression 데이터 작성 완료. 스탯 적용·초기 스킬·성장 증가량은 Pending.
- Compile/isolated test: 검증 완료. Inspector/Production Play Mode/Integration: Unverified.
- Recommended: **Character Start Data Implemented — Production Wiring / Stats / Skills / Manual Integration Pending**.

