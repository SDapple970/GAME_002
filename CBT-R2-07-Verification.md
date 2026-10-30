# CBT-R2-07 — 정식 전투 데이터·Production 연결

작성일: 2026-10-29 KST. 현재 작업문, 저장소의 실제 코드/자산과 사용자의 “자료 없음” 답변을 기준으로 한다. 정식 콘텐츠를 추정 제작하지 않았다.

## 결과 및 작업 경계

승인 자료가 없어 실제 콘텐츠 연결은 Content Pending이다. 대신 기존 Validator를 재사용하는 실행 가능한 읽기 전용 통합 감사, 검출 테스트, 데이터 작성 및 정확한 Inspector 연결 가이드를 추가했다. 기존 Production 스킬/Config/UI 연결은 확인했으며 이번에 연결하거나 변경한 Scene/Prefab/SO는 없다. 05/06A의 실제 다인 Inspector·수동 Play Mode 보류는 유지한다.

- 적용 지침: 루트 `AGENTS.md`. 적용되는 중첩 AGENTS/override 없음.
- Branch `main`, HEAD `6fa26d294472574097d33b884858ba0ee8bdc8b7`. commit/push/PR/새 branch 없음.
- ProjectVersion 및 실행 Editor: Unity 6000.3.23f1 (09d2ecc7fb28). manifest의 Input System 1.20.0, URP 17.3.0, Test Framework 1.6.0. 버전·패키지 변경 없음.
- 시작 시 기존 tracked/untracked 변경의 SHA-256을 별도 저장했다. 05/06A/06B 전투 코드·테스트·보고서와 Quest/Reward/Save 및 Unity Asset의 기존 상태를 유지한다.

## 추가 파일과 호환성

- `Assets/GAME/Editor/CombatProductionDataAudit.cs`: 메뉴 `GAME/Verification/Audit Combat Production Data` 또는 batch `-executeMethod Game.EditorTools.CombatProductionDataAudit.Run`. 결과는 `Logs/CBT-R2-07-ContentAudit.md`.
- `Assets/GAME/Tests/Editor/CombatProductionDataAuditTests.cs`: 8개 검사. 잘못된 ID, 중복 ID, 단일 초기 파티·리더·편성, 누락 시작 정의, Test/Debug 경로, 실제 감사의 보호 Asset 비변경 검증.
- 위 두 스크립트의 `.meta`: Unity가 신규 발급. 기존 GUID 재사용/변경 없음.
- 이 보고서.

기존 파일 수정·삭제·이동 없음. Runtime public API, SerializeField, Save schema, Character ID/persistentKey, Input 소유권 변경 없음. 추가 public API는 Editor 감사용 정적 메서드뿐이다. 새로운 Runtime Manager나 전투 실행 경로가 없다.

감사는 별도 Preview Scene에서 조회 후 닫으며 Scene/Prefab을 저장하지 않는다. 기존 identity/acquisition/Unique/catalog Validator를 호출하고 자산 목록·스킬 비용/분류·등록·로드아웃 ID·Missing Script/직렬화 참조·Test/Debug 의존성과 기존 Owner 수를 조사한다. `UNASSIGNED`는 필수 누락과 optional/runtime 바인딩을 구분하여 수동 검토해야 하며 모두 오류라고 단정하지 않는다. `REVIEW`는 승인 여부나 기능 결함 확정이 아니다. 빈 매핑이나 검사 실행 성공도 콘텐츠 승인으로 취급하지 않는다.

## 데이터와 Production 연결 현황

| 기획 항목 | Production 시스템/코드 | 자산 및 Scene/Prefab | 검증·남은 일 |
|---|---|---|---|
| 아난 드 모르테 초기 1명·리더·편성 1명 | RuntimeBootstrapper → CharacterStartDefinitionSO → PartyRuntime/CharacterProgressionService | CharacterStartDefinitionSO 0개. Title `Systems.characterStartDefinition`, Dungeon `Runtime.characterStartDefinition` 미지정 | 공식 ID/성장 정의 없으므로 연결 불가. 단일 인원 검증 fixture만 작성. |
| 보유 최대 6 / 편성 1~3 | PartyRuntime | 편성 MaxCombatLineupSize=3. AddMember는 보유 상한 검사 없음 | 보유 6명 제한은 별도 Party 정책/Save 호환성 작업 필요. 이번에 정책 변경 안 함. |
| 기존 캐릭터/Save | CharacterProgressionService, CharacterSkillRuntime | `Assets/GAME/Data/Test/CP_TestHero.asset`, ID `hero.test`; Title의 CharacterProgressionService가 참조 | 테스트 자산이 실제 Title에 연결됨. 공식 주인공으로 간주하지 않으며 ID 강제 치환 안 함. 기존 schema/저장 호환 경로 유지. |
| 기존 공격 스킬 | CombatEntryPoint, SkillBook, SkillRunner | Dungeon `Runtime/Combat/CombatRuntime.skillDefinitions` 4개 | ID 1/2/11/12 등록. 현 연결 유지. 최종 정식 스킬/손패 승인과는 별개. |
| 장착 → 다음 전투 | CharacterSkillRuntime → PersistentSkillCombatLoadoutBridge.ApplyToRequest → CombatSession snapshot | 정식 Film/Unique 데이터 0개 | 기존 snapshot 회귀 실행. 실제 정식 콘텐츠 검증 Pending. |
| 일반/트리키 적 | CombatEncounterGroup, EnemySourceComponent, EnemyDefinitionSO, FinalCombatEnemyBehaviorProfileSO | EnemyDefinition 0개 / AI Profile 0개. Dungeon Encounter_01~03의 Enemy_01~03은 source 없음, loadout ID 11/12 | 적 3개가 정식 일반형/트리키형이라는 증거 없음. HP/maxHP 10/10은 기존 CombatHpComponent 값. MP/체간은 공통 Runtime Config 경로, EnemyDefinition은 별도 능력치 필드를 소유하지 않음. |
| Enemy → Film | EnemySourceSnapshot → EnemySkillAcquisitionIntegration/Processor → CharacterSkillRuntime | 적별 acquisition mapping 없음 | Skill loadout은 획득 매핑이 아님. 승인할 Enemy key/Film key/분류·드롭 규칙 필요. |
| 인벤토리/전투 아이템 | InventoryService → CombatItemUseAdapter → Orchestrator → CombatItemUseExecutor | ItemCatalogSO/CombatItemCatalogSO/CombatItemDefinitionSO 0개. Scene/Prefab Adapter 없음 | 승인 데이터 전까지 연결 불가. 06B 승인·소비·복구·UI 갱신 경로 재사용. |
| 상태 | CombatStatusDefinitionSO → SkillRunner/CombatantCombatState | Status SO 4개 모두 `Tests/Editor/Fixtures/CombatStatusValidation` | Production 승격 금지. 상태 지원 분류는 아래 참조. |
| 궁극기/BP | 기존 Ultimate Runtime 및 SkillRunner | CombatRuntime Debug `CombatBpValidationOptIn.ultimateSkill` → Skill_skill2 | 검증 fixture 연결이지 정식 궁극기 승인 아님. BP 누적/소비 규칙 유지. |
| 총공격 | CombatAllOutPolicy / CombatAllOutExecutionPolicy → SkillRunner | 첫 유효 아군의 authored AllEnemies 공격을 선택하는 기존 기술 계약. 독립 배율 SO 없음 | 추가 배율을 만들지 않음. Skill_skill2의 현재 데이터로 정식 총공격 수치를 확정할 수 없음. |
| UI·상태 흐름 | GameFlowController → GameState → UIScreenRouter → GameUIRootController → CombatHUD | Dungeon `Runtime/UI/ProductionDungeonUI/CombatCanvas/CombatHUD/FinalExchangePanel` | 기존 root/button/MP/체간/타이머 참조 감사 기록. 실제 가독성·입력·다인 화면 검증 보류. |
| 종료·Reward·EXP | CombatEntryPoint → CombatResult CompletionId → 기존 Reward/Progression 소유자 | 기존 Completion/participant snapshot 유지 | 지급 정책 변경 없음. 기존 중복 지급·참가자 EXP 회귀 실행. |

### Skill SO 전체 목록

| 경로 (Assets/GAME 기준) | persistentKey / ID | 분류·현재 값 |
|---|---|---|
| Data/Skill/Skill_BasicAttack.asset | skill.production.0001 / 1 | Compatibility, Attack/SingleEnemy, Final MP 1 |
| Data/Skill/Skill_skill2.asset | skill.production.0002 / 2 | Compatibility, Attack/AllEnemies, Final MP 2 |
| Data/Skill/Angel_Skill.asset | skill.production.0011 / 11 | Compatibility, Attack/SingleEnemy, Final MP 1 |
| Data/Skill/Angel_Skill2.asset | skill.production.0012 / 12 | Compatibility, Attack/SingleEnemy, Final MP 2 |
| Combat/Data/Skills/SO_Skill_BasicAttack.asset | skill.demo.0001 / 1 | Demo/Compatibility, Production 등록 안 됨 |
| Combat/Data/Skills/SO_Skill_Fire.asset | skill.demo.0002 / 2 | Demo/Compatibility, Production 등록 안 됨 |
| Combat/Data/Skills/SO_Skill_Inspect.asset | skill.demo.0010 / 10 | Demo/Inspect, Production 등록 안 됨 |
| Tests/Editor/Fixtures/CombatStatusValidation/TS_Skill_ApplyStatuses.asset | test.skill.apply_statuses / 920001 | Test 전용, Production 등록 안 됨 |
| Tests/Editor/Fixtures/CombatStatusValidation/TS_Skill_FinalAttack.asset | test.skill.final_attack / 920002 | Test 전용, Production 등록 안 됨 |

전역 persistentKey는 유일하며 demo와 Production의 숫자 ID 겹침은 개별 등록 집합에서 검사한다. 위 스킬 모두 현재 legacy MP 0, BP enhancement None:0이다. 합 공격/대응·일반/지원·Film/Unique/Ultimate의 정식 콘텐츠 분류를 이름만으로 추정하지 않는다. 현재 손패/반복 합/BP는 Debug opt-in fixture 연결이 있으며 정식 카드 데이터 승인과 Production 런타임 설정이 필요하다.

### Status 및 Item 분류

- 기존 지원 및 검증 가능: Buff/Debuff의 Refresh/Replace/Stack과 피해·합·체간 보정, 완료 Exchange 기준 지속시간, 별도 Mental/Panic/확정 Overcome, 별도 Stun 복귀. Item은 Self/SingleAlly의 즉시 HP 회복 또는 authored Status 1종 적용.
- 기존 지원이나 Production 미배치: 위 authored Status/HP 아이템. Status SO는 테스트 4개뿐이고 Item SO는 없음.
- 규칙 확정 및 구현 필요: 무적·면역·DoT·HoT·출혈, 부활·정화·MP/BP 아이템 등. 수치·지속/갱신 단위·중첩·대상/사망/면역 우선순위가 필요하다.
- 기절은 일반 Status SO와 분리된 기존 Stagger/Posture 상태다. 모든 상태가 구현됐다고 기록하지 않는다.

## Inspector 연결·데이터 작성 가이드 (미수행)

아래는 승인 데이터 확보 후의 절차다. 05/06A의 다인 연결·수동 검증을 이번에 완료 처리하지 않는다.

1. Character Progression Definition에 승인된 공식 ID·성장값을 작성한다. Character Start Definition의 `definitions`, `initialMemberIds[0]`, `initialLeaderId`, `initialCombatMemberIds[0]`, `defaultRewardTargetId`를 같은 공식 캐릭터로 지정한다. Title `Systems`와 Dungeon `Runtime`의 **RuntimeBootstrapper.characterStartDefinition**에 동일 SO를 연결한다. 기존 `hero.test`는 Save migration 정책 없이 제거/치환하지 않는다. 새 시작 Definition에 Legacy 저장 정체성을 어떻게 유지할지 별도 검증한다.
2. Dungeon `Runtime/Combat/CombatRuntime`의 **CombatEntryPoint**: `finalExchangeRuntimeConfig`는 `Data/Combat/Runtime/FinalCombatRuntimeConfig_V1.asset`, `skillDefinitions`는 위 기존 4개다. 기본 `Prefabs/CombatRuntime.prefab`의 빈 registry를 독립 사용하려면 승인된 registry를 별도 연결해야 한다. Scene override를 무단 apply하지 않는다.
3. `World/Encounters/Encounter_01/Enemy_01`, `Encounter_02/Enemy_02`, `Encounter_03/Enemy_03`: 승인 Enemy Definition 확보 후 **EnemySourceComponent.definition**을 지정한다. Definition의 `persistentKey`, `finalCombatBehaviorProfile`, `acquirableSkillPersistentKeys`를 승인 자료로 채운다. **CombatSkillLoadoutComponent.skillIds**는 registry에 존재해야 한다. 일반/트리키 AI의 priorities·feint 설정과 실제 필드 HP를 별도로 승인한다. 임의 새 적 배치 금지.
4. 승인된 Inventory Item Definition + Item Catalog를 먼저 작성한다. 동일 `itemId`의 Combat Item Definition을 만들고 `targetRule`, `effectKind`, `hpRecovery` 또는 `appliedStatus`를 지정하여 Combat Item Catalog에 넣는다. Dungeon `Runtime/Combat/CombatRuntime`에 **CombatItemUseAdapter** 추가 후 `flowOrchestrator`는 같은 오브젝트의 Orchestrator, `inventoryService`는 RuntimeBootstrapper가 확보하는 단일 InventoryService, `combatItemCatalog`는 승인 Catalog에 연결한다. 현재 InventoryService는 bootstrap 동적 Owner일 수 있으므로 임의 중복 추가하지 말고 씬의 실제 Owner 확보 방식을 먼저 확인한다. Adapter는 기존 Instance fallback도 지원한다.
5. `Runtime/UI/ProductionDungeonUI`의 **UIScreenRouter.uiRoot**는 같은 GameUIRootController다. `stateMachine` 미지정은 기존 runtime resolve 계약을 확인한다. **GameUIRootController.combatRoot**는 `CombatCanvas/CombatHUD`; 그 아래 **FinalCombatUIBinder**의 root/button 템플릿은 감사 보고서 참조. Item 명령은 현 구현상 `HandoffListRoot`에 표시되며 기존 실패 테스트는 `SkillListRoot`를 기대한다. 최종 UI 위치 계약 확정 전 기대값을 변경하지 않는다.
6. FinalExchangePanel의 **FinalCombatUIBinder.statusListPresenter**는 미지정이다. 승인 상태 UI 작성 시 기존 FinalCombatStatusListPresenter를 사용하고 목록/템플릿 계약을 확인한다. 현 필드 목록에는 독립 BP Text 필드가 없으므로 BP/타이머의 기존 context 표시와 실제 연출을 확인한다.
7. `CombatHandValidationOptIn`, `CombatClashSeriesValidationOptIn`, `CombatBpValidationOptIn`이 Scene에 활성화돼 있다. 이들을 정식 콘텐츠 Owner로 쓰지 않는다. 정식 hands/BP/Ultimate 런타임 연결 방식과 승인 자료를 마련한 뒤 별도 migration으로 제거/대체한다. 보류 작업과 겹치므로 이번에 비활성화하지 않았다.

모든 authored skill persistentKey는 유일해야 하고 registry 내부 skillId도 유일해야 한다. Unique는 공식 owner ID, Film은 명시적 acquisition mapping 및 category가 필요하다. 장착 후 다음 전투 snapshot을 확인한다. ScriptableObject에는 세션 수량/HP/진행 상태를 저장하지 않는다.

## 자동 검증

감사 집중 검사: 8/8 PASS (`Logs/CBT-R2-07-AuditTests.xml`), Unity 종료 0. 실제 대상 Scene/Prefab 4개의 byte 동일성을 검사했다. `AuditTests` PASS는 콘텐츠 유효/완료 판정이 아니라 누락 검출 및 비변경 동작 확인이다.

최종 전체 실행: **1,562 PASS / 8 FAIL / 1 IGNORED**, 총 1,571 (`Logs/CBT-R2-07-AllEditMode.xml`, `.log`). Unity 종료 2는 기존 테스트 실패를 반영한다. 06B 최종 XML(1,554/8/1)과 실패 이름 차이 없음. 새 검사 8개 전부 통과. 실제 Editor 6000.3.23f1 C# 컴파일 오류 없음. 이번 로그에는 C# 컴파일 경고가 없고, Scene 누락/Debug/Reward 검증에 따른 런타임 경고는 별도 로그에 유지한다.

기존 실패 목록(삭제하거나 기대값을 바꾸지 않음):

1. `Game.Tests.Combat.CombatItemUsePlayModeIntegrationTests.IsolatedFinalExchange_ItemAdapterInventoryAndBinderRunInActualPlayMode` — SkillListRoot에서 Item을 기대하나 현재 HandoffListRoot에 표시.
2. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasCanonicalCombatRewardUiCameraAndBootstrapOwners` — 보호 Scene Debug opt-in.
3. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasExplicitInteractionOwnersWithoutLegacyInteractionPaths` — 동일.
4. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasOneCanonicalRepeatableNpcInteraction` — 동일.
5. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasQuestDerivedCompletionContractWithoutLegacyExitOrFallbackDestination` — 동일.
6. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_IsCleanEnvironmentMigration` — 동일.
7. `Game.Tests.Integration.FilmUniqueProductionPlayModeTests.ProductionScene_ContactAcquire_UIEquip_FieldInputSkill_ColdSaveLoad` — 보호 Scene의 Hand validation fixture와 임시 스킬 불일치.
8. `Game.Tests.Integration.ProductionSceneWiringTests.DungeonTemplate_IsCanonicalSingleOwnerProductionSource` — template의 Debug/Legacy 컴포넌트.

Ignored: `Game.Tests.Integration.PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners`. 선행 write 후 별도 Editor 프로세스 read가 필요하다.

기존 CharacterProductionStartup/Scene, CharacterSkillRuntime, Film/Enemy acquisition 및 중복 Reward, Party roster/snapshot, Mental/Status/Item UI, 반복 합·BP·손패·인계·적 AI·Stun 복귀·AllOut/Ultimate 회귀를 전체 실행에 포함했다. Editor의 EnterPlayMode fixture 통과와 정식 콘텐츠를 사용한 수동 Production 검증은 구분한다. Player Build, 정식 자료가 없는 데이터 동작, 실제 다인 Inspector 조작은 미실행이다.

## Play Mode 수동 검증 가이드

현재 미실행. 승인 데이터 및 보류 해제 이후 별도 테스트 저장 경로를 사용한다.

1. Title New Game → 보유/편성/리더가 공식 캐릭터 1명인지 확인. 직접 Dungeon 시작도 같은 Definition을 쓰는지 확인. 기존 Save Continue 및 반복 Load로 hero.test 정체성·EXP·장착이 손실되지 않는지 확인.
2. 일반/트리키 적을 각각 접촉/필드 공격으로 시작한다. CombatEntryPoint가 하나이고 snapshot/HP/MP/손패가 승인 데이터를 따르는지 확인한다. 입력은 기존 Input 라우팅을 사용한다.
3. Enemy 승리 결과 → Film 1회 획득 → 장착 → 다음 전투 snapshot 갱신. 같은 CompletionId 재전달로 Film/Reward/참가자 EXP가 추가 지급되지 않아야 한다.
4. 아이템 HP/상태 적용·재고 1회 소비·실패 시 복구, 외부 재고 변경 및 reload 후 UI 갱신, Scene 전환 후 구독 정리를 확인한다.
5. 반복 합 3라운드의 Mental/상태 지속시간, Panic/Overcome=35, BP/손패/인계/적 AI/기절 복귀와 Ultimate/AllOut 수락·거절·Timeout·중복 방지를 확인한다. 생존 적은 정상 흐름으로 복귀하고 종료 후 지연 효과는 없어야 한다.
6. GameState 전환에 따른 Combat/Reward UI, Pause/Abort/Scene 전환 및 timeScale 복원, 실제 다인 Inspector/연출은 05/06A 별도 항목으로 검증한다.

## Development tracking

- System: Character / Progression / Combat / Enemy / Inventory / UI.
- Feature/Requirement: CBT-R2-07 정식 전투 데이터와 Production 연결 감사.
- Production owner: RuntimeBootstrapper, PartyRuntime, CharacterProgressionService, CombatEntryPoint, Session/StateMachine/Orchestrator, SkillRunner/Item Executor, Inventory/Reward, UIScreenRouter 유지.
- Implementation: **In Progress — Validator/작성 가이드 구현, 승인 콘텐츠 Pending**.
- Compile/자동 검사: 아래 최종 기록 참조.
- Inspector: Serialized 참조 읽기 전용 확인. 실제 Inspector 조작·수동 확인 미실행.
- Play Mode: 기존 자동 격리 fixture와 실제 정식 데이터 Production 수동 검증을 구분. 후자는 미실행.
- Integration: 기존 코드 회귀 실행. 공식 캐릭터/적/Film/Item/Status/Ultimate/AllOut 데이터 통합 Pending.
- Recommended tracking: **In Progress — Content Pending / Production Verification Pending**.
- Stale mapping: skill.production 이름과 현재 Compatibility 분류, 기본 prefab 빈 registry와 Scene override, Debug opt-in을 정식 전투 데이터로 간주하면 안 됨. hero.test는 공식 아난 ID로 재해석하지 않음.

## 최종 점검

기존 미커밋 파일 48개는 전부 시작 시 SHA-256과 동일하다. 새 Editor 코드·테스트와 Unity 발급 meta 2개 및 보고서만 추가했다. 신규 파일 공백 검사 통과. 기존 전체 `git diff --check`의 보호 폰트/Scene 공백 문제는 이번에 수정하지 않았다. GUID·기존 Asset·Quest/Reward/Save·Legacy/Debug/보류 연결을 모두 보존했다.

구현 상태: 감사 및 작성 가이드 구현 완료, CBT-R2-07 전체는 In Progress. 검증 상태: Compile Verified 및 감사/회귀 검증 완료(기존 실패 유지); 공식 콘텐츠 Inspector/Play Mode/Integration 미검증. 전체 시스템 완료 아님.
