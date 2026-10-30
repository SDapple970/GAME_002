# CBT-R2-08-P2B2D — 승인된 Hand/BP/Clash 초기화 계약 구현

2026-10-30 KST. 승인 정책 A/B/C를 기존 Production 코드에 연결했다. **Production 설정 활성화, 보호 Asset 편집, Debug 제거, Cutover는 수행하지 않았다.**

## 지침·기준선·조사

- 활성 지침: 루트 `AGENTS.md`; 중첩 AGENTS/override 없음. Branch `main`, HEAD `6fa26d294472574097d33b884858ba0ee8bdc8b7` 유지.
- Unity: `ProjectSettings/ProjectVersion.txt` 및 실제 실행 Editor 모두 6000.3.23f1. Unity/Package 변경 없음.
- 시작 시 tracked/untracked 상태와 SHA-256 목록을 `Logs/CBT-R2-08-P2B2D-BeforeStatus.txt`, `...-BeforeHashes.json`에 기록했다. 기존 Scene/Prefab/폰트/TimeManager, Quest/Reward/Save 관련 변경 및 앞 Batch의 코드를 기반으로 최소 확장했다.
- P2B1/P2B2/P2B2C 보고서와 Config, EntryPoint/Bootstrapper/Session/StateMachine/Orchestrator, Hand/BP/Clash Runtime, CharacterSkillRuntime 및 loadout bridge, Enemy policy/controller/source snapshot, Debug opt-in 및 실제 Scene/Prefab/Config/meta를 조사했다.
- 기존 전체 회귀 **1,562 PASS / 8 FAIL / 1 IGNORED**는 이전 전체 기준선이다. 이번 선택 결과와 구분한다.

## 구현 결과와 소유권

### A. Enemy Hand 선택 적용

`FinalCombatRuntimeConfigSO.enemyHandSourceKeys`에 명시된 기존 `EnemyDefinitionSO.PersistentKey` / `EnemySourceSnapshot.SourceKey`와 일치하는 적만 Hand를 생성한다. 목록은 기본 빈 배열이며 일반 적에게 Hand를 강제하지 않는다. 같은 encounter에 Hand 적과 일반 적을 함께 사용할 수 있다. 타입별 선택이며 동일 Source Key의 인스턴스에는 같은 정책을 적용한다.

일반 적도 등록된 authored skill snapshot, BP enhancement/Ultimate mapping 검증은 유지한다. Hand 전용 카드 rule/draw 가능 검사만 적용하지 않는다. Hand 미사용은 빈 Hand 생성과 다르다. Hand 적용 적은 기존 Controller/FirstValid/Conditional policy와 Orchestrator availability/commit을 통해 카드 종류·MP·BP·행동 권한을 검사한다. 빈 Hand는 기존 행동권 반환 또는 대응 포기/Clash timeout 경로를 사용한다. 별도 Player UI나 새 AI 실행기를 만들지 않았다. 적 결정은 적 자신의 Hand/skills와 공개 목표 상태를 사용한다.

### B. Skill 분류 및 식별

Film/Unique/Compatibility 소유·획득/해금 정책은 그대로다. Normal/Clash는 기존 `handCardRules[].kind`에서 결정한다. 기존 한 Skill당 한 rule 및 persistentKey 중복 거부를 유지하므로 동일 Skill의 양쪽 동시 배치는 거부된다. copies로 생성된 같은 종류의 카드들은 Session 내 서로 다른 RuntimeId를 갖는다. persistentKey/SkillId를 카드 Instance ID로 사용하지 않는다.

장착 snapshot/Save 구조, MP·카드·BP 승인 경계와 receipt 중복 방지는 변경하지 않았다. 기존 CharacterSkillRuntime/Unique unlock 및 Save 회귀로 호환성을 확인한다. Debug/Legacy의 기존 core API 의미를 바꾸기 위해 HandRuntime 전체에 새 영구 데이터 모델이나 분류 체계를 추가하지 않았다.

### C. 필수 Clash 초기화

호출 순서는 다음과 같다.

`Encounter(Contact/FieldAttack) → CombatEntryPoint → roster/Character ID 및 loadout snapshot → CombatBootstrapper 사전 callback → 모든 Hand/BP/Ultimate/Clash 설정 검증 → 선택된 actor Hand → BP → Ultimate → Session의 Clash 설정 snapshot 및 성공 표식 → Opening Effect/첫 Tick → 전역 흐름 동기화 → Orchestrator bind 시 Clash 설정 설치 → 첫 command pump → Director/Enemy startup → OnCombatStarted → Enemy 요청 활성화`.

새 Production Hand/BP 모드에는 양수 Durability/비교 상한, 유한한 양수 timeout과 명시적 skill 선택 정책이 필수다. 누락/잘못된 값과 Orchestrator 누락은 Opening Effect 이전에 거부한다. ClashSeries 자체는 기존대로 첫 공격/대응 commit 후 생성된다. 반복 합 Durability와 Posture를 합치지 않았다.

Session에는 수치 snapshot만 저장하고, Orchestrator가 기존 Clash 구성 로직을 재사용해 첫 pump 전에 설치한다. 정식 초기화는 `ClashSeriesValidationEnabled` Debug 이벤트를 발행하지 않는다. 기존 공개 `TryEnableClashSeriesValidation`은 기존 검증 경로의 이벤트를 유지하되 Production 성공 Session 덮어쓰기는 거부한다. Hand/BP/Clash Debug 컴포넌트의 기존 marker 검사도 보존했다.

Bootstrapper callback 실패 시 기존 `AbortStartup`이 부분 Hand/BP/Ultimate 및 새 Clash snapshot을 폐기한다. EntryPoint의 기존 rollback은 UI/흐름/바인딩을 정리한다. Orchestrator bind가 예외를 내더라도 rollback에 포함되도록 cleanup flag를 호출 전에 설정했다. GameState 직접 writer나 새 Manager는 추가하지 않았다.

Production Owner는 **CombatEntryPoint(시작/종료), CombatSession(상태), CombatStateMachine(phase/승인), CombatFlowOrchestrator(진행), 기존 SkillRunner/효과 실행기**다. Enemy Controller, GameFlowController, UI/Input, Inventory/Reward 및 LegacyPlanning 소유권 유지.

## Config·API·직렬화

기존 SerializeField 이름/타입과 enum 값은 모두 유지한다. 추가된 필드는 기존 Config 타입 안에만 있다.

| 추가 필드 | 작성 계약 / 누락 기본값 |
|---|---|
| `enemyHandSourceKeys` | 기존 Enemy source key 목록, 공백/중복 거부. 빈 배열=적 Hand 미사용 |
| `clashAttackerDurability`, `clashResponderDurability`, `clashMaxComparisons` | 활성 시 양수 필수. 기본 0은 미작성 |
| `clashDecisionDurationSeconds` | 유한한 양수 필수. 기본 0은 미작성 |
| `clashFinalSkillPolicy` | `Unspecified=0` 거부. 제작자가 `LastCommittedWinningSkill=1`을 명시 선택 |
| `clashTimeoutAttackPolicy` | `Unspecified=0` 거부. 제작자가 `FirstAvailableSkill=1`을 명시 선택 |

선택 정책은 기존 core의 허용된 committed skill / availability 계약을 사용한다. 임의 정식 기본 선택이나 Debug 숫자를 자동 주입하지 않는다. 기존 `enableProductionHandBp=false` Asset은 새 필드가 없어도 기존 동작을 유지한다. 과거 활성 fixture의 Hand/BP-only 가정은 이번 승인 C에 따라 더 이상 유효하지 않으며 명시적 Clash 데이터를 공급하도록 테스트를 확장했다. 과거 활성 프로필을 가져오려면 적 Hand 대상도 source key 목록으로 명시해야 한다. 현재 보호 Production Config는 비활성이라 실제 자산 마이그레이션은 하지 않았다.

기존 공개 메서드 삭제/변경 없음. 새 Config getter와 두 개의 중첩 enum만 추가했다. Session의 Clash snapshot은 internal runtime property이며 SO 진행 상태가 아니다. 기존 GUID/meta 및 Save schema 무변경.

## 이번 수정 파일

1. `Assets/GAME/Scripts/Combat/Data/FinalCombatRuntimeConfigSO.cs`: 명시적 Enemy opt-in 및 필수 Clash 작성 데이터.
2. `Assets/GAME/Scripts/Combat/Runtime/Core/CombatEntryPoint.cs`: actor별 Hand 선택, Clash 사전 검증/Session 준비 및 bind 실패 cleanup.
3. `Assets/GAME/Scripts/Combat/Runtime/Core/CombatFlowOrchestrator.cs`: 공통 Clash 설정 재사용, 첫 pump 전 Production 설정 설치, Debug overwrite 거부.
4. `Assets/GAME/Scripts/Combat/Runtime/Model/CombatSession.cs`: 전투별 Clash snapshot과 AbortStartup 정리.
5. `Assets/GAME/Tests/Editor/CombatProductionHandBpStartupTests.cs`: 기존 18건의 새 승인 계약 데이터 보강 및 새 집중/격리 Play Mode 검사.

추가: 이 보고서와 무시되는 Logs 검증 기록. 테스트 파일은 시작 시 이미 untracked였던 P2B1 파일에 이어 수정했다. 삭제/이동/새 meta/새 Runtime 타입 없음. 기존 테스트를 삭제·Skip·완화하지 않았다.

## 자동 검증

최종 실행 결과는 아래 기록에 기재한다. P2B1의 18건은 명시적 fixture 설정과 기존 모든 주요 assertion을 유지하며 다시 실행한다. 기존 Debug overwrite 검사는 승인 C에 맞게 Production Clash 설정이 유지됨을 검사하도록 변경했다. 실패 주입 검사는 실제 Production initializer의 전체 준비 이후 예외를 발생시켜 cleanup 범위를 강화했다.

검사 범위: 1~3인 snapshot/초기화, Contact/FieldAttack start reason, 선택적 적 Hand와 일반 authored skill, 빈 적 Hand의 행동권 반환, 카드 ID/MP/BP/stale 거부, Film 및 실제 해금된 Unique/card kind 독립성, dual rule 거부, Clash 누락/0/NaN/Infinity/unknown policy, Opening Effect 이전 실패, 부분 runtime 폐기, 재진입/재전투, 첫 command의 Clash 준비 상태, authored durability 및 timeout, stale round token, Debug 충돌, 비활성 Config와 Legacy, 실제 unsaved fixture Play Mode.

전체 회귀는 `CharacterSkillProductionWiringTests.RepeatInstallation_DoesNotResaveSceneOrSharedUI`가 보호 자산 설치/저장 함수를 호출하므로 실행하지 않는다. 안전한 기존 선택 filter에 소유/해금/저장 집중 회귀를 더했다. 실제 Production Inspector·수동 Play Mode, Player Build, 디스크 재시작 Save 검증은 미실행이다.

### 기존 실패 6건

공통 namespace `Game.Tests.Integration`, 상태 **Existing Confirmed / Asset Change Required**:

1. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasCanonicalCombatRewardUiCameraAndBootstrapOwners`
2. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasExplicitInteractionOwnersWithoutLegacyInteractionPaths`
3. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasOneCanonicalRepeatableNpcInteraction`
4. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasQuestDerivedCompletionContractWithoutLegacyExitOrFallbackDestination`
5. `DungeonOneProductionSceneTests.ProductionDungeonOne_IsCleanEnvironmentMigration`
6. `ProductionSceneWiringTests.DungeonTemplate_IsCanonicalSingleOwnerProductionSource`

앞 5개는 기존 Scene의 Hand Debug 선행 gate, 마지막은 Template→CombatRuntime Prefab의 BP Debug gate다. 개별 NPC/Quest/Reward의 실제 신규 결함으로 재분류하지 않는다. Validator 약화나 serialized reference 제거로 숨기지 않았다.

## Pending·P2-B3·수동 검증

승인 완료: Enemy 선택 적용, 소유/전투 카드 분류 독립 및 동시 양쪽 배치 금지, 새 Production Hand/BP의 Clash 필수. 미승인: 정식 슬롯 수, copies/seed, BP 밸런스, Clash durability/비교 수/timeout, 공식 Character/Enemy/Skill 및 궁극기/총공격 데이터. 지원되는 selector를 실제 제작 프로필에 선택하는 작업도 남아 있다. 새 enum 지원이 정식 제작값 승인이나 Production 활성화를 의미하지 않는다.

P2-B3 전에 공식 ID/장착·적 snapshot/registry, 모든 Hand 사용 actor의 단일 rule, BP/Clash 작성값과 selector, optional Ultimate를 준비하고 검증해야 한다. Enemy Hand가 필요한 타입은 기존 EnemyDefinition persistentKey 및 SourceComponent를 통해 source snapshot이 확보되어야 한다.

보호 자산 후보는 기존 계획대로 `Assets/GAME/Data/Combat/Runtime/FinalCombatRuntimeConfig_V1.asset`, `Assets/GAME/Prefabs/CombatRuntime.prefab`, `Assets/GAME/Scenes/Dungeon_1_Production.unity`, `Assets/GAME/Scenes/Dungeon_Template.unity`다. 실제 참조: Dungeon의 `Runtime/Combat/CombatRuntime`에 Hand/Clash/BP Debug, 원본 CombatRuntime prefab에 BP Debug. 기존 Config GUID `55e6978e3cf88ca4994f7855f907bc6c`, prefab GUID `1bfc52cd235e809449d17d48571f5a38` 유지. 원본과 added component/instance override를 구분한다. 이번에 편집/제거하지 않았다.

별도 승인 후 Inspector/수동 절차:

1. 작업 직전 사용자 변경을 포함한 사본/해시/override/UnityEvent를 기록한다. 공식 registry·Character ID와 snapshot, Config 참조 및 새 필드를 확인한다.
2. 활성화 전 잘못된 Clash 설정에서 시작 거부·Exploration 유지·Opening Effect/비용/명령/UI 무변경을 확인한다.
3. 승인 Config로 Contact/FieldAttack에서 첫 명령 이전 Hand/BP/Clash 준비, 일반 적 no-Hand와 opt-in 적 카드 제한, 빈 Hand/MP 부족/timeout/stale 거부를 확인한다.
4. Normal/Clash 단일 배치와 card instance별 승인 소비, 반복 합 durability/최종 피해 1회, 인계 시 손패·BP 유지, Ultimate 및 재전투 reset을 확인한다.
5. Debug 제거/비활성 전환은 별도 승인된 P2-B3 범위에서만 수행한다. Missing Script/Reference, 6개 gate의 후속 조건, Pause/Abort/Reward/EXP/Input/UI 복귀와 Save/Continue를 확인한다.
6. 문제 시 HEAD reset 대신 작업 직전 사본에서 승인된 변경만 복구한다. 원본 prefab/Scene override 및 meta/GUID를 함께 대조한다.

CBT-R2-05/06A 보류는 유지한다. 실제 Production 수동 검증과 위 자산 전환은 이번 격리 fixture의 통과로 대신하지 않는다.

## Development tracking

- System: Combat / Production Hand-BP-Clash startup.
- Requirement: CBT-R2-08-P2B2D 승인 정책 A/B/C.
- Owner: CombatEntryPoint → Bootstrapper → CombatSession / CombatStateMachine / CombatFlowOrchestrator.
- Implementation: 승인 계약 코드 연결 완료, 정식 콘텐츠·밸런스 작성 Pending.
- Compile/자동 검사: 아래 최종 실행 기록.
- Inspector: 실제 Production 연결 미검증; 보호 자산 무변경.
- Play Mode: 격리 fixture 자동 검사와 실제 Production 수동 검증 구분. 후자 Pending.
- Integration/Cutover: 미실행. LegacyPlanning/Debug 호환 경로 유지.
- 권장 상태: **Implementation Complete — Compile/Isolated Tests Verified; Production Content / Inspector / Integration Pending**.
- 문서 변경: P2B1의 Hand/BP-only 성공, 전 적 Hand 필수 및 P2B2C의 A/B/C 미승인 표시는 역사적 상태다. 현재 기준은 이번 승인 지시와 이 보고서다.

## 최종 실행 및 보존 기록

Unity 6000.3.23f1 최종 선택 회귀: **720 PASS / 6 FAIL / 1 IGNORED**, 총727. 기록: `Logs/CBT-R2-08-P2B2D-RegressionFinal.xml`, `.log`, 정확한 선택 범위 `...-TestFilter.txt`. 종료2는 기존 6개 Asset gate 실패를 반영한다. 이전 P2B2C 결과와 실패 이름을 직접 비교했으며 차이 없음, 신규 실패 없음(선택 범위).

- 초기화 집중 **40/40 PASS**: 기존18 + 추가22. 실제 unsaved EnterPlayMode fixture 2개 포함. Production 모드의 반복 합 durability/timeout/오래된 round 명령 및 abort 후 binding/timer 정리를 검사했다.
- 기존 Legacy 관련 **220/220 PASS**: EntryConsolidation36, FinalExchangeDriver15, FinalRulesPolicy52, Foundation2, ProductionOwnership13, SessionTurnFlow44, UIRouting58.
- Enemy behavior policy9/9 및 실제 격리 Play Mode1/1, Hand Runtime9/9, Hand Debug6/6, BP15/15, P2-A 초기화5/5 유지.
- 소유/저장 보강: SharedFilm6/6, UniqueUnlock11/11, FilmSaveEquip17/17, FilmUniqueProductionIntegration9/9. 기존 Item UI 및 Film Contact/FieldInput/ColdSaveLoad 검사도 통과했다.
- Ignored는 기존 `Game.Tests.Integration.PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners`. 별도 Editor 쓰기/읽기 프로세스 재시작 요건으로 이번에 실행하지 않았다. 새 Skip은 없다.
- Unity C# 컴파일 오류 없음. 첫 Runtime 재컴파일의 기존 경고4종: FieldEnemy CS0067, RescueNpcActor CS0414, StoryInteractionController CS0414 2개. 최종 Editor 테스트 변경도 컴파일됐다. Scene gate 예외와 컴파일 오류는 구분한다.
- 초기 확인33/33, 확장37/37 및 중간 선택718/6/1은 중간 실행이다. 최종40/40 및720/6/1로 보고한다. 전체1,562/8/1은 과거 기준선이며 이번 전체 실행 결과가 아니다.

기존 실패 원문은 `Logs/CBT-R2-08-P2B2D-SixEvidence.txt`에 보존했다. 첫5개 오류는 `Forbidden Demo/Debug/Test/Legacy component remained: Game.Combat.Debugging.CombatHandValidationOptIn on CombatRuntime`; 마지막은 Template의 Debug 검출 predicate `Expected: False / But was: True`다. 6건 모두 **Asset Change Required** 유지.

최종 SHA-256 비교: 시작1,800개 중 의도한 코드/테스트5개만 변경, 나머지 **1,795개 동일**. 보호 Scene/Prefab/SO, 기존 meta/GUID, Packages/ProjectSettings, 그 밖의 dirty/untracked 파일을 보존했다. Git 상태 목록의 추가 차이는 이 보고서 한 개뿐이다. 해당 코드 범위 `git diff --check`와 untracked 테스트/보고서 공백 검사 통과. 저장소 전체의 기존 폰트/Scene 공백은 수정하지 않았다. 삭제/이동, commit/push/PR 없음.

구현 상태: **승인 계약 Implementation Complete / Compile Verified / Isolated Fixture Verified**. 실제 Production Inspector/수동 Play Mode/통합은 **Pending**. 정식 콘텐츠·수치 승인, 자산 전환 및 Production 활성화 조건을 충족한 것으로 기록하지 않는다.
