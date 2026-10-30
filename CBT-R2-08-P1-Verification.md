# CBT-R2-08-P1 — Production Debug 의존 및 기존 실패 정리

2026-10-29 KST. 이번 작업은 최종 Cutover가 아니다. 공식 콘텐츠 Content Pending, CBT-R2-05/06A의 실제 다인 Inspector/Production 수동 검증 보류를 유지한다.

## 지침·환경·보존

- 적용: 루트 `AGENTS.md`. 중첩 지침 없음.
- Branch `main`, HEAD `6fa26d294472574097d33b884858ba0ee8bdc8b7`. commit/push/PR/branch 생성 없음.
- Unity ProjectVersion 및 실제 실행 Editor 6000.3.23f1. Packages/Input/URP/ProjectSettings 변경 없음.
- 선행 자료: `CBT-R2-08-PreCutoverAudit.md`, `Logs/CBT-R2-08-FailureEvidence.md`, 06B/07 보고서와 실제 Runtime/Debug/Test 코드, Scene/prefab/meta GUID.
- 기존 전체 기준선: **1,562 PASS / 8 FAIL / 1 IGNORED** (`CBT-R2-07-AllEditMode.xml`, 이전 실행). 사전 감사 선택 기준선: **407/8/1** (`CBT-R2-08-SafeRegression.xml`, 이전 실행).
- 시작 시 1,247개 파일 해시와 두 테스트의 원본을 Logs에 기록했다. Scene/Prefab/SO/meta와 기존 미커밋 변경을 보존한다.

## 수정 근거와 결과

### Item UI: 코드의 기존 명령 root에 fixture 정렬

실제 경로는 GameFlowController/GameState → UIScreenRouter → GameUIRootController.combatRoot → CombatHUD/FinalExchangePanel이다. Binder는 패널 내부 목록을 렌더링하며 전역 Field/Reward root를 소유하지 않는다.

`FinalCombatUIBinder`는 `skillListRoot`에 HandCards/SkillAvailability를, `handoffListRoot`에 Item/BP/Overcome 등 별도 명령을 배치한다. `SetVisible(handoffListRoot, attack && SelectableItems...)`와 `BuildCombatItemOptions(handoffListRoot, ...)`가 같은 계약을 사용한다. Controller에서도 Item은 스킬과 별도 선택/요청이며 비용 승인과 Inventory 소유권은 유지된다.

기존 실패 테스트는 Item을 skillListRoot에서 검색하여 실제 효과 검증 전에 멈췄다. 이번에는 공유 명령 root의 **활성·상호작용 가능 버튼**을 찾고 skillListRoot에는 해당 Item이 없음을 검증한다. 회복/수량/중복 stale/잘못된 대상/효과 실패 복구의 기존 검증을 유지한다. 06B의 외부 재고 Changed/Refreshed 갱신 검사는 별도 기존 테스트로 계속 실행한다. Runtime UI 위치, Serialized root 이름·타입 및 router 소유권을 변경하지 않았다.

이 수정은 현 구현 계약을 테스트하는 것이며, 정식 UI 디자인 승인이나 Production Item 연결 완료를 뜻하지 않는다.

### Film Hand: 고정 Scene 카드 대신 기존 snapshot 모드로 격리 구성

`CharacterSkillRuntime`에서 Film 장착 → `PersistentSkillCombatLoadoutBridge`가 다음 시작 요청의 loadout snapshot 작성 → Session 생성 후 Hand opt-in 적용 순서다. 장착이 있으면 이전 compatibility 스킬 1/2가 빠지고 테스트의 **1011/1010/1020**(Film 2개 + 잠금 해제 Unique 1개)이 snapshot에 들어간다. persistentKey는 `validation.17c6.film.*`, `validation.17c6.unique.*`의 transient 복제본이다. 정식 데이터를 생성하지 않는다.

Scene의 `CombatHandValidationOptIn.playerCardSource=InspectorFixture`는 SO 1/2만 찾으므로 장착 후 일치 카드가 없어 Error가 발생했다. Test가 Play Mode 인스턴스만 구성하는 `ConfigureSceneInstances`에서 기존 `SessionSkillSnapshot` 모드를 선택하고 normalHandCapacity=3을 명시한다. 세 장은 이 fixture의 장착 목록이며 Production 기본 용량이 아니다. Scene의 합 카드 정의/적 카드/BP 설정은 그대로다.

Standoff 공격에는 **Normal** 카드, 대응에는 **Clash** 카드가 필요하다. 기존 Binder는 HasHand일 때 SelectableSkills가 아닌 HandCards로 버튼을 만든다. Film을 합 카드로 강제 분류하거나 일반 스킬 목록을 클릭하지 않고, 일반 Film 카드의 실제 버튼을 선택하도록 fixture를 정렬했다. Hand DeckSnapshot ID 집합이 전투 snapshot과 일치하는 assertion을 추가했다. 기존 입력/획득/장착/피해/승리/Reward/Save/Load assertions는 삭제하지 않았다.

검증 중 이 불일치가 단계적으로 드러났다: 최초 수정에서 손패 생성 후 SelectableSkills 검색이 실패했고, 다음 구성에서는 합 카드를 Standoff 공격에 사용하여 선택 불가가 확인됐다. 최종 fixture는 현재 Normal/Clash 계약을 그대로 따른다. 예외를 무시하거나 LogAssert로 오류를 숨기지 않았다.

## 실패 8건의 전후 상태

원래 8건 각각을 명시적으로 선택해 재실행했다. 로그: `Logs/CBT-R2-08-P1-Eight.xml`(첫 수정 단계), `Film.xml`(중간 단계), `Film2.xml`(Film 최종 단독), 최종 집중 회귀 XML. F2~F6은 같은 gate에 막히므로 그 뒤 Quest/NPC/UI 세부 검사가 통과했다고 해석하지 않는다.

| 번호 | 정확한 테스트 이름 | 수정 전 원인 | 최종 상태 |
|---|---|---|---|
| F1 | Game.Tests.Combat.CombatItemUsePlayModeIntegrationTests.IsolatedFinalExchange_ItemAdapterInventoryAndBinderRunInActualPlayMode | skillListRoot에서 Item을 기대, null | **Fixed — Environment/Test Fixture Issue**. 활성 명령 root 검사 및 기존 효과/복구 검증 |
| F2 | Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasCanonicalCombatRewardUiCameraAndBootstrapOwners | Forbidden CombatHandValidationOptIn on CombatRuntime | **Asset Change Required / Existing Confirmed** |
| F3 | Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasExplicitInteractionOwnersWithoutLegacyInteractionPaths | 동일 선행 gate | **Asset Change Required / Existing Confirmed** |
| F4 | Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasOneCanonicalRepeatableNpcInteraction | 동일 선행 gate | **Asset Change Required / Existing Confirmed** |
| F5 | Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasQuestDerivedCompletionContractWithoutLegacyExitOrFallbackDestination | 동일 선행 gate | **Asset Change Required / Existing Confirmed** |
| F6 | Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_IsCleanEnvironmentMigration | 동일 선행 gate | **Asset Change Required / Existing Confirmed** |
| F7 | Game.Tests.Integration.FilmUniqueProductionPlayModeTests.ProductionScene_ContactAcquire_UIEquip_FieldInputSkill_ColdSaveLoad | fixed Hand fixture에 equipped session skills 없음 | **Fixed — Environment/Test Fixture Issue**. 실제 transient Film 카드 피해·승리·저장/복원까지 실행 |
| F8 | Game.Tests.Integration.ProductionSceneWiringTests.DungeonTemplate_IsCanonicalSingleOwnerProductionSource | template의 prefab이 Debug BP component 포함 | **Asset Change Required / Existing Confirmed** |

F2~F6/F8을 고치기 위해 validator의 금지 조건을 약화하거나 컴포넌트를 Runtime에서 자동 파괴/무시하지 않았다. 미해결 6건은 실제 Debug-free Production gate를 만족하지 못한 상태이며 단순 환경 문제로 치부하지 않는다.

## 보호 자산 분리 계획 — 이번에 미수행

| Asset / Hierarchy / Component | 정확한 변경 대상 | 영향·호환성 위험 | 후속 검증 |
|---|---|---|---|
| `Assets/GAME/Scenes/Dungeon_1_Production.unity` / `Runtime/Combat/CombatRuntime` / CombatHandValidationOptIn | playerCardSource, fixtureCards, clashSkillDefinitions, enemyCardSource/enemyFixtureCards로 주입되는 검증용 구성을 정식 손패 작성 데이터로 이전한 후 해당 Debug component를 Editor에서 제거 | 현재 enableValidation=1이며 첫 선언 전에 손패를 만든다. 단순 삭제는 손패 검증 기능 상실/동작 변경 가능. 저장 character/skill ID와 Session snapshot을 바꾸지 말아야 함 | Hand/Clash/BP 회귀, F2~F6, 장착 snapshot, 1~3인·인계·소모·취소, 실제 Scene 수동 검증 |
| 같은 Scene / 같은 오브젝트 / CombatClashSeriesValidationOptIn | durability/comparison/timeout fixture 설정과 기존 전투 시작 이벤트 연결을 정식 초기화 계약으로 대체한 후 제거 | 반복 합 활성화와 timeout/승자 선택을 주입하므로 제거만 하면 반복 합 경로가 달라질 수 있음. 임의 기본값 도입 금지 | 반복 합 3라운드, timeout, 적 AI, BP 주기와 MP/손패 승인 회귀 |
| 같은 Scene / 같은 오브젝트 / CombatBpValidationOptIn | initialPlayerBp=3, skillFixtures, ultimateSkill 등 검증 입력을 승인된 설정으로 대체한 후 제거 | 초기 BP/강화/Ultimate 부여 변경. 현 Skill_skill2는 승인된 정식 Ultimate가 아님 | BP 누적/소비·Ultimate·AllOut 상호작용·참가자 EXP/Reward 회귀 |
| `Assets/GAME/Prefabs/CombatRuntime.prefab` / `CombatRuntime` / CombatBpValidationOptIn | 위 Debug BP component를 정식 초기화 대체 완료 후 prefab 편집 모드에서 제거. Scene의 추가 component/override도 별도 확인 | Template와 실제 Scene에 전파될 수 있음. 기존 prefab GUID `1bfc52cd235e809449d17d48571f5a38`, 다른 component fileID/serialized refs 보존. Debug 스크립트 자체는 테스트용 유지 | F8과 Template/Production audit, prefab/Scene diff, 변경 전후 entry/director/orchestrator/reward/lifecycle refs 확인 |
| `Assets/GAME/Scenes/Dungeon_Template.unity` / CombatRuntime prefab 인스턴스 | 해당 prefab 원본 BP 제거 결과와 override를 확인. template만 숨기는 우회 대신 원본과 실제 Scene을 함께 계획 | `m_SourcePrefab` GUID 1bfc… → BP script GUID `4bd31fad07c44e9795d18530d37a6855`. 새 Production Runtime prefab 복제 금지 | F8과 단일 Owner·Missing Script/Reference·Legacy 회귀 |

올바른 분리는 Debug 타입의 namespace만 Production으로 옮기는 방식이 아니다. 기존 CombatEntryPoint/Session/Orchestrator의 초기화 경계를 재사용하여 승인된 immutable config/hand authoring을 runtime state에 적용하는 계약이 먼저 필요하다. 이후 검증용 opt-in은 별도 unsaved fixture/Debug 검증 환경에 남기고 Production serialized dependency를 제거한다. 새 Manager나 두 번째 start 경로는 필요하지 않다. 현재 정식 콘텐츠/설정 승인 없이 그 정책을 이 Batch에서 임의 구현하지 않았다.

수동 변경 순서: 승인된 구성·초기화 경계 준비 → 복구 가능한 현재 자산 기준 기록 → 기존 prefab을 Unity Editor에서 편집 → Scene별 added component/override 검토 → Debug 참조 제거 → 지정 파일만 YAML diff 확인 → 5개 clean gate와 template gate → 손패/합/BP/Ultimate/Film snapshot 회귀 → 별도 승인된 Production 수동 검증. 보류된 05/06A 검증을 이 단계에서 자동 완료 처리하지 않는다.

## 변경 파일·소유권

수정:

- `Assets/GAME/Tests/Editor/CombatItemUsePlayModeIntegrationTests.cs` — 기존 미커밋 확장을 보존하면서 root 검색과 활성 버튼 assertion 정렬.
- `Assets/GAME/Tests/Editor/FilmUniqueProductionPlayModeTests.cs` — transient Hand fixture, snapshot assertion 및 실제 카드 버튼 선택.

추가: 이 보고서. 삭제·이동·새 Runtime/Editor 도구 없음. 기존 public API/SerializeField/meta/GUID 변경 없음. Production Owner, Inventory/Reward/Save/Input 소유권과 LegacyPlanning 유지. Debug 스크립트 및 해당 fixture 테스트도 유지.

## 실제 검증 및 제한

최종 집중 회귀: **629 PASS / 6 FAIL / 1 IGNORED**, 총 636 (`Logs/CBT-R2-08-P1-FinalRegression.xml`, `.log`), Unity 종료 2는 남은 자산 gate 실패를 반영한다. 원래 실패 8개를 전부 포함해 개별 결과를 확인했고 Item/Film 두 건은 Passed, 나머지 여섯 건은 같은 실패다. 선택 범위에서 신규 실패 없음. 필터는 `Logs/CBT-R2-08-P1-TestFilter.txt`, 원래 8개 전후 message/stack은 `Logs/CBT-R2-08-P1-EightEvidence.md`.

사전 감사의 407/8/1과 비교하면 같은 416개에서 2개가 수정되고, Legacy 관련 220개 검사를 추가한 결과다. 추가 구성은 Foundation 2, SessionTurnFlow 44, EntryConsolidation 36, FinalExchangeDriver 15, FinalRulesPolicy 52, ProductionOwnership 13, UIRouting 58이며 모두 통과했다.

중간 실행 기록: Eight 8 PASS/7 FAIL(15개, 고정 Hand 오류 해소 후 Film의 일반 스킬 목록 가정 실패), Film 단독 0/1(중간 카드 분류 구성 실패), Film2 단독 **1/1 PASS**. 최종 집중 실행은 모든 수정이 반영된 동일 코드로 Item/Film을 함께 통과했다. 실패를 숨기거나 기존 assertion을 삭제하지 않았다.

Unity 6000.3.23f1에서 변경된 C# 컴파일 및 실행 확인, 컴파일 오류 없음. 이번 실행 로그에서 C# compiler warning은 발견하지 못했다. 예상된 asset gate 예외/fixture 진단은 compiler error와 구분하여 원본 XML/log에 보존했다. 기존 ignored는 `Game.Tests.Integration.PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners` 한 건이며 별도 Editor 재시작 단계는 실행하지 않았다.

전체 회귀는 이번에 실행하지 않는다. 기존 suite에는 `CharacterSkillProductionWiringTests.RepeatInstallation_DoesNotResaveSceneOrSharedUI` → `ProductionCharacterSkillUISetup.BuildAndConnect`처럼 보호 Scene 설치/저장 함수를 호출하는 경로가 있어, 이번 보호 자산 자동 수정 금지 범위에서는 검토한 선택 실행만 사용한다. 이전 전체 수치를 이번 결과로 재표기하지 않는다. 테스트 삭제/Skip 변경 없음.

미실행: 전체 회귀, 실제 보호 자산 제거·Inspector 조작, 정식 콘텐츠 Production 수동 Play Mode, Player Build, 별도 Editor 재시작 디스크 검증. Item/Film의 Editor EnterPlayMode 테스트는 격리/임시 콘텐츠 검증이며 정식 Production 통합 검증과 다르다.

보존 검사: 1,247개 snapshot 중 이번 수정 대상인 기존 dirty Item 테스트 한 개만 달라졌고 나머지 1,246개는 SHA-256 동일하다. 원래 clean이던 Film 테스트는 별도로 저장한 원본과 diff를 검토했다. 따라서 코드 수정은 위 두 테스트뿐이며 기존 Scene/Prefab/SO/meta/GUID/Packages/ProjectSettings와 다른 dirty/untracked 파일은 보존됐다. 두 테스트의 `git diff --check` 및 보고서 공백 검사 통과. 관계없는 기존 폰트/Scene 공백 문제는 수정하지 않았다.

## Development tracking

- System: Combat / UI / Film / Production wiring.
- Requirement: CBT-R2-08-P1.
- Production Owner: 기존 CombatEntryPoint/Session/StateMachine/Orchestrator/SkillRunner, UI Router 및 Inventory/Reward 소유권 유지.
- Implementation: **In Progress — fixture 수정 완료, Production Debug asset migration 필요**.
- Compile/자동 검사: 최종 기록 참조.
- Inspector/Asset: 보호 자산 무변경, Debug 의존 제거 미수행.
- Play Mode: Item/Film transient fixture 검증과 실제 정식 Scene 수동 검증을 구분.
- Integration: Content Pending, 05/06A 보류 유지. Cutover 미수행.
- Recommended: **2 Fixed / 6 Asset Change Required — Content·Contract·Production Validation Pending**.
