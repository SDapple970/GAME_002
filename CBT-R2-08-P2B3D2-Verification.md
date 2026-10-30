# CBT-R2-08-P2B3D-2 — Production 자산 전환 예행 검증

2026-10-31 KST. **실제 CombatRuntime Prefab의 비활성 메모리 복제본에서 Debug 없이 정식 초기화 및 반복 합을 검증했다.** 보호 자산 원본, Production 활성화, 실제 Dungeon 실행 및 Cutover는 변경·수행하지 않았다. 예행 검증 통과와 실제 자산 전환 가능 상태는 구분한다.

## 1. 지침·Git·작업 범위

- 적용 지침: 루트 `AGENTS.md` 및 현재 사용자 지시. 적용되는 중첩 `AGENTS.md`/`AGENTS.override.md` 없음.
- Branch `main`, HEAD `6fa26d294472574097d33b884858ba0ee8bdc8b7`. ProjectVersion 및 실행 Editor는 Unity `6000.3.23f1` 유지.
- 시작 기록: `Logs/CBT-R2-08-P2B3D2-BeforeStatus.txt`, `...-BeforeHashes.json`. 기존 tracked/untracked 및 Quest/Reward/Save/Unity 자산 변경을 보존한다.
- P2B3D1 보고서, 실제 Config SO, EntryPoint/Bootstrapper/Session/Orchestrator/Director, WorldLifecycle/RewardUIBinder, Debug opt-in 및 Scene YAML의 원본·추가 컴포넌트·참조를 조사했다.
- 편집 전 테스트 코드·보고서만 추가하는 범위와 직렬화 위험, 보호 Scene 실행 제외, 메모리 복제 검증 계획을 보고했다. Runtime 코드 및 기존 테스트 수정 없음.

## 2. 격리 방법과 확인 범위

`PrefabUtility.LoadPrefabContents`로 기존 Prefab을 읽고 비활성화한 뒤 **저장되지 않는 메모리 GameObject 복제본**만 만든다. Prefab contents는 save 없이 unload한다. 복제본을 새 빈 격리 Scene에 옮기고 base BP Debug를 제거한다. Dungeon의 추가 Hand/Clash/BP는 base 제거 이후의 순차 전환을 모델링하여 비활성 복제본에서만 추가·제거한다. 이 검사는 **실제 Dungeon Scene instance override를 복제하여 실행한 검사가 아니다.** 실제 Camera/UI/Encounter/UnityEvent 통합도 별도 검증 대상이다.

기존 B3B fixture의 일시적 Skill/CharacterSkillRuntime/참가자/Config 데이터를 재사용한다. 그 fixture의 임시 전투 owner는 삭제한 후 실제 Prefab 복제본을 활성화하므로 중복 실행 Owner를 두지 않는다. 기존 Prefab 내부 EntryPoint→Orchestrator/Director 참조를 그대로 검증한다. 복제본에는 기존 RewardUIBinder/WorldLifecycle/FieldLock도 남는다. Fixture Skill ID·캐릭터 ID·effect·MP·seed·카드 copies는 Production 콘텐츠가 아니다.

EditMode에서는 일반 MonoBehaviour의 Awake/OnEnable이 Play Mode처럼 자동 실행되지 않으므로 RewardUIBinder의 기존 수명주기를 명시적으로 재현했다. Reward/GameFlow가 Exploration 복귀를 처리하며, 테스트가 별도 전역 상태 writer나 수동 unlock 경로를 만들지 않는다. 실제 Play Mode 검사는 해당 컴포넌트의 자동 수명주기를 사용한다.

신규 집중 검사 **14/14 PASS** (`Logs/CBT-R2-08-P2B3D2-Focused2.xml/.log`):

| 검사 | 결과·증거 |
|---|---|
| 원본 Prefab/Scene 참조·override footprint | 원본 BP 및 Dungeon 추가 컴포넌트, source GUID, 비활성 Config/빈 registry 확인 |
| base/Scene 추가 Debug의 순차 분리 2건 | 메모리 복제본만 제거, 원본 BP 존재 유지, 기존 Owner 연결 유지 |
| Debug 제거만 수행 | 원본 disabled Config로는 Hand/BP/Clash 승인 모드가 활성화되지 않음 |
| Config/Clash/Character/Enemy skill/registry 누락 5건 | 시작 거부, Session/명령 미발행, HP 및 Exploration 유지 |
| 1/2/3인 초기화·Enemy Hand 선택 3건 | 각 Hand 2/3, BP0, Ultimate0, source-key opt-in 및 Abort 정리 |
| 수동 presentation gate로 3회 비교 | 비교 중 피해 없음, 최종 HP10→9·Mental100→85·상태1회, 중복 완료 거부, Chain 진입 |
| 실제 Director 격리 Play Mode | 3인 fixture, 3회 비교→Chain, Abort 후 Session/명령 정리·timeScale1·fixedDeltaTime 보존·필드 잠금 해제·Exploration 복귀 |

최초 집중 실행은 11 PASS/3 FAIL이었다. 실패는 EditMode에서 RewardUIBinder enable 수명주기를 생략해 Exploration 복귀 구독이 없던 **신규 fixture 문제**다. 동일 실행의 실제 Play Mode 복제본 검사는 통과했다. 기존 Owner의 Awake/OnEnable을 새 fixture에 재현한 뒤 14/14 통과했다. Runtime·기존 테스트 기대값·보호 자산은 수정하지 않았다.

## 3. 실제 조사한 자산과 분리 대상

| 자산 / Hierarchy | 현재 serialized 구성 | 향후 별도 승인 작업 |
|---|---|---|
| `Assets/GAME/Prefabs/CombatRuntime.prefab` / `CombatRuntime` | source GUID `1bfc52cd235e809449d17d48571f5a38`; BP Debug fileID `3049129679861795314` | 정식 데이터 준비 후 base BP Debug 분리. Entry/Director/Orchestrator/RewardUIBinder/WorldLifecycle/FieldLock 유지 |
| `Assets/GAME/Scenes/Dungeon_1_Production.unity` / `Runtime/Combat/CombatRuntime` | source instance `1665839071176935753`, `m_RemovedComponents=[]`; 추가 Hand `1665839071176935759`, Clash `1665839071176935761`, BP `1665839071176935766` | base의 inherited BP와 Scene-added 3개 Debug를 각각 처리. 현재 실제 실행 횟수는 수동 검증하지 않음 |
| 위 Dungeon instance | 추가 Camera Controller `1665839071176935757`; registry에 기존 Skill1/2/11/12 override | Camera와 기존 registry·UnityEvent·UI/World 참조 보존. 4개 Skill 존재만으로 공식 장착 snapshot을 확보한 것으로 보지 않음 |
| `Assets/GAME/Scenes/Dungeon_Template.unity` / `Runtime/Combat/CombatRuntime` | 같은 Prefab source, 추가 Camera, base BP 상속 | base 수정 전파와 instance override 확인. Template 자체에 Scene-added Hand/Clash/BP를 새로 만들지 않음 |
| `Assets/GAME/Data/Combat/Runtime/FinalCombatRuntimeConfig_V1.asset` | GUID `55e6978e3cf88ca4994f7855f907bc6c`, 기존 EntryPoint 참조 | 승인된 제작 데이터 작성과 활성화는 별도 승인 대상. 이번에는 원본 무변경 |

Debug script GUID: Hand `bce1360c580248db935ccf2b6f845871`, Clash `040a0e4f14ef4872ac50661f4ee830da`, BP `4bd31fad07c44e9795d18530d37a6855`. 원본 직접 참조는 Prefab과 Dungeon에서 확인되며 Template은 Prefab 상속의 영향을 받는다. Debug 클래스·fixture·LegacyPlanning은 삭제하지 않는다.

`enabled=false`만으로는 serialized Debug 존재 gate를 해결하지 못한다. 정식 데이터 없이 제거하면 이전 Debug 카드/BP 주입도 없어지므로, 단순 제거를 기능 전환 완료로 취급하지 않는다.

## 4. Config와 Production 데이터 부족분

기존 테스트 프로필 `Assets/GAME/Tests/Editor/Fixtures/CombatPlaytestV1/FinalCombatRuntimeConfig_ClashPlaytestV1.asset`은 숫자 참고 및 격리 fixture 전용이다. 기존 파일·GUID `77fff0b326a327943b8f564b26f03718`·비활성 상태를 보존했다. **Production에 직접 연결하지 않는다.** 새 Production SO나 중복 Config 시스템을 만들지 않았다.

| 설정/API | 승인값 또는 현재 규칙 | 실제 적용 조건 |
|---|---|---|
| `normalHandCapacity` / `clashHandCapacity` | 2 / 3 | 승인 Playtest V1 |
| `bpMaximum` / `bpRoundCyclesPerGain` / `bpMaximumInvestment` | 3 / 2 / 3 | 유효 D12 RoundCycle 사용, 프레임/ClashRound가 아님 |
| 초기 BP / Ultimate | Runtime 초기0, 승인 누적 소비1/2/3 단계 | 숫자 전용 새 SO 필드 불필요 |
| `clashAttackerDurability` / `clashResponderDurability` | 3 / 3 | Posture와 구분 |
| `clashMaxComparisons` / `clashDecisionDurationSeconds` | 3 / 5초 | 3회는 상한; MP·카드가 부족하면 기존 무대응/Timeout 정책 |
| `clashFinalSkillPolicy` / `clashTimeoutAttackPolicy` | 지원 selector LastCommittedWinningSkill / FirstAvailableSkill | 기존 승인 정책 명시, Unspecified는 누락 |
| `enableProductionHandBp` | 기존 Production 및 저장된 테스트 SO 모두 false | 이번 작업에서 활성화하지 않음 |
| `handCardRules` / `handSeed` | 실제 Skill 참조·Normal/Clash 단일 분류·양수 copies·seed 필요 | 제작 데이터/정책 미정. 동일 Skill 양쪽 배치 금지 |
| `enemyHandSourceKeys` | 정확한 authored Source Key만 opt-in | 일반 적의 authored skill AI 경로 유지 |
| `ultimateSkills` / Skill BP enhancement | 실제 Skill/Effect/MP 비용·enhancement kind/perPoint 별도 | Content Pending; fixture 효과/계수 복사 금지 |

원본 Production Config는 Hand/BP opt-in false, Hand rules 비어 있음, 필수 Clash 필드가 0/Unspecified다. base Prefab registry도 비어 있고 Dungeon registry override는 별도다. **Debug 제거만으로도 이전 disabled 모드가 시작될 수 있지만 승인 모드 초기화 성공을 의미하지 않는다.**

CBT-R2-07 read-only 감사 재사용 결과의 주요 제작 차단 조건:

- `CharacterStartDefinitionSO` 없음. Title/Dungeon bootstrap의 `characterStartDefinition` 미지정. 유일 성장 데이터 `Assets/GAME/Data/Test/CP_TestHero.asset`/`hero.test`를 공식 데이터로 승격하지 않는다. 실제 Party Character ID와 CharacterSkillRuntime 장착 snapshot이 필요하다.
- `EnemyDefinitionSO` 및 `FinalCombatEnemyBehaviorProfileSO` 없음. Dungeon의 `World/Encounters/Encounter_01/Enemy_01`, `Encounter_02/Enemy_02`, `Encounter_03/Enemy_03`에 `EnemySourceComponent`가 없다. 기존 loadout/AI와 공식 source-key 데이터 연결을 검토해야 한다.
- Item/CombatItem catalog·definition 없음, 감사에서 CombatItemUseAdapter 누락. Inventory/Item 콘텐츠 연결은 별도이며 Combat가 소유하지 않는다.
- Status definition 4개는 모두 Tests/Editor/Fixtures. 공식 Film/Unique/Ultimate/AllOut 및 추가 MP·효과 제작 데이터는 여전히 Content Pending이다. 이 선택 기능 전체가 Hand/BP startup의 필수 조건이라고 단정하지 않는다.
- base Prefab의 RewardUIBinder rewardService/panel, WorldLifecycle camera/formation 등은 null이며 실제 Scene/서비스 연결과 런타임 해석을 별도로 확인해야 한다. 메모리 복제본의 경고를 UI/보상 통합 완료 근거로 사용하지 않는다.

## 5. 기존 Owner 및 호출 순서

기존 `CombatEntryPoint.StartCombat`의 정규화·검증 → Enemy provenance 준비 → `CombatBootstrapper` → factory roster 및 `CombatSession`/`CombatStateMachine` 구성 → callback에서 roster·Character snapshot 검증 및 Hand→BP→Ultimate→필수 Clash 준비 → FinalExchange Opening Effect → 최초 Tick/Standoff 순서다.

이후 EntryPoint가 ActiveSession/StateMachine 게시·World lifecycle 준비·GameFlow 동기화 → Orchestrator bind(첫 요청 전 Clash 설치) → Director 및 Enemy controller bind → OnCombatStarted → Enemy startup request 활성화를 수행한다. 실패 시 기존 AbortStartup/rollback을 재사용한다. LegacyPlanning의 별도 Opening 순서/호환 경로도 유지한다.

피해·Status·Mental은 기존 SkillRunner/Outcome Executor에서 최종 결과당 적용하며 Director는 이미 판정된 결과를 표현한다. Terminal은 기존 EntryPoint completion으로 끝나고 RewardService가 CompletionId를 사용한다. GameState/Input/UI/필드 복구 Owner를 새로 만들지 않았다.

## 6. 실제 자산 전환 준비도

| 사용자 조건 | 현재 판단 |
|---|---|
| 1. Debug 제거 후 정식 초기화 경로 | **격리 검증 완료**, 실제 콘텐츠 연결은 미완료 |
| 2. 필수 Config·Skill snapshot | 승인 숫자 확보, **Content/Contract Pending** |
| 3. Scene/Prefab 수정 대상 | 위 정확한 경로·컴포넌트 확정 |
| 4. 상속·Instance override | source/added 구성 읽기 확인. **실제 Editor 적용 결과 미검증** |
| 5. GUID/SerializeField 호환 | 이번 무변경, 기존 type/field/GUID 유지 가능. 실제 변경 후 재확인 필요 |
| 6. 복구 절차 | 아래 절차 준비, 실제 자산 변경·복구는 수행하지 않음 |
| 7. 사용자 보호 자산 변경 승인 | **없음. 이번 예행 지시는 승인에 해당하지 않음** |

따라서 **현재 실제 보호 자산 수정 가능 상태로 판정하지 않는다.** 데이터 준비 및 별도 승인이 남는다.

### 향후 Inspector 연결·변경·복구 순서

1. 별도 승인 후 Play Mode를 종료하고, 현재 dirty/untracked를 포함한 Scene/Prefab/SO/meta의 변경 직전 사본·SHA-256·Inspector/UnityEvent/override 목록을 확보한다. HEAD reset으로 복구하지 않는다.
2. 기존 Config type을 사용하여 승인된 숫자와 승인된 실제 콘텐츠만 준비한다. 기존 Production SO를 작성할지 별도 Production Playtest SO를 만들지는 승인 단계에서 결정한다. 별도 SO라면 Production 데이터 경로·새 Unity GUID·EntryPoint 참조 변경을 먼저 명시하고 검증한다. Tests/Editor SO 직접 연결 금지.
3. enable=false 상태에서 EntryPoint Config/registry, 공식 Character ID/장착 snapshot, Enemy loadout/source key, Hand rule·copies·seed, selector 및 관련 서비스 참조를 확인한다. 실제 authored 데이터로 격리 초기화/실패 정리를 재실행한다.
4. 승인된 base Prefab BP 분리 후 Dungeon/Template 전파를 확인한다. 이어 Dungeon의 Scene-added Hand/Clash/BP만 분리한다. Camera Controller와 기존 UI/World/Reward/registry override는 보존한다. Apply All/Revert All 금지.
5. 삭제 대상 fileID의 serialized reference와 UnityEvent persistent call을 변경 직전/후 재검색한다. Missing Script/Reference, prefab 원본/instance override, Config type 및 GUID를 대조한다. 이번 조사만으로 모든 UnityEvent 호환을 검증했다고 보지 않는다.
6. 별도 활성화 승인·데이터 검증 후에만 enableProductionHandBp를 켠다. 아래6개 gate 및 격리 회귀를 재실행한다. gate 이후 다른 결함이 드러나면 따로 기록한다.
7. 문제가 생기면 변경 직전 사본에서 승인된 component/field 범위만 복구하고 중간 사용자 변경을 대조한다. base와 instance를 함께 점검하고 enable=false·GUID·meta 및 diff를 확인한다. 전체 원본 덮어쓰기/자동 reset은 하지 않는다.

### 실제 Production 수동 검증 순서·통과 기준

현재 실행하지 않는다. CBT-R2-05/06A 보류 해제도 별도 지시가 필요하다.

1. Inspector에서 Config/registry/party/enemy/서비스 연결과 Missing 참조·단일 EntryPoint·Debug 분리를 확인한다.
2. Contact/FieldAttack 각각으로 시작한다. 초기 명령 전에 Hand/BP/Ultimate/Clash 준비 완료, 손패2/3·BP0, 적 Hand 선택 정책을 확인한다.
3. MP2와 별도 유효 MP3 상황·카드 부족·무대응·Timeout5초를 검증한다. 3회 강제 실행이나 MP 자동 지급 없이 정상 조기/상한 종료되어야 한다.
4. 최종 피해/상태/Mental 1회, Chain/인계·다인 Hand 보존, BP 유효 주기·Ultimate 단계를 확인한다.
5. Pause/슬로모션/Abort/Scene 전환에서 명령·타이머·timeScale/fixedDeltaTime·입력/UI/필드 잠금이 기존 Owner 계약대로 복구되어야 한다.
6. Victory/Defeat 및 지연·중복 이벤트에서 Completion/Reward/참가자 EXP 1회, Reward→Exploration 복귀와 다음 전투 새 snapshot을 확인한다. 실제 Save/Continue·Player Build·Editor 재시작은 별도 검증이다.

## 7. 테스트 및 파일 보존 최종 기록

Unity 6000.3.23f1 선택 회귀 **801 PASS / 6 FAIL / 1 IGNORED**, 총808. 기록: `Logs/CBT-R2-08-P2B3D2-Regression.xml/.log`, 정확한 실행 범위 `...-TestFilter.txt`. 종료 코드2는 기존6개 자산 gate 실패를 반영한다. D1 XML과 실패 이름을 대조해 동일6개이며, 이번 선택 범위에서 신규 실패가 없음을 확인했다. 전체 회귀 과거 기준선 `1,562 PASS / 8 FAIL / 1 IGNORED`, D1 선택 `788/6/1`은 이번 실행 결과와 구분한다.

이번 선택 범위는 D1 filter에서 실제 Production Scene을 열고 Play Mode로 들어가는 `FilmUniqueProductionPlayModeTests.ProductionScene_ContactAcquire_UIEquip_FieldInputSkill_ColdSaveLoad` 1건을 제외하고 새14건을 추가했다. 기존 테스트를 삭제·Skip·기대값 완화한 것이 아니라 사용자 지시의 실제 Scene 실행 금지를 지키기 위한 실행 범위 제한이다. Film/Item 호환은 안전한 기존 격리 회귀로 확인한다. 보호 자산 설치/저장 테스트가 있는 전체 회귀는 실행하지 않는다.

추가 파일: `Assets/GAME/Tests/Editor/CombatProductionCutoverRehearsalTests.cs`, Unity 생성 `.cs.meta`(GUID `6afaedf0f154701428382afe716781ff`), 이 보고서. 기존 파일 수정/삭제/이동 없음. 공개 API·SerializeField·Save schema·Unity/Package 버전·기존 GUID 무변경. 저장된 테스트 SO도 무변경이다. Logs는 무시되는 실행 증거다.

### 세부 결과와 기존 실패6건

- 신규 메모리 Prefab 예행 **14/14 PASS**. 독립 집중 실행과 최종 선택 회귀 모두 통과.
- B3A **22/22**, B3B **15/15**, B3C **23/23**, D1 **8/8**, B2D startup **40/40 PASS**.
- Legacy 관련 **220/220 PASS**: EntryConsolidation36 / FinalExchangeDriver15 / FinalRulesPolicy52 / Foundation2 / ProductionOwnership13 / SessionTurnFlow44 / UIRouting58.
- Film 안전한 격리 회귀: SharedFilmOwnership6 / UniqueSkillUnlock11 / FilmSaveEquipPolicy17 / FilmUniqueProductionIntegration9 **모두 PASS**. Item UI/Film Hand P1 및 기존 Mental/Status/Item/AllOut/Ultimate·명령·Hand/BP 회귀도 선택 범위에 포함된다.
- B3B `TerminalFromFinalClash_PublishesCompletionAndParticipantRewardsOnce`가 통과했다. 실제 기존 EntryPoint completion과 RewardService의 중복 차단·참가자별 EXP를 격리 fixture에서 확인한 결과다. **새 복제 Prefab 검사의 Abort/Chain 결과와 기존 fixture의 Terminal/Reward 결과를 구분**하며 실제 Dungeon Reward UI 연결을 통과했다고 주장하지 않는다.
- 저장된 승인 프로필의 initialMp2는 유지했다. 3회 합 검사는 기존 B3B fixture의 initialMp3을 일시적으로 사용한다. 이는 3회 비교를 재현하는 테스트 예산이며 Production MP 지급/진입 요건 변경이 아니다.

공통 namespace `Game.Tests.Integration`. 아래 모두 **Existing Confirmed / Asset Change Required**:

| 정확한 테스트 이름 | 이번 재현 결과·원인 |
|---|---|
| `DungeonOneProductionSceneTests.ProductionDungeonOne_HasCanonicalCombatRewardUiCameraAndBootstrapOwners` | FAIL — 아래 공통 Hand Debug 선행 gate |
| `DungeonOneProductionSceneTests.ProductionDungeonOne_HasExplicitInteractionOwnersWithoutLegacyInteractionPaths` | FAIL — 동일 |
| `DungeonOneProductionSceneTests.ProductionDungeonOne_HasOneCanonicalRepeatableNpcInteraction` | FAIL — 동일 |
| `DungeonOneProductionSceneTests.ProductionDungeonOne_HasQuestDerivedCompletionContractWithoutLegacyExitOrFallbackDestination` | FAIL — 동일 |
| `DungeonOneProductionSceneTests.ProductionDungeonOne_IsCleanEnvironmentMigration` | FAIL — 동일 |
| `ProductionSceneWiringTests.DungeonTemplate_IsCanonicalSingleOwnerProductionSource` | FAIL — inherited BP Debug 존재 predicate, Expected False / But was True |

앞5건 로그는 `Forbidden Demo/Debug/Test/Legacy component remained: Game.Combat.Debugging.CombatHandValidationOptIn on CombatRuntime` (`DungeonOneProductionMigrationUtility.cs:456`). 상세 메시지·stack은 `Logs/CBT-R2-08-P2B3D2-SixEvidence.txt`에 보존했다. 이는 공통 선행 gate이며 각 테스트의 NPC/Quest/UI 후속 조건을 개별 결함으로 단정하거나 검증 완료로 취급하지 않는다. 실제 serialized Debug 경로를 승인 후 분리하고 전체 assertion을 다시 통과해야 해결이다. 비활성만으로 해결되지 않는다.

IGNORED는 기존 `Game.Tests.Integration.PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners` 1건이다. 별도 쓰기/읽기 Editor 프로세스 재시작 절차를 실행하지 않았다. 새 Ignore/Skip 추가 없음.

Unity가 신규 Editor 테스트를 컴파일하고 실행했으며 `error CS`/`warning CS` 없음. 기존 OfficeFlowController meta GUID import 오류 및 빈 `Assets/GAME/Scenes/Tests/Tests.asmdef` 안내는 별도 기존 경고/자산 문제로 기록하고 원본을 보존했다. 모든 Runtime의 강제 전체 재컴파일를 수행했다고 주장하지 않는다. 기대한 startup 거부 로그와 위6개 테스트 assertion 실패를 C# 컴파일 오류로 혼동하지 않는다.

### 보존 및 미실행 범위

시작 기록 **1,818개 모두 SHA-256 동일**, 누락/기존 파일 변경0. 보호 Production Scene/Prefab/SO 및 meta/GUID, 저장된 테스트 Config, 기존 dirty/untracked 파일·보고서·Unity/Package/ProjectSettings를 보존했다. Git 상태 차이는 위 새 테스트/meta/보고서3개뿐이며 삭제/이동 없음. 신규 script GUID 중복 없음. commit/push/PR 없음.

새 C#/보고서 trailing whitespace 검사 통과. `git diff --check`는 기존 Font3곳·Dungeon Scene1곳의 trailing whitespace4건 및 기존 줄바꿈 안내를 보고한다. 해시 보존을 위해 해당 사용자 변경을 수정하지 않았다.

실제 Production Scene Play Mode/Inspector, 보호 자산 저장·설치·전환, Player Build, 디스크 Editor 재시작 검증 및 전체 회귀는 미실행이다. 신규 footprint 검사는 전환 전 현재 구성을 의도적으로 확인하므로, 향후 실제 자산 변경 승인 단계에서는 그 단계의 상태 계약에 맞춰 별도 검토해야 한다. 현 단계에서 기존 실패를 숨기도록 완화하지 않았다.

## Development tracking

- System / requirement: Combat / CBT-R2-08-P2B3D-2 자산 전환 예행 검증.
- Production owner: 기존 CombatEntryPoint, CombatSession, CombatStateMachine, CombatFlowOrchestrator, SkillRunner/Outcome Executor, Director 및 Reward/GameFlow/UI/Input 유지.
- Implementation: **Implementation Complete (격리 예행 검사 및 전환 계획)**. 실제 자산 전환은 미실행.
- Compile / automated validation: 최종 실행 기록 참조.
- Inspector: 실제 자산 연결 **Pending**. 읽기 전용 구조 검사는 Inspector 연결 검증과 구분.
- Play Mode: 메모리 복제본의 실제 Director 검사 통과. 실제 Dungeon/Template·카메라/UI/Input 통합 **Unverified**.
- Integration: 격리 기능 검증 범위만 통과, 실제 Production 통합/Content/자산 전환/승인 **Pending**.
- Recommended: **Rehearsal Verified — Production Content / Asset Change Approval / Inspector & Manual Integration Pending**.
- 05/06A 수동 보류, LegacyPlanning/Debug 호환 코드, 정식 콘텐츠 Pending 유지. Production 초기화 비활성 및 Cutover 미실행. 시스템 전체 완료로 기록하지 않는다.
