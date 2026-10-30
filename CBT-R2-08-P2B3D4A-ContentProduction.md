# CBT-R2-08-P2B3D-4A — 챕터1 정식 콘텐츠 제작

2026-10-31 KST. **사용자가 승인한 천사 source 정의 SO 1개를 실제 제작했다.** 공식 Character/초기 Normal·Clash Skill은 필수 식별자·성장·FinalExchange 적용 데이터가 미승인이라 생성하지 않았다. source 제작 완료를 천사의 전투 콘텐츠 전체 완료로 취급하지 않는다. 기존 보호 Scene/Prefab/SO·Registry는 무변경, Production 초기화 비활성, Cutover 미실행이다.

## A. 실제 제작 결과

### 제작한 Enemy SO

| 항목 | 실제 값 |
|---|---|
| Asset | `Assets/GAME/Data/Enemy/Enemy_Angel.asset` |
| 타입 | 기존 `Game.Enemies.EnemyDefinitionSO` |
| `persistentKey` | **`enemy.chapter1.angel`** |
| `acquirableSkillPersistentKeys` | 빈 배열. Film 획득 목록 **미지정** |
| `finalCombatBehaviorProfile` | null. AI Profile **미지정** |
| Unity GUID | **`1ee790526b4dbd948b59c54906693bd0`** |
| 새 Enemy 폴더 GUID | `95874da1feeeabd4c87c81ea772b2042` (`Assets/GAME/Data/Enemy.meta`) |
| Production 등록 | Scene/Prefab/Config/Registry에 연결하지 않음 |

현재 대화에서 사용자가 “이 ID·경로로 source 정의 제작 승인”을 선택했다. 승인 범위는 위 source key·경로 및 Film/AI 미지정 상태다. HP/MP·전투 스킬 승인이나 Scene 연결 승인으로 확대하지 않았다. 새 Enemy 폴더는 이 승인으로 사용했으며 Character 데이터 폴더의 전역 표준을 임의로 정한 것이 아니다.

기존 타입의 필수 source identity를 갖춘 유효한 SO다. 이 타입은 적의 HP·MP·전투 loadout을 소유하지 않는다. 빈 Film 목록은 현재 연결했을 때 획득 항목이 없다는 저장 표현이며, 기획상 천사가 영구적으로 Film을 주지 않는다는 결정을 뜻하지 않는다. 미연결 상태이므로 기존 실제 전투·보상 동작도 바뀌지 않는다.

신규 `.asset`만 기존 타입의 확인된 필드로 작성한 뒤 Unity로 import했다. **기존 script GUID는 타입 참조에만 사용하고**, 새 Asset/folder GUID는 Unity가 생성했다. 새 GUID 중복 없음. 기존 Enemy/Skill/Character ID 변경 없음.

### Character / Skill SO 및 기존 Asset

- 생성한 CharacterStart/Progression SO: **0개**. 공식 Character ID·정식 시작/최대 레벨·EXP 곡선이 승인되지 않았고, CharacterStart는 유효한 progression definition과 ID/편성을 요구한다. 빈 정의나 타입 기본 EXP100을 공식값으로 저장하지 않았다.
- 생성한 Skill SO: **0개**. 문서에 스킬명과 일부 수치는 있으나 최초 Normal/Clash 지정·persistentKey/skillId·Final MP·위력/효과 적용 계약이 완비되지 않았다. 기존 Skill4개를 중복 생성하거나 이름/효과를 바꿔 정식화하지 않았다.
- 새 C# / Runtime / Owner: **0개**. 기존 SO 타입과 Validator로 충분하여 스크립트를 추가하거나 기존 코드를 수정하지 않았다.
- 재사용한 구조: 기존 EnemyDefinitionSO, EnemySourceComponent/EnemyCombatProvenanceBridge, CombatEntryPoint·기존 Skill Registry/Runner/결과 처리 및 기존 Validator.
- 기존 `Assets/GAME/Data/Combat/Runtime/FinalCombatRuntimeConfig_ProductionPlaytestV1_Candidate.asset`를 그대로 보존했다. Clash3/3·상한3·5초, Hand3/2, BP0/상한3·유효 RoundCycle2회+1·투자0~3, 기존 Ultimate 소비1/2/3 단계 계약은 확정 상태를 유지한다. `enableProductionHandBp=false`; Hand/Ultimate/Enemy Hand 제작 참조는 여전히 비어 있다.

## B. 출처 매핑

### 실제 읽은 자료

Google Drive 연결로 아래 원문을 실제 읽었다. Google Docs의 전체 tab text와 원본 문서 구조를 확인하고 관련 탭을 추출했다. 읽은 것과 자산에 적용한 것을 구분한다. 외부 문서는 수정하지 않았다. 파일 메타/수정일·문서 revision 증거는 `Logs/CBT-R2-08-P2B3D4A-DriveSourceMetadata.json`에 기록했다.

1. [원본 게임 개발 기획서](https://docs.google.com/document/d/1gfyV1lSm2IXKR9Xx--43ICBXCMj9q5ny0_ekxWZB9e0/edit): 주인공-아난 드 모르테(`t.p9uhmtssrkf0`), 전투(`t.ut0aacu08xip`), 성장규칙(`t.vcicx58zrimi`), 전투 > 적-천사(`t.i8bpng8tz0u3`), 스토리-챕터1(`t.a4dlspe9sgsk`), 챕터1 아파트맵(`t.n2i6ozl1v7qv`) 확인. 성장규칙 탭은 현재 본문이 비어 있다. 주인공 탭의 기록 재현 설정은 있지만 Character ID/HP/MP/EXP 정의는 없다.
2. [현재 기능구현 문서](https://docs.google.com/document/d/1JF92rWAxIJ-RNvqJL5lw8CAa_OsVQu61ohmvH9YfVkA/edit): 주인공 표/B02 정책(`t.7hkzp5zfiut9`), 초기 파티/성장 적용 계약(`t.5o8poqwz9lp9`), 현재 전투 문맥을 확인했다. **이 문서에는 주인공 초기 능력치 값이 존재**한다. 해당 값이 문서에 없다고 보고하지 않는다.
3. [현재 인계 문서](https://docs.google.com/document/d/1I8QXiwpWDuHX8VTqGqJjAGA-HhWB1lTDKD_62LgjPhk/edit): D01~D16 등 전투 경계·책임/제안과 실제 현재 코드를 대조했다. “추가 필요”·이전 테스트 수치는 역사적 상태이고 이번 코드 상태를 대체하지 않는다.
4. [콘텐츠 제작 표준](https://drive.google.com/file/d/1tIHX5hID8F46_IaX5tjQ7QfFQ8F4OEBQ/view): §2 ID/Save, §3 경로, §4 Owner, §8 검증.
5. [스킬 제작 가이드](https://drive.google.com/file/d/1gCam7-iHSwr7t7DxI_3Rt0opwseYQqS5/view): §1 기존 콘텐츠 현황, §2 소유 분류, §4 경로/ID, §6 필드, §8 Validator, §10 Feature Gap.
6. [적/조우 제작 가이드](https://drive.google.com/file/d/11H-p-hYgs40tvNjEiIpoVEzTdMyNEOVm/view): source와 HP/loadout 분리, source mapping/실제 root 연결, optional Party/전투 진입 경계.
7. [디자이너 리소스](https://drive.google.com/drive/folders/1C1e86BBrSP1NbYWDwQnOXmPYA0KwnMg6): 폴더를 실제 조회해 `monster/all monster/angel monster`를 확인했다. [angel monster 폴더](https://drive.google.com/drive/folders/1vTB2zbUqMlbMLglAMrPnYaTDq4rLLnzX)에 png.gif/sprite sheet/aseprite 하위 폴더가 있다. sprite sheet 하위 조회는 빈 목록이었다. 원격 그림을 다운로드하거나 시각 검수했다고 주장하지 않는다.

### 기획 → 필드 → SO → Owner → 검증

| 출처/확정 내용 | 데이터/실제 SO | Production Owner | 이번 검증/상태 |
|---|---|---|---|
| 원본 주인공 탭 + 현재 요청: 아난 드 모르테·기록 재현 | 현 CharacterStart/Progression에는 이름/서사/임의 재현 능력을 담는 필드가 없음 | Party/Progression/CharacterSkillRuntime | 설정 확인. 임의 SO 타입 추가 없음 |
| 기능구현 파티 절 + 현재 요청: 시작 보유/리더/전투편성 각1명 | CharacterStart의 definitions/initialMemberIds/initialLeaderId/initialCombatMemberIds | RuntimeBootstrapper → PartyRuntime/Progression | 구조 확정 유지. 공식 ID/진행도 없어서 실제 SO 생성·실데이터 검증 불가 |
| 기능구현 주인공 표: HP320 등 | 아래 수치 확인. Progression SO에 HP/공격력 필드 없음 | field HP adapter / Session·기존 전투 공식 | 수치 존재 확인; FinalExchange 적용 계약 Pending |
| 원본 전투 > 적-천사 + 스토리-챕터1: 아파트 조사 중 천사 조우 | **새 Enemy_Angel.asset**의 출처 정체성 | EnemySourceComponent → EnemyCombatProvenanceBridge → Session/Result | 사용자 승인 key/경로로 실제 제작, 기존 source Validator 통과 |
| 현재 대화 사용자 승인 | persistentKey=`enemy.chapter1.angel`, Data/Enemy 경로, Film/AI 미지정 | 기존 EnemyDefinitionSO | 새 Asset import 및 전역 source 중복 검사 |
| 원본 적-천사: 내려찍기/물어뜯기/기도 | 미생성 Skill SO; 기존 Angel_Skill/2와 자동 동일시하지 않음 | SkillDefinitionSO → SkillRunner/Outcome Executor | Final MP·분류·효과 미완비 및 Feature Gap. 실콘텐츠 실행 PASS 아님 |
| 원본 전투: 물어뜯기 Film 획득 예시 | 새 source의 획득 목록은 빈 배열 | EnemySkillAcquisitionIntegration → shared Film runtime | “예를 들어”인 문맥. 정식 Film 목록 미승인이라 미적용 |
| 현재 사용자 승인 V1 | 기존 비활성 ProductionPlaytestV1_Candidate SO | EntryPoint/Session/Orchestrator | 기존 수치·비활성·fixture 비참조 회귀 유지 |

### 확인한 실제 콘텐츠와 차이

원본 `전투 > 적-천사` 표에는 HP30, 회피3, 방어10, 속도3, 명중5, 치명2, 공격력7~14가 있다. 내려찍기는 수치7과 기절확률, 물어뜯기는 수치10과 출혈, 기도는 자가 버프·소량 피해와 공포/약화 문맥이다. **옛 턴제 표를 FinalExchange의 baseDamage/clashPower/FinalMpCost에 그대로 복사하지 않았다.** 새 source SO에는 이 수치를 저장하는 필드 자체가 없다.

원본의 별도 `천사` 탭(`t.4v173l4bm654`)은 지배·권속·테이밍/캐릭터 앤젤 설계다. 챕터1 일반 천사 몬스터의 AI나 Film 목록으로 혼합하지 않았다. 스토리-챕터1은 실종 아이를 찾는 첫 정식 임무에서 천사와 조우하는 맥락을 확인한다. 현재 구현 맵의 Enemy_01~03을 특정 스토리 등장 개체로 자동 확정하지 않았다.

현재 기능구현 문서의 주인공 표에는 **HP320 / 정신력100 / 공격18 / 방어11 / 속도16 / 정밀도20 / 치명14 / 회피9 / AP3**이 있다. 같은 문서는 “정식 전투 적용은 확인 필요”라고 명시한다. 따라서 숫자를 없는 것으로 재분류하지 않고 **문서 수치 확인, FinalExchange 적용 Contract Pending**으로 기록한다. AP3을 MP3으로 바꾸지 않는다. 공식 시작/최대 레벨·EXP 곡선은 문서의 미확정 항목이다.

기능구현 문서의 Character ID `char.anand_de_morte`는 **후보/제안**으로 표시되어 있다. 이번 source key 승인에 포함되지 않으므로 아직 공식 ID로 작성하지 않았다. 기존 hero.test는 그대로 보존했다.

현재 실제 Dungeon 자산:

| 경로/자산 | 관찰값 | 이번 처리 |
|---|---|---|
| `Assets/GAME/Scenes/Dungeon_1_Production.unity` / World/Encounters/Encounter_01/Enemy_01, Encounter_02/Enemy_02, Encounter_03/Enemy_03 | 각 HP/maxHP10, loadout IDs11/12. EnemySourceComponent 없음 | 원본 무변경, 새 source 연결 없음 |
| `Assets/GAME/Data/Skill/Skill_BasicAttack.asset` | ID1, key skill.production.0001, FinalMP1, 위력2, 피해2, speed10 | 기존 Registry/호환 자산 유지. 새 Normal로 자동 확정하지 않음 |
| `Assets/GAME/Data/Skill/Skill_skill2.asset` | ID2, key skill.production.0002, FinalMP2, 위력3, 피해4, speed5 | 동일. 새 Clash로 자동 확정하지 않음 |
| `Assets/GAME/Data/Skill/Angel_Skill.asset` | ID11/key skill.production.0011, FinalMP1/위력2/피해1/speed5, trigger Attack | 기존 그대로. 내려찍기와 같은 공식 스킬이라고 선언하지 않음 |
| `Assets/GAME/Data/Skill/Angel_Skill2.asset` | ID12/key skill.production.0012, FinalMP2/위력3/피해1/speed5, trigger Attack2 | 기존 그대로. 물어뜯기 Film으로 승격하지 않음 |
| `Assets/GAME/Image/Enemy/angel/` | idle/run/attack1/attack2 clip, punch/bite sheet 및 AngelAnimatorController 존재 | 경로·serialized trigger 확인. 그림/실제 애니메이션 Play 검수 미실행 |

기존 AngelAnimatorController에는 Speed/Attack/Attack2/Hit/Stagger/Die 파라미터가 있다. 원격/로컬 아트가 동일 버전인지 검증하지 않았고 새 연결도 하지 않았다. 기존 Skill4개 소유 분류는 Compatibility이며, Film/Unique와 Normal/Clash는 독립 계약이다.

## C. 결정 필요 항목

### 이번에 확정된 것 — 재승인 대상 아님

- 주인공 아난 드 모르테·기록 기반 재현 설정, 시작 보유/리더/전투편성1명.
- Normal/Clash 분리, 동일 Skill 양쪽 배치 금지, Film/Unique 별도 소유 분류.
- 승인된 Hand/BP/Clash/Ultimate Playtest V1 수치와 기존 확정형 Overcome35.
- **천사 source key `enemy.chapter1.angel`와 Data/Enemy/Enemy_Angel.asset 경로, Film/AI 미지정으로 source만 제작**.
- 기존 현재 Runtime의 참가자별 전액 EXP·CompletionId 중복 차단 정책. 옛 문서의 EXP 분배 미정 문구 때문에 현재 정책을 다시 변경 대상으로 열지 않는다.

### 사용자 결정 후 바로 제작할 수 있는 목록

| 항목 | 구체적인 결정/선택 | 작성할 필드/영향 |
|---|---|---|
| 공식 Character ID | 문서 후보 **char.anand_de_morte 채택** 또는 다른 영구 ID 지정 | progression.characterId, start 멤버/리더/편성/기본 EXP 대상. 기존 hero.test 변환 없음 |
| Character 경로 | Data/Character 등 신규 경로를 승인하거나 별도 경로 지정 | Enemy 폴더 승인만으로 Character 표준을 정하지 않음 |
| 레벨/성장 | 시작레벨, 최대레벨, 레벨별 필요 EXP 표 승인 | Progression startingLevel/maximumLevel/experienceRequiredByLevel. 타입 기본1/99/100을 공식화하지 않음 |
| 초기 전투 수치 적용 | 문서 HP320 등 채택/수정 여부와 FinalExchange 적용 범위 결정 | HP field binding 및 능력치 snapshot 계약. AP3과 MP는 별도 |
| 최초 Normal/Clash | 기존 ID1/2의 현행 수치를 그대로 플레이테스트에 사용할지, 문서 기술을 새 승인 데이터로 만들지 결정 | 정확한 Skill SO/key, Normal/Clash kind, copies/seed, 초기 획득/장착 시점 |
| 새 Skill을 만들 경우 | 기획에 있는 정확한 이름·분류·대상·사용 조건·Final MP·위력/피해/speed·지급 출처 승인 | SkillDefinitionSO 필수 필드 및 고유 key/skillId. 미승인 값0으로 채우지 않음 |
| 천사 전투 수치 | 기존 Dungeon HP10/loadout11·12를 유지할지, 옛 HP30/기술 표를 FinalExchange용으로 재설계할지 결정 | HP Component/loadout/필요한 Skill. **source ID는 이미 확정** |
| AI/MP | 기본 유효 스킬 정책 유지 또는 authored 우선순위, MP 계약 선택 | 선택적 AI Profile/Config. 일반 적의 Hand opt-out 유지 가능 |
| Enemy→Film | 획득 없음/보류 유지 또는 승인된 Film key 목록 선택 | 현재 빈 목록 유지. 전투 스킬 목록과 획득 목록을 분리 |

예를 들어 기존 Normal/Clash 후보를 채택한다면 ID1과2의 실제 수치는 위 표에 있으며, 승인 없이 적용하지 않았다. 새로운 임의 스킬명·효과·성장 곡선을 제안값이라는 이유로 Production에 넣지 않았다.

### Feature Gap / 별도 구현 요청

- 내려찍기의 **확률 기절**, 물어뜯기의 **출혈/지속 피해**: 현 Status SO는 damage/clash/posture 정수 보정·중첩/지속 메타데이터를 지원한다. 확률 발동/DoT를 해당 보정값으로 가장하지 않는다. 기존 Stun/Posture 처리와 확률 기절은 같은 기능이 아니다.
- 기도의 **자가 버프 + 별도 적 소량 피해**, 공포/약화: 효과 대상·수치·지속 단위와 Mental 연결을 먼저 확정해야 한다. 일반 signed 피해 보정 등 일부 기초 기능은 있으나 이 복합 기술 전체가 단일 현행 SO로 완성되었다고 보지 않는다.
- 원본 통찰의 “다음 스킬 예측”과 현재 Inspect의 “약점 공개”는 다르다. Utility/ScanEnv라는 태그만 설정해 해당 기획을 구현했다고 표시하지 않는다.
- 기존 주인공 표에는 다이 하드/조디악/펄프 픽션/킬 빌/화양연화/아키라/중경삼림/풀 메탈 재킷 및 명중·치명·퍼센트 피해 수치가 있지만, 현재 Unique의 지원 해금5/10/20/40 및 최대6개와 그대로 일치하지 않는다. 명중/치명 보정·학습 레벨 표를 현재 전용 Skill로 자동 변환하지 않았다.
- 전체 기본능력치/성장값을 최종 전투 공식에 일괄 적용하는 정책, 임의 기록 재현·권속/테이밍은 이번 콘텐츠 작업 범위 밖이다. 새 엔진/Manager로 구현하지 않았다.

## D. 구현 및 검증

### 지침·Git·파일·직렬화

- 읽은 적용 지침: 루트 `AGENTS.md`; 적용 중첩 없음. Google Drive/Docs 스킬은 원문 읽기에 사용했고 쓰기 동작은 하지 않았다.
- Branch `main`, HEAD `6fa26d294472574097d33b884858ba0ee8bdc8b7`. 시작 Git status 및 tracked/untracked 파일2,603개의 SHA-256을 `Logs/CBT-R2-08-P2B3D4A-BeforeStatus.txt`, `...-BeforeHashes.json`에 기록했다.
- 추가: Enemy_Angel.asset, Unity 생성 asset meta 및 Enemy 폴더 meta, 이 보고서. **기존 파일 수정/삭제/이동0, C# 추가/수정0**.
- 기존 public API/SerializeField/enum/GUID 및 Unity/Package/Input/Render pipeline 무변경. 새 SO는 기존 EnemyDefinition script GUID `52c4238b155d1cc4cb1d524581ad9a6f`만 참조한다. Tests/Fixtures/Demo 참조 없음.
- SaveLoadService/CharacterSkillRuntime/PartyRuntime 및 현재 Save schema **14** 유지. Character ID, 영구 Skill key/장착 순서·shared Film key 저장 계약 보존, 새 migration 없음. Enemy source는 기존 immutable provenance snapshot/전투 결과 경계를 사용한다.
- 보호 Scene/Prefab/SO 자동 저장·연결·Debug 제거 없음. 기존 Quest/Reward/Save·Unity 자산 및 사용자 dirty/untracked 변경을 보존한다.

### 실제 실행 검증

1. Unity 6000.3.23f1 `Game.EditorTools.EnemySkillAcquisitionValidator.ValidateAllAssets` 실행, 종료0. 로그 `Logs/CBT-R2-08-P2B3D4A-EnemyValidation.log`: **Enemy identities and explicit skill mappings are valid**. 새 SO import/type/source-key 검사 통과. 빈 매핑의 구조 유효성이 실제 Film 콘텐츠 완성을 뜻하지 않는다.
2. 기존 read-only ProductionDataAudit, Playtest 후보 감사, 전역 persistent key/Registry·Character·Enemy/획득·snapshot·Save 검사와 기존 전투 회귀를 선택 실행했다. **864 PASS / 6 FAIL / 1 IGNORED**, 총871. 기록 `Logs/CBT-R2-08-P2B3D4A-Regression.xml/.log`, 정확한 filter `...-TestFilter.txt`. 종료2는 기존6개 자산 gate 실패다. 직전 D3 XML과 실패 이름이 동일하며 선택 범위에서 신규 실패 없음.

세부 결과:

- 이번 범위에 추가한 기존 검사 **39/39 PASS**: CombatSkillIdentityValidation5(전역 persistent key/호환 ID 및 Dungeon/Template 실제 Registry 포함), EnemySkillAcquisitionData4, EnemySkillAcquisitionIntegration15, EnemyCombatProvenance4, CharacterSkillRuntime11.
- 기존 후보 Config **24/24**, D2 메모리 Prefab 예행 **14/14**, B2D startup **40/40**, B3A **22/22**, B3B **15/15**, B3C **23/23**, D1 **8/8 PASS**.
- Legacy 관련 기존7개 suite **220/220 PASS**. 기존 격리 Director Play Mode/반복 합·손패/BP·Enemy Hand 선택·Item/Film·Terminal/Reward/참가자 EXP/Abort 복구 회귀도 유지했다.
- `CombatProductionDataAudit.Run` 재실행 출력에 실제 `EnemyDefinitionSO: 1` 및 새 자산 경로가 나타난다. `MISSING AI profile`도 그대로 보고한다. AI Profile은 현재 선택 기능이지만 콘텐츠 미지정 사실을 숨기지 않았다. 실제 Scene에는 여전히 EnemySourceComponent가 없다.
- 후보 감사는 `MATCH / DISABLED`, **Readiness: BLOCKED**를 보고한다. source 정의 부재 항목은 해소됐지만 CharacterStart·장착/Hand 규칙 부족이 남는다. source1개 존재가 실제 Enemy root 연결/전투 수치/Film 콘텐츠를 검증한 것은 아니다.
- 새 C# 추가·변경 없음. Unity Editor import/assembly load/테스트 실행에서 `error CS`/`warning CS` 없음. C# 전체 강제 재컴파일/Player Build를 수행한 것으로 주장하지 않는다.
- 기존 OfficeFlowController meta GUID import 문제 및 빈 Scenes/Tests/Tests.asmdef 안내는 기존 자산 문제/경고로 기록하고 원본을 보존했다.

기존6개는 모두 **Existing Confirmed / Asset Change Required**, 공통 namespace `Game.Tests.Integration`:

1. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasCanonicalCombatRewardUiCameraAndBootstrapOwners`
2. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasExplicitInteractionOwnersWithoutLegacyInteractionPaths`
3. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasOneCanonicalRepeatableNpcInteraction`
4. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasQuestDerivedCompletionContractWithoutLegacyExitOrFallbackDestination`
5. `DungeonOneProductionSceneTests.ProductionDungeonOne_IsCleanEnvironmentMigration`
6. `ProductionSceneWiringTests.DungeonTemplate_IsCanonicalSingleOwnerProductionSource`

앞5개 로그는 공통 `Forbidden Demo/Debug/Test/Legacy component remained: Game.Combat.Debugging.CombatHandValidationOptIn on CombatRuntime`. 마지막은 Template이 상속한 BP Debug 존재 predicate의 Expected False / But was True다. 원문/stack은 `Logs/CBT-R2-08-P2B3D4A-SixEvidence.txt`. 공통 선행 gate이므로 각 테스트의 NPC/Quest/UI 후속 조건을 개별 결함 또는 검증 완료로 오해하지 않는다. 새 Enemy SO로 이 실패를 우회하지 않았다.

기존 IGNORED는 `Game.Tests.Integration.PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners`. 별도 프로세스 재시작 검증 미실행이며 새 Skip/Ignore 없음.

최종 보존: 시작 기록 **2,603개 전부 SHA-256 동일**, 누락/기존 수정0. 신규 Enemy asset/meta·폴더 meta·보고서4개만 추가했다. 새 GUID2개 고유, 새 source key 및 GUID가 기존 Scene/Prefab/Config에 연결되지 않았음을 확인했다. 신규 asset/보고서 공백 검사 통과. `git diff --check`의 기존 Font3곳/Dungeon Scene1곳 trailing whitespace4건 및 기존 줄바꿈 안내는 기존 사용자 변경으로 보존했다. commit/push/PR 없음.

기존 D3 선택 기준선은 **825 PASS / 6 FAIL / 1 IGNORED**, 과거 전체 기준선은 **1,562/8/1**이다. 이번 실행 결과와 혼합하지 않는다. 신규 C#이 없어 새 테스트 스크립트도 추가하지 않았고, 기존 메뉴/fixture/검사를 재사용했다.

### 실제 콘텐츠로 미실행한 검사

- 공식 Character Start/성장 SO 및 실제 장착 snapshot: 미제작으로 실데이터 검증 불가. 기존 validator/fixture 통과는 코드 계약 검증이다.
- 최초 정식 Normal/Clash 효과/천사 Film 획득: 데이터 미승인 및 일부 Feature Gap으로 실제 실행 미검증.
- 실제 Dungeon/Title의 Inspector·Contact/FieldAttack·표현/UI/Input·Reward/Save 통합 Play Mode 및 Player Build·Editor 재시작은 미실행.
- 보호 자산 설치·저장 위험이 있는 전체 회귀와 실제 Production Scene Play Mode 검사는 제외한다. 기존6개 자산 gate는 읽기 전용 EditMode 검사로 재현한다.

### 향후 Inspector 연결 절차 — 이번 미실행

1. Character/Skill·성장·천사 전투 데이터 승인과 각 새 SO 유효성 검사를 먼저 완료한다. CharacterStart/Progression과 실제 획득·장착 source가 갖춰져야 한다.
2. 별도 보호 자산 변경 승인 후 변경 직전 사본/해시·Inspector·UnityEvent/override를 기록한다. 현재 Dungeon `World/Encounters/Encounter_01/Enemy_01` 등 실제 combatant root에 `EnemySourceComponent`를 연결할 때 definition=`Enemy_Angel.asset`를 사용한다. Contact helper child에는 붙이지 않는다. 구체적 적용 개체 범위는 그 단계에서 승인한다.
3. 전투 loadout IDs 및 Film 획득 목록은 각각 승인된 데이터로 별도 작성한다. Enemy Hand를 선택 적용할 때만 Config.enemyHandSourceKeys에 **이미 승인된** enemy.chapter1.angel과 일치하도록 작성한다. 현재 목록은 비어 있고 이번 변경하지 않았다.
4. 실제 EntryPoint Registry·Character snapshot·Hand 규칙·필수 Clash를 검증한다. 새 source만 존재한다고 초기화 enable을 켜지 않는다. Camera/UI/World/Reward Owner와 기존 override를 보존한다.
5. 별도 승인 후 Contact/FieldAttack, Victory/Defeat/Abort, 중복 완료·Film 미지정/지정, Reward/참가자 EXP·Exploration 복귀, Save/Load를 검증한다. 상태·시간·입력 복구와 다음 전투 snapshot도 확인한다.

Production 활성화 가능 여부: **아직 불가**. source identity만 준비되었고 공식 Character/초기 Skill·성장·적 전투 데이터·실제 연결 및 별도 승인이 남는다. CBT-R2-05/06A 수동 검증 보류와 LegacyPlanning 호환을 유지한다.

## E. 개발현황

- 구현: **천사 Enemy source Asset — Implementation Complete**. Character/첫 Normal·Clash/천사 실제 전투 수치·Film/AI 콘텐츠는 **In Progress / Content Pending**.
- 검증: 새 source Unity import·기존 ID/매핑 Validator 확인. 실제 연결 Inspector/Production Play Mode/Integration **Unverified**. 자동 회귀 세부 결과는 아래 기록 참조.
- 승인 완료 항목은 유지하고, 후보 Character ID와 FinalExchange 수치 적용·성장/스킬·Film 결정만 후속으로 남긴다.
- 다음 제작 Batch 추천: **Character ID/경로·레벨/EXP와 초기2종 Skill의 정확한 명세 승인 → CharacterStart/Progression 및 필요한 Skill SO 작성 → Hand rules/copies·초기 장착 정의 검증**. 기존 Skill을 승인해 재사용하면 중복 Skill SO를 만들지 않는다.
- 권장 상태: **First Enemy Source Asset Created — Character / Initial Skills / Enemy Combat Content Pending; Production Wiring & Manual Integration Pending**.

### Development tracking

- System: Chapter1 Combat Content / Enemy provenance.
- Requirement: CBT-R2-08-P2B3D-4A 및 현재 source-only 명시 승인.
- Production owner: 기존 EnemySourceComponent/EnemyCombatProvenanceBridge → CombatEntryPoint/CombatSession/CombatResult → EnemySkillAcquisitionIntegration; Party/Progression/CharacterSkill/Reward/Save 각 기존 Owner 유지.
- Implementation: source-only 완료, 전체 최소 전투 콘텐츠 세트 미완료.
- Compile/automatic: 최종 실행 기록 참조. C# 무변경, 새 SO import/Editor 실행 확인.
- Inspector/Play Mode/Integration: 실제 Production 연결 미실행. 격리 회귀는 실콘텐츠 완성을 뜻하지 않음.
- Stale mapping: 제작 가이드의 schema12 및 “EnemyDefinition0개”는 역사적 상태. 실제 schema14 유지, 이번 source1개 생성. 원본 성장규칙 빈 탭과 기능구현의 실제 초기 수치 표를 구분한다. 이전 보고서는 덮어쓰지 않았다.
