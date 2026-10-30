# CBT-R2-08-P2B3D-3 — 최소 Production Playtest 데이터 준비

2026-10-31 KST. **승인된 숫자를 담은 독립 비활성 Production Playtest 후보 Config를 생성했다. 최소 실제 전투 콘텐츠 세트는 Content Pending이다.** 공식 아난 드 모르테 ID/초기 장착·성장 및 적 provenance 데이터가 없으므로 새 캐릭터·적·스킬을 임의 제작하지 않았다. 실제 Dungeon 연결, 보호 자산 편집, 초기화 활성화 및 Cutover는 수행하지 않았다.

## 1. 조사·지침·Git

- 지침: 루트 `AGENTS.md` 및 현재 사용자 요청. 적용 중첩 지침 없음. Branch `main`, HEAD `6fa26d294472574097d33b884858ba0ee8bdc8b7` 유지.
- Unity ProjectVersion 및 실행 Editor `6000.3.23f1`, Packages/manifest 및 기존 버전 유지.
- 시작 Git 상태 `Logs/CBT-R2-08-P2B3D3-BeforeStatus.txt`, 기존 tracked/untracked 파일 SHA-256 `...-BeforeHashes.json` 기록. 기존 미커밋 Quest/Reward/Save·Unity 자산·코드·보고서를 보존한다.
- 필수 조사: D1/D2 보고서, `CharacterStartDefinitionSO`, `CharacterProgressionDefinitionSO`, `CharacterSkillRuntime`, `SkillDefinitionSO`, `PersistentSkillCombatLoadoutBridge`, `CombatSkillLoadoutSnapshot`, `EnemyDefinitionSO`, `FinalCombatEnemyBehaviorProfileSO`, `EnemyCombatProvenanceBridge`, `FinalCombatRuntimeConfigSO`, EntryPoint 초기화 및 BP/Ultimate 계약, 기존 Playtest fixture와 실제 Prefab/Scene 참조.
- 편집 전에 승인 수치만 독립 SO로 작성하고 기존 감사 도구/테스트만 최소 확장하는 범위, 공식 콘텐츠 부재 및 직렬화 위험을 보고했다. Runtime 변경·두 번째 Production Owner 추가 없음.

## 2. 새 후보 Config와 실제 입력값

경로: `Assets/GAME/Data/Combat/Runtime/FinalCombatRuntimeConfig_ProductionPlaytestV1_Candidate.asset`.

- 기존 `FinalCombatRuntimeConfigSO` 타입 사용. Unity AssetDatabase로 새 자산을 생성해 GUID **`bbcc87babccf02049b7bb7d9b84157ca`**를 발급했다. 기존 Production/테스트 Config의 GUID를 재사용하지 않았다.
- 라벨: `ProductionPlaytestCandidate`, `ApprovedPlaytestV1Numbers`, `ContentPending`, `Disabled`. 숫자 승인만 의미하며 실제 콘텐츠 승인/연결/활성화 표시는 아니다.
- 기존 `FinalCombatRuntimeConfig_V1.asset` 원본을 덮어쓰지 않았다. Scene/Prefab은 새 후보를 참조하지 않는다. 테스트 SO 복제/직접 참조 없음.

| 실제 필드/계약 | 값 | 근거 |
|---|---|---|
| `clashAttackerDurability` | 3 | 승인 Playtest V1 |
| `clashResponderDurability` | 3 | 승인 Playtest V1 |
| `clashMaxComparisons` | 3 | 승인 상한, 강제 횟수 아님 |
| `clashDecisionDurationSeconds` | 5초 | 승인 Playtest V1 |
| `clashHandCapacity` / `normalHandCapacity` | 3 / 2 | 승인 Playtest V1 |
| `bpMaximum` / `bpRoundCyclesPerGain` / `bpMaximumInvestment` | 3 / 2 / 3 | 승인 Playtest V1, 유효 D12 RoundCycle |
| 초기 BP | 0 | 기존 CombatBpRuntime 계약. SO 필드를 추가하지 않음 |
| Ultimate 단계 | 누적 승인 소비1/2/3 | 기존 CombatUltimateRuntime 계약. 새 게이지 데이터/효과 없음 |
| `clashFinalSkillPolicy` | LastCommittedWinningSkill | 기존 지원 계약 재사용 |
| `clashTimeoutAttackPolicy` | FirstAvailableSkill | 기존 지원 계약 재사용 |
| `enableProductionHandBp` | false | 기본 비활성 유지 |
| `handCardRules` / `ultimateSkills` / `enemyHandSourceKeys` | 빈 배열 | 미승인 콘텐츠를 채우지 않음. Enemy Hand opt-in 없음 |

SO 생성 시 타입의 기존 MP/Posture/pressure/Mental 기본값이 함께 직렬화된다. 기존 Production 기본값과 같은 maxMP3/initialMP2/회복1, Posture3/0, pressure1/0.25, Mental100 및 손실10/5/20·Overcome35를 변경하지 않았다. **이 값들은 이번에 승인한 아난 드 모르테 개인 스탯이나 새로운 밸런스 결정이 아니다.** `handSeed=0`도 타입 기본값일 뿐 제작 seed 승인/완비를 뜻하지 않는다. 카드 copies·Skill 분류·효과·추가 MP 비용을 fixture에서 가져오지 않았다.

재현 메뉴: `GAME/Verification/Create Disabled Production Playtest V1 Candidate`. `CombatProductionDataAudit.CreatePlaytestCandidateIfMissing`는 기존 경로에 자산/파일이 있으면 덮어쓰지 않는다. 새 자산만 `SaveAssetIfDirty`하며 전체 SaveAssets를 호출하지 않는다. 기존 후보가 잘못되었을 때 자동 교정하지 않고 아래 감사에서 문제를 보고한다.

## 3. 실제 콘텐츠 재사용 조사

**현재 공식 콘텐츠로 확정하여 새 후보에 연결한 Character/Skill/Enemy는 0개**다. 존재하는 자산·런타임을 조사한 것과 승인 콘텐츠로 채택한 것을 구분한다.

| 구분 | 실제 존재/경로 및 계약 | 이번 판단 |
|---|---|---|
| Character Start | `CharacterStartDefinitionSO` Asset 없음. Title/Dungeon bootstrap의 `characterStartDefinition` 미지정 | Content Pending |
| Character progression | `Assets/GAME/Data/Test/CP_TestHero.asset`, ID `hero.test`, 시작레벨1/최대5·테스트 EXP 곡선 | 테스트 데이터 유지, 공식 ID로 승격 금지 |
| 초기 파티/리더/편성 | 기획상 아난 드 모르테1명 승인. 실제 공식 ID·성장 정의와 연결 없음 | 구조 요구만 확정, 데이터는 Pending |
| HP/스탯 | CharacterProgressionDefinition은 ID/레벨/EXP/Unique 조건을 소유하며 HP/MP 정의 자산이 아님. Party adapter는 field HP source에서 HP snapshot을 읽음 | 공식 HP/능력치 승인 및 현재 field binding 검토 필요. 임의 스탯 생성 없음 |
| Skill 장착 | CharacterSkillRuntime의 획득·공유 Film·장착 키 → PersistentSkillCombatLoadoutBridge → immutable snapshot | 기존 경로 재사용 가능. 공식 초기 획득/장착 키·시점 미정 |
| Skill 소유/사용 분류 | 기존 실제4개 Skill은 Compatibility. Film/Unique 소유 분류와 Config의 Normal/Clash 분류는 별개 | Compatibility도 조건에 따라 기존 bridge에서 resolve 가능하지만 정식 소유/초기장착 승인으로 간주하지 않음 |
| Film/Unique | 공식 Film/Unique 소유·장착/해금 제작 데이터 없음 | Content Pending, 새 지급 정책 추가 없음 |
| 적 | Dungeon의 기존 Enemy_01/02/03 및 loadout 참조는 존재. EnemyDefinitionSO/AI Profile Asset 없음 | 기존 적 GameObject를 승인된 authored Enemy 종류로 자동 승격하지 않음 |
| Enemy provenance | Encounter_01/Enemy_01, Encounter_02/Enemy_02, Encounter_03/Enemy_03에 EnemySourceComponent 없음 | 실제 Source Key 미확정. 선택 Hand를 쓸 때 source 연결 필수 |
| AI Profile | 기존 타입은 optional authored preference. 일반 적은 기존 유효 스킬 정책 사용 가능 | AI Profile 부재를 Runtime 전체의 필수 시작 실패로 오해하지 않음. 필요한 우선순위는 별도 승인 |

Dungeon EntryPoint가 실제 참조하는 기존 Skill:

| Asset (`Assets/GAME/Data/Skill/`) | Skill ID / persistentKey | Final MP / Clash 위력 | baseDamage / speed | 후보 연결 |
|---|---|---|---|---|
| `Skill_BasicAttack.asset` | 1 / `skill.production.0001` | 1 / 2 | 2 / 10 | 미연결, Normal/Clash·공식 효과 승인 필요 |
| `Skill_skill2.asset` | 2 / `skill.production.0002` | 2 / 3 | 4 / 5 | 미연결, 동일 |
| `Angel_Skill.asset` | 11 / `skill.production.0011` | 1 / 2 | 1 / 5 | 미연결, 적 종류/스킬 승인 필요 |
| `Angel_Skill2.asset` | 12 / `skill.production.0012` | 2 / 3 | 1 / 5 | 미연결, 동일 |

위 값은 **현재 자산 관찰값**, 승인 제안이나 수치 확정이 아니다. 파일명/key에 production이 있다는 사실만으로 승인하지 않는다. demo Skill1/2와 같은 runtime ID를 공유하는 별도 자산도 있어, 향후 registry에는 승인한 정확한 정의만 유일하게 등록해야 한다.

Card RuntimeId는 전투 카드 인스턴스 식별자이며 영구 Skill persistentKey와 다르다. 동일 key에 합법적 여러 카드 copies는 가능하지만 같은 Skill/key를 Normal/Clash 양쪽 규칙으로 중복 배치하는 계약은 거부한다. Save/장착 key 구조는 변경하지 않았다.

## 4. 최소 실행 구성과 필요한 승인 데이터

현재 **승인된 실제 자산만으로 아군1/Normal1/Clash1/적1/양측 snapshot의 최소 구성이 완성되지 않는다.** fixture 통과를 실제 준비 완료로 표시하지 않는다.

| 필요한 데이터/필드 | 승인·작성 대상 |
|---|---|
| 공식 Character ID | 아난 드 모르테의 영구 ID. `hero.test` 재사용/임의 새 문자열 생성 금지 |
| `CharacterProgressionDefinitionSO` | characterId, startingLevel, maximumLevel, experienceRequiredByLevel, 필요 시 uniqueSkillUnlocks |
| `CharacterStartDefinitionSO` | definitions, initialMemberIds[0], initialLeaderId, initialCombatMemberIds[0], defaultRewardTargetId를 같은 승인 ID에 연결 |
| 캐릭터 field HP·능력치/MP | 실제 HP source 및 기본 전투 수치의 플레이테스트 채택 여부. 새 Config 기본값을 개인 스탯 승인으로 간주하지 않음 |
| 초기 Skill 키와 획득/장착 출처 | 실제 Normal/Clash에 사용할 정확한 기존 SO 또는 승인 신규 Skill. persistentKey/SkillId/효과/Final MP/위력/소유 분류 및 장착 시점 |
| 후보 `handCardRules` | 두 종류 규칙의 skill/kind/copies, 제작 seed. 동일 key 양쪽 배치 금지. fixture copies4/6·seed 복사 금지 |
| 적1종 | 기존 Enemy_01 등을 어떤 승인 종류로 사용할지, HP/loadout·실제 스킬·EnemyDefinition.persistentKey 및 필요 시 획득 매핑 |
| 선택 AI/Enemy Hand | AI Profile 필요 여부·priorities, Hand 적용할 때 정확한 Source Key 및 대응 카드 규칙. 일반 적은 opt-out 유지 가능 |
| BP 강화/Ultimate | 실제 Skill의 enhancement kind/perPoint 및 Ultimate Skill/Effect/추가 MP 비용. gauge 단계 구현과 콘텐츠는 별개 |

이 표는 추가 사용자 승인/제작이 필요한 정확한 schema다. 미승인 정의의 새 파일명·GUID·캐릭터 ID를 만들어 채우지 않았다. 공식 시작 정의와 스킬 데이터가 없다는 이전 답변도 유지하며 같은 자료를 재요청하지 않았다.

## 5. Readiness 감사와 안전성

기존 `Assets/GAME/Editor/CombatProductionDataAudit.cs`를 확장했다. 기존 감사·메서드는 유지한다.

- 숫자 검사 `CollectPlaytestNumberIssues`: 비활성 여부, 2/3 손패·BP3/2/3·Clash3/3/3/5, 지원 selector를 검사한다. 빠진 값/NaN/Infinity/잘못된 값을 오류로 보고한다.
- 콘텐츠 검사 `CollectPlaytestContentIssues`: Normal/Clash 규칙 누락, key/copies/kind 오류, 같은 persistentKey 중복 배치, 테스트 Skill/recursive test dependencies 및 Enemy Hand source 중복·미해결을 보고한다.
- `RunPlaytestCandidateAudit` / 메뉴 `GAME/Verification/Audit Production Playtest V1 Candidate`: 기존 start validator를 재사용하고 실제 AssetDatabase inventory에서 공식 시작/적 정의 부족을 보고한다. 결과는 `Logs/CBT-R2-08-P2B3D3-Readiness.md`다.
- 숫자가 모두 맞더라도 콘텐츠가 없으면 **BLOCKED**다. 이후 구조 문제가 없더라도 결과는 **VALIDATION PENDING**이며 자동 Ready/승인 상태를 만들지 않는다. 런타임 장착 snapshot·registry·field HP·초기 획득·Scene 연결은 기존 EntryPoint 검증 및 별도 실제 데이터 검증이 필요하다.
- EnemyDefinition.persistentKey → EnemySourceSnapshot.SourceKey의 정확한 연결을 따른다. 비어 있는 opt-in 목록은 일반 적 경로를 보존하며 임의 key를 생성하지 않는다.
- Runtime 필수 검증은 기존 EntryPoint가 수행한다. Config/스킬/Character snapshot 누락 시 Opening/첫 명령 전에 실패하고 기존 rollback이 정리한다. 새 Editor 감사가 전투 실행기를 대체하지 않는다.

## 6. Owner·호환성·파일 목록

Production Owner는 CombatEntryPoint(시작/끝), CombatSession(상태), StateMachine(phase/승인), Orchestrator(진행), SkillRunner/Outcome Executor(효과), Director(연출), 기존 GameFlow/Input/UI/Inventory/Reward/Save로 유지된다. Runtime C# 및 SerializeField/공개 Runtime API/Save schema 변경 없음. LegacyPlanning 및 Debug opt-in 소스·자산 참조 보존.

이번 수정:

- 기존 **untracked** `Assets/GAME/Editor/CombatProductionDataAudit.cs`에 위 Editor 생성/감사 메서드와 후보 경로 상수 추가. 기존 기능/메타 보존.

추가:

- 새 후보 `.asset` 및 Unity 생성 `.asset.meta` (위 GUID).
- `Assets/GAME/Tests/Editor/CombatProductionPlaytestCandidateTests.cs` 및 Unity 생성 meta, script GUID `e5dcacc469dd1af44a9e3960fb3e0806`.
- 이 보고서. 삭제/이동 없음. 새 폴더/폴더 GUID 없음.

Editor 공개 helper 추가는 위 감사 도구에 한정된다. 기존 public 메서드의 시그니처·namespace·Script GUID·필드 타입을 바꾸지 않았다. 테스트 fixture의 Production 직접 참조 없음. 새 candidate를 기존 Production에 연결하지 않았다.

## 7. 자동 검증 결과

Unity 6000.3.23f1 선택 회귀 **825 PASS / 6 FAIL / 1 IGNORED**, 총832. `Logs/CBT-R2-08-P2B3D3-Regression.xml/.log`, 정확한 filter `...-TestFilter.txt`에 기록했다. 종료2는 기존6개 자산 gate 실패이며 D2 XML과 실패 이름이 동일하다. 이번 선택 범위의 신규 실패 없음. 과거 전체 `1,562 PASS / 8 FAIL / 1 IGNORED` 및 직전 D2 선택 `801 PASS / 6 FAIL / 1 IGNORED`는 이번 결과와 구분한다.

- 새 후보 검사 **24/24 PASS**: 승인 수치, 잘못된 값/누락 selector/NaN·Infinity, 활성화 감지, 콘텐츠 Pending, 독립 GUID·테스트 의존성 없음, 원본 참조 미변경, 중복 Hand 분류 및 테스트 Skill 거부, source-key opt-out/미해결 감지, 재생성 멱등성, 읽기 전용 BLOCKED 감사.
- P2B3A **22/22**, B **15/15**, C **23/23**, D1 **8/8**, D2 **14/14**, P2B2D startup **40/40 PASS**.
- Legacy 관련 **220/220 PASS**: EntryConsolidation36 / FinalExchangeDriver15 / FinalRulesPolicy52 / Foundation2 / ProductionOwnership13 / SessionTurnFlow44 / UIRouting58.
- 기존 CombatDirector 실제 격리 Play Mode/3회 합, Hand/BP/Ultimate·Enemy Hand, Mental/Status/Item UI·Film, Terminal/CompletionId·Reward/참가자 EXP 및 Abort/시간 복구 회귀를 포함했다. **이들은 fixture 검증이며 새 후보에 실제 콘텐츠가 준비되었다는 증거가 아니다.**
- Unity 신규 Editor 코드 컴파일 및 후보 생성 성공(종료0, `...-Create.log`). `error CS`/`warning CS` 없음. 모든 Runtime 강제 재컴파일 또는 Player Build를 수행한 것으로 주장하지 않는다.
- 기존 OfficeFlowController.cs.meta GUID import 문제와 빈 Scenes/Tests/Tests.asmdef 안내는 기존 자산 문제/경고로 별도 기록하며 원본 보존. 예상한 거부 로그 및6개 assertion 실패를 C# 컴파일 오류로 분류하지 않는다.

실제 새 감사 출력은 `Approved numbers: MATCH / DISABLED`, `Readiness: BLOCKED`다. 차단 사유는 (1) Normal/Clash rules·copies·장착 snapshot, (2) 공식 CharacterStart/ID/편성·성장, (3) Enemy definition/source provenance 부족이다. 검사가 Pending을 정확히 보고해서 PASS한 것이며 실제 콘텐츠 준비가 PASS한 것이 아니다.

### 기존 실패6건 — Existing Confirmed / Asset Change Required

공통 namespace `Game.Tests.Integration`:

1. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasCanonicalCombatRewardUiCameraAndBootstrapOwners`
2. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasExplicitInteractionOwnersWithoutLegacyInteractionPaths`
3. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasOneCanonicalRepeatableNpcInteraction`
4. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasQuestDerivedCompletionContractWithoutLegacyExitOrFallbackDestination`
5. `DungeonOneProductionSceneTests.ProductionDungeonOne_IsCleanEnvironmentMigration`
6. `ProductionSceneWiringTests.DungeonTemplate_IsCanonicalSingleOwnerProductionSource`

앞5건은 `Forbidden Demo/Debug/Test/Legacy component remained: Game.Combat.Debugging.CombatHandValidationOptIn on CombatRuntime` 공통 선행 gate다. 마지막은 Template의 상속 BP Debug 존재 검사에서 Expected False / But was True. 상세 메시지·stack은 `Logs/CBT-R2-08-P2B3D3-SixEvidence.txt`에 보존했다. 새 후보 생성만으로 해결되지 않으며, 실제 승인 자산 전환 후 각 테스트의 나머지 조건까지 재검증해야 한다. NPC/Quest/UI 후속 조건이 통과한 것으로 간주하지 않는다.

기존 IGNORED 1건은 `Game.Tests.Integration.PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners`. 별도 쓰기/읽기 Editor 재시작 검증 미실행. 새 Skip/Ignore 추가 없음.

실행 범위는 D2 안전한 선택 filter에 새 후보 검사를 추가한다. 실제 Dungeon Scene Play Mode, 보호 자산 설치/저장 테스트, Player Build 및 Editor 재시작 Save 검증은 수행하지 않는다. 새 후보 검사는 복제본에 잘못된 값/테스트 Skill/중복 분류를 주입할 뿐 저장된 후보나 보호 자산을 바꾸지 않는다. 기존 테스트 삭제·Skip·기대값 변경 없음.

### 보존 최종 확인

시작 SHA-256 manifest 2,578개 중 **관련 감사 코드1개만 의도적으로 수정**, 나머지 **2,577개 동일**, 누락0. Git이 인용 출력한 비ASCII MapTest 경로20개는 시작 status에서 변경이 없었고, 현재 Git blob도 모두 HEAD와 일치함을 별도 확인했다(`...-UnquotedPathEvidence.json`). 새 파일은 후보 SO/meta·테스트/meta·보고서5개뿐이다. 기존 보호 Scene/Prefab/SO/meta, 저장 테스트 fixture, Runtime/Quest/Reward/Save 및 Unity/Package/ProjectSettings 기존 변경을 보존했다. 새 GUID2개 중복 없음. commit/push/PR 없음.

새 C#/보고서 trailing whitespace 검사 통과. `git diff --check`의 기존 Font3곳·Dungeon Scene1곳 trailing whitespace4건 및 기존 줄바꿈 안내는 사용자 변경 보존을 위해 그대로 두었다. Unity 생성 새 NativeFormatImporter meta의 표준 빈 문자열 공백도 임의 재포맷하지 않았다.

## 8. Inspector 연결 및 다음 보호 자산 변경

**아래는 별도 승인·콘텐츠 준비 후의 작업 목록이며 이번에는 실행하지 않았다.**

1. Title/Dungeon `RuntimeBootstrapper.characterStartDefinition`과 CharacterProgressionService.definitions/defaultRewardTargetId를 승인된 공식 시작/성장 정의에 연결한다. PartyRuntime 초기 owned/leader/lineup1명 및 CharacterSkillRuntime 획득·장착 상태를 실제 시작 흐름으로 확인한다.
2. 승인된 Normal/Clash SO를 Dungeon `Runtime/Combat/CombatRuntime`의 `CombatEntryPoint.skillDefinitions`와 후보 Config.handCardRules에 정확히 연결한다. candidates의 copies/seed·필요한 selector/선택 콘텐츠를 검증한다. 장착 snapshot이 registry와 일치하고 key/ID가 중복되지 않아야 한다.
3. `Assets/GAME/Scenes/Dungeon_1_Production.unity`의 `World/Encounters/Encounter_01/Enemy_01` 등을 실제 사용할 승인 적으로 결정하고 loadout/HP/source definition·선택 AI Profile을 연결한다. Hand 미적용이면 authored skill 후보를 유지하고 opt-in이면 Source Key를 정확히 맞춘다.
4. 충분한 데이터와 별도 승인 전까지 EntryPoint.finalExchangeRuntimeConfig를 새 후보에 교체하지 않는다. 현재 참조 `Assets/GAME/Data/Combat/Runtime/FinalCombatRuntimeConfig_V1.asset` 유지. 후보도 enable=false 유지한다.
5. 향후 보호 자산 대상은 기존 `Assets/GAME/Prefabs/CombatRuntime.prefab`의 base BP Debug, Dungeon instance의 added Hand/Clash/BP Debug, 상속받는 `Assets/GAME/Scenes/Dungeon_Template.unity`, 관련 승인 Config/서비스 연결이다. 카메라 추가 컴포넌트·기존 UI/World/Reward/registry override는 보존한다.
6. D2 보고서의 변경 직전 작업 사본·해시·UnityEvent/override 기록과 부분 복구 절차를 따른다. 실제 변경 승인은 별도이며 Apply All/Revert All/HEAD reset으로 사용자 변경을 덮어쓰지 않는다.

실제 전환 가능 여부: **아직 불가(Content/Contract/Asset Change Approval/Inspector Pending)**. 숫자 승인과 후보 생성만으로 공식 콘텐츠 또는 보호 자산 변경을 승인받았다고 해석하지 않는다.

## 9. 향후 수동 검증 순서

현재 Inspector/실제 Production Play Mode는 미실행이다. 데이터 승인·연결·별도 보류 해제 이후:

1. New Game에서 공식1명/리더/편성과 HP·초기 획득·장착을 확인한다. TestHero/fixture fallback 없이 snapshot이 준비되어야 한다.
2. Contact와 FieldAttack 각각 EntryPoint로 진입해 초기 명령 전 Hand2/3·BP0·Ultimate0·Clash3/3/3/5를 확인한다.
3. 실제 승인 MP/카드 조건에서 1~3회 합·조기 소진·동점·Timeout5초를 검증한다. 3회를 위해 MP를 강제 지급하지 않는다. 최종 피해/상태/Mental은1회여야 한다.
4. Chain/인계·유효 BP 주기·궁극기·Item/Film 및 Enemy Hand 선택 경로를 확인한다. 미제작 선택 기능은 사용 가능으로 표시하지 않는다.
5. Abort/Pause/Scene 전환의 명령·타이머/timeScale/fixedDeltaTime·입력/UI/필드 잠금 복구와 Terminal/CompletionId·Reward/참가자 EXP1회를 확인한다. 실제 Save/Continue/재시작·빌드는 별도 검증이다.

CBT-R2-05/06A 수동 보류는 유지한다. 격리 자동 검증이 실제 Production 수동 통합을 대체하지 않는다.

## Development tracking

- System: Combat / Production Playtest data readiness.
- Requirement: CBT-R2-08-P2B3D-3 승인 V1 수치 및 최소 실제 콘텐츠 준비.
- Owner: 기존 EntryPoint/Session/StateMachine/Orchestrator/SkillRunner/Director 및 각 서비스 유지.
- Implementation: **Config 후보/감사 구현 완료. 최소 실제 Character/Skill/Enemy 콘텐츠 세트 Content Pending**.
- Compile/자동 검증: 최종 기록 참조.
- Inspector/serialized asset: 새 SO import/숫자/참조 검사와 실제 Scene 연결 검증을 구분. 실제 연결 Pending.
- Play Mode/Integration: 기존 격리 회귀 범위만 검증. 실제 Dungeon 및 콘텐츠 연계 Unverified.
- Recommended: **Playtest Config Candidate Verified — Minimum Production Content / Approval / Inspector & Manual Integration Pending**.
- Production 초기화 비활성, 보호 자산/Legacy/05·06A 보류 보존. Cutover 또는 전투 시스템 전체 완료 아님.
