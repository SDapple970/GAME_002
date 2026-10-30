# CBT-R2-06B — 정신력·상태이상·아이템 재검증

작성일: 2026-10-29 (KST). 이 보고서는 현재 사용자 작업문 CBT-R2-06B와 실제 코드·테스트를 기준으로 한다. CBT-R2-05/06A의 Production Inspector 및 실제 Dungeon 수동 Play Mode 보류는 유지한다.

## 지침, 환경, 작업 범위

- 적용 지침: 루트 `AGENTS.md`. 적용되는 중첩 지침 없음.
- Branch `main`, HEAD `6fa26d294472574097d33b884858ba0ee8bdc8b7`. 새 branch·commit·push·PR 없음.
- 현재 ProjectVersion, AGENTS와 이번 테스트의 실제 실행 Editor는 Unity **6000.3.23f1 (09d2ecc7fb28)**다. 설치된 6000.2.6f2는 이번 테스트에 사용하지 않았다.
- Git 이력상 `f5c9fe0b` 이전 ProjectVersion은 6000.2.6f2, 해당 커밋부터 6000.3.23f1이다. 이것이 이전 기준과 현재 설정의 차이를 설명한다. 해당 변경의 의도나 승인 여부는 Git 이력만으로 판단하지 않는다.
- `Packages/manifest.json` 확인: Input System 1.20.0, URP 17.3.0, Test Framework 1.6.0. ProjectVersion, manifest 및 package lock을 수정하지 않았으며 이번 작업에서 프로젝트를 마이그레이션하지 않았다.
- 시작 시 tracked/untracked 변경의 해시를 기록했다. 기존 Quest/Reward/Save 및 Scene/Prefab/폰트/TimeManager, 05/06A의 미커밋 변경을 보존한다. 관련 기존 수정 코드·테스트는 현재 내용을 기반으로 최소 확장했다.

## 구현 및 기존 계약

Production 시작·종료: `CombatEntryPoint`. 전투 상태·승인은 `CombatStateMachine`, 흐름·아이템 요청은 `CombatFlowOrchestrator`, 효과 실행은 `SkillRunner` 및 기존 `CombatItemUseExecutor`, 수량은 `InventoryService`, 보상은 기존 `RewardService`가 소유한다. 두 번째 전투 진입점이나 새 피해 엔진을 만들지 않았다.

- `CombatClashSeriesState`는 각 비교에서 HP·정신력·상태를 직접 바꾸지 않는다. 완료된 Exchange에 대해 기존 정신력 계산과 참여자의 지속시간 감소를 한 번 수행한다. 현재 명시적 라운드별 정신력/상태 효과 계약은 없다.
- 기존 확정형 Overcome 및 기본 복귀값 35, 공격권 상대에게 양도, Panic 제약을 유지한다. 확률형 규칙은 추가하지 않았다.
- 일반 Outcome과 Ultimate가 실행 중일 때 동기 콜백으로 같은 승인된 명령을 재진입하지 못하도록 보호한다. 부분 효과 예외는 Abort하여 동일 효과의 재시도를 차단한다. 기존 AllOut 보호도 유지한다.
- 기절한 캐릭터는 기존 versioned 선언 외 compatibility 선언·대응에서도 거부한다. Panic 공격/인계 및 잘못된 명령은 승인 전에 MP·BP·손패를 소비하지 않는다.
- 세션의 전투 종료 여부를 기록하고 실행기에서 확인한다. Abort/Exit 이후 전달된 이전 스킬 실행 요청은 거부되며, 피해 도중 종료되면 남은 타격·대상 및 상태 부여를 중단한다.
- 사망한 대상에게 상태를 추가하지 않는다. 피해 직후 HP 0이 된 대상에게 BP 회복 효과를 적용해 다시 살아나게 하지 않는다. 살아 있는 대상에 대한 기존 상태 중첩 규칙은 유지한다.
- 다중 타격은 승인된 하나의 실행에서 피해만 추가하며 기본 상태 부여는 한 번이다. 별도의 `StatusApplications` BP 효과가 명시적으로 설정되었을 때만 해당 횟수만큼 추가 부여한다.
- 아이템 처리 중 동기 인벤토리 변경 알림에서 동일 요청/다른 공격 명령이 재진입하지 못한다. 제거 후 대상 사망, 전투 종료 또는 세션 해제가 확인되면 효과를 실행하지 않고 기존 인벤토리 API로 재고를 복원한다. 사용 중 Pause도 명령을 거부한다.
- 인벤토리 변경·재로드 시 기존 Adapter가 UI 명령 Controller에 갱신을 전달한다. 아이템 실행 중에는 갱신을 보류하고 완료 후 갱신한다. 알림 예외 시 제거한 재고를 복구하며, 복구를 확인할 수 없으면 재시도를 막기 위해 전투를 Abort한다.
- 총공격과 궁극기는 같은 SkillRunner/상태 처리 계약을 사용한다. Reward/참가자 EXP 지급 정책은 바꾸지 않는다.

06A의 기존 테스트 중 치명적 총공격이 사망 대상에게도 상태를 부여한다고 기대하던 부분은 이번 작업의 명시적 사망 대상 제한과 충돌했다. 새 요구에 맞춰 생존 대상은 1회 적용, 사망 대상은 미적용을 각각 검증하도록 바꿨다. 기존 테스트를 삭제하지 않았다.

## 실제 지원과 Content Pending

| 항목 | 현재 지원 및 남은 계약 |
| --- | --- |
| Buff/Debuff | 상태 ID·표시·극성, Refresh/Replace/Stack, 피해·합·체간 보정 지원. 제작 데이터와 실제 Scene UI 활성화는 별도 검증. |
| 지속시간 | `Explicit`은 참여자가 완료한 Exchange마다 1회 감소; 새로 부여·변경된 상태는 해당 Exchange에서 제외. `UntilRemoved` 유지. Explicit 0의 기존 처리도 유지. |
| 기절 | 일반 Status 스택과 별도의 ICombatant/Stagger·Posture 경로. 06A Standoff 복귀 정책 유지. |
| Mental/Panic | 별도 Runtime State와 완료 Exchange 결과 집계, 확정형 Overcome 지원. 기본 35는 기존 설정값이며 별도 작성 설정도 기존 계약대로 존중. |
| 즉시 회복 | 전투 아이템의 살아 있는 대상 HP 회복 및 별도 BP Healing 계약 지원. 사망/부활은 지원하지 않음. |
| DoT / HoT / 출혈 | 주기·대상·수치·종료 규칙과 실행기 없음. 명세 및 제작 데이터가 필요하며 이번에 임의 구현하지 않았다. |
| 무적 / 상태 면역 | 명시적인 판정 데이터/효과 처리 계약이 없다. 상태 ID나 극성만으로 면역을 추론할 수 없다. 요구사항의 실제 무적·면역 보호 테스트는 미실행이며 명세·데이터·실행 정책이 필요하다. |
| 전투 아이템 | Self/SingleAlly, 즉시 HP 회복 또는 하나의 authored Status 적용. 부활, MP/BP 회복, 해제/정화, 복합 효과, 적 대상, 별도 전투 비용은 현재 계약에 없다. |
| Content Pending | 제작된 Status/Item/Ultimate/AllOut 데이터의 대상·수치·내성/면역 규칙과 Production UI/Inspector 연결 확인이 남았다. |

정적 GUID 검색에서 `CombatStatusDefinitionSO` 자산은 `Assets/GAME/Tests/Editor/Fixtures/CombatStatusValidation`의 4개만 확인됐다. `CombatItemDefinitionSO` 및 `CombatItemCatalogSO` 자산과 `CombatItemUseAdapter` Scene/Prefab 직렬화 참조는 `Assets`에서 발견되지 않았다. Production 스크립트에도 해당 Adapter의 동적 추가 경로는 없다. 따라서 격리 fixture 통과만으로 Production 전투 아이템이 연결되어 있다고 볼 수 없다. 향후 실제 콘텐츠 정책에 따라 Combat Item 정의/카탈로그 제작과 Adapter 컴포넌트 연결이 필요하다. 이 검색은 누락된 정책·데이터를 임의 제작하거나 보류 Scene을 편집할 근거가 아니다.

`SkillRunner.TryExecute` 자체는 기존 테스트와 compatibility 호출이 재사용하는 저수준 공개 API다. Production의 단회 승인은 StateMachine/Orchestrator가 관리한다. 임의 호출자가 동일 저수준 요청을 독립적으로 두 번 호출하는 것까지 세션 전체에서 금지하는 새 공개 계약은 도입하지 않았다.

## 아이템 UI의 06A 실패 재검증

기존 `CombatItemUsePlayModeIntegrationTests.IsolatedFinalExchange_ItemAdapterInventoryAndBinderRunInActualPlayMode`는 아이템을 `skillListRoot`에서 찾는다. 현재 Binder는 기존 `handoffListRoot`에 아이템 명령을 만든다. 이 차이는 06B 변경 전 보관된 Binder 소스에서도 확인된다.

기존 테스트 기대값을 수정하지 않았다. 별도 실제 Editor Play Mode fixture `ItemAtCurrentCommandRoot_ReflectsInventoryAndUsesOneItem`는 현재 명령 root의 활성 버튼, 표시 수량 2 → 실제 1, 대상 회복 4 → 8, 공격권 양도 후 잔여 수량 표시를 검증한다. 이 결과는 격리 fixture에서 현 코드 경로가 동작함을 뜻하며 실제 Production 화면 배치·가독성이나 어느 root가 최종 디자인 요구인지 확인한 것은 아니다. 기존 실패는 명시적인 UI 위치 계약이 정리될 때까지 별도 추적한다.

## 변경 파일 및 호환성

### Production 수정

- `Assets/GAME/Scripts/Combat/Runtime/Actions/SkillRunner.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Model/CombatSession.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Model/CombatantCombatState.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatStateMachine.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Core/CombatFlowOrchestrator.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatItemUseExecutor.cs`
- `Assets/GAME/Scripts/Combat/Runtime/Integration/CombatItemUseAdapter.cs`
- `Assets/GAME/Scripts/Combat/Runtime/UI/FinalCombatPlayerCommandController.cs`

### 기존 테스트 확장

- `Assets/GAME/Tests/Editor/CombatClashSeriesIntegrationTests.cs`
- `Assets/GAME/Tests/Editor/CombatBpValidationTests.cs`
- `Assets/GAME/Tests/Editor/CombatItemUseIntegrationTests.cs`
- `Assets/GAME/Tests/Editor/CombatItemUsePlayModeIntegrationTests.cs`
- `Assets/GAME/Tests/Editor/CombatAllOutRecoveryTests.cs`
- `Assets/GAME/Tests/Editor/CombatAllOutRecoveryPlayModeTests.cs`

추가: 이 보고서. 삭제·이동 없음. 기존 public API 삭제·이름/타입 변경, SerializeField 변경, 기존 `.meta`/GUID 변경, Scene/Prefab/SO/패키지 변경 없음. CombatSession에 내부 전투 종료 상태와 Orchestrator에 추가 이벤트 `CombatItemOptionsChanged`를 도입했다.

## 실제 자동 검증

- 초기 집중 1차: 테스트 lambda의 `_` 이름이 `out _`와 충돌해 C# 컴파일 실패. 테스트 이름 충돌을 수정했다.
- 집중 2차: **80/81 통과**. 신규 3라운드 재진입 검증이 일반 Outcome의 비버전 overload가 반복 호출될 수 있음을 확인했다. 실행 가드를 수정했다.
- 집중 3차: **97/97 통과**, 실패/ignored 0. `Logs/CBT-R2-06B-Focused-3.xml`, `.log`, Unity 종료 0.
- 집중 3차 구성: AllOut EditMode 29, AllOut 실제 EnterPlayMode 4, BP 15, 반복 합 26, 아이템 EditMode 10, 별도 실제 Item UI PlayMode 1, Status FinalExchange 11, Status 실제 PlayMode 1.
- 테스트 fixture 숫자와 status ID는 검증용이며 Production 밸런스가 아니다.
- 추가 집중 회귀: **344/344 통과**, `Logs/CBT-R2-06B-FinalFocused.xml`. 별도 테스트는 실제 EnterPlayMode를 포함한다.
- 아이템 표시 갱신 및 예외 복구 변경 후 집중 검증: **12/12 통과**, `Logs/CBT-R2-06B-ItemRefresh.xml`. Editor Play Mode에서 재고 추가·제거·재로드 직후 UI 갱신을 포함한다.
- 최종 전체 EditMode 실행: **1,554 pass / 8 fail / 1 ignored**, 총 1,563. `Logs/CBT-R2-06B-AllEditMode-AfterItem.xml` 및 `.log`, Unity 종료 코드 2(테스트 실패). 06A 최종 XML과 실패 이름을 비교한 결과 동일하며 신규 실패는 없다.
- Unity 컴파일 오류 없음. 기존 경고 4종: FieldEnemy의 미사용 이벤트 CS0067, StoryInteractionController의 미사용 필드 2건과 RescueNpcActor의 미사용 필드 1건 CS0414.

검증 범위: 3회 합 동안 정신력 100·상태 지속시간 3 유지 → 결과 시 정신력 85 → Exchange 완료 시 지속시간 2; 동기 재진입 거부; 다중 타격 및 명시적 상태 추가 횟수; 사망 후 회복/상태 방지; 피해 중 Abort 후 재생 거부; 재고 사용과 rollback; 패닉 시 MP/BP/카드 보존; 실제 총공격/아이템 UI. Overcome=35, 공격권 양도, 인계·적 AI, Reward/EXP 중복은 기존 관련 회귀 결과로 확인한다.

## 06A 기준 실패 및 ignored 보존

06A 전체 기준 1,538 pass / 8 fail / 1 ignored, 총 1,547. 비교해야 하는 실패 이름:

1. `Game.Tests.Combat.CombatItemUsePlayModeIntegrationTests.IsolatedFinalExchange_ItemAdapterInventoryAndBinderRunInActualPlayMode` — skillListRoot 대 handoffListRoot 기대값.
2. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasCanonicalCombatRewardUiCameraAndBootstrapOwners` — 보호 Scene Debug opt-in.
3. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasExplicitInteractionOwnersWithoutLegacyInteractionPaths` — 동일.
4. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasOneCanonicalRepeatableNpcInteraction` — 동일.
5. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasQuestDerivedCompletionContractWithoutLegacyExitOrFallbackDestination` — 동일.
6. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_IsCleanEnvironmentMigration` — 동일.
7. `Game.Tests.Integration.FilmUniqueProductionPlayModeTests.ProductionScene_ContactAcquire_UIEquip_FieldInputSkill_ColdSaveLoad` — 보호 Scene Hand validation fixture와 임시 스킬 불일치.
8. `Game.Tests.Integration.ProductionSceneWiringTests.DungeonTemplate_IsCanonicalSingleOwnerProductionSource` — template의 Debug/Legacy 컴포넌트.

Ignored: `Game.Tests.Integration.PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners`; 선행 write 이후 별도 Editor 프로세스 read가 필요하다. ignored를 pass로 합산하지 않는다.

## Inspector 및 수동 Play Mode

이번 변경에서 새 Inspector 연결·컴포넌트·ScriptableObject 작업은 수행하지 않았다. Production 전투 아이템 사용을 위해서는 별도 승인된 콘텐츠 작업에서 `CombatItemUseAdapter` 컴포넌트 추가와 Orchestrator/Inventory/Catalog 참조 및 Combat Item 정의/카탈로그 자산이 필요하다. UI Binder root와 버튼 템플릿, authored AllOut/Ultimate/Status 콘텐츠도 별도 Production 검증에서 확인해야 한다. 05/06A에서 보류한 연결을 이번에 강제 수정하거나 검증 완료로 처리하지 않았다.

독립 fixture: Unity 6000.3.23f1 Test Runner의 EditMode에서 CombatClashSeriesIntegrationTests, CombatBpValidationTests, CombatItemUseIntegrationTests, CombatAllOutRecoveryTests 및 추가 Item UI Play Mode 테스트를 실행한다.

Production 수동 검증은 05/06A 연결 보류가 해제된 후 실시한다.

1. 3라운드 합에서 중간 라운드의 정신력과 기존 상태 지속시간이 변하지 않고 Exchange 완료 후 한 번만 변하는지 확인한다.
2. 패닉 캐릭터의 공격·대응·인계·아이템이 거부되고 MP/BP/카드/재고가 유지되는지 확인한다. Overcome 후 설정된 35로 회복하고 상대에게 공격권이 넘어가는지 확인한다.
3. 다중 타격의 생존 대상 상태가 기본 1회만 부여되고 치명상 대상에는 후속 상태/회복이 없는지 확인한다. 별도 StatusApplications 작성값이 있으면 그 횟수를 확인한다.
4. 아이템 표시 수량과 실제 재고, 대상 유효성, 1회 회복/소비, 중복 클릭 거부를 확인한다. 사망 대상·전투 종료·취소 시 효과와 소비가 남지 않아야 한다.
5. Ultimate 및 AllOut의 생존/치명·중복 입력·전투 종료 후 연출 재전달, 완료 결과와 참가자별 Reward/EXP 1회 지급을 확인한다.
6. 현재 상태이상·아이템 UI의 위치와 읽기 쉬움, 각 Serialized 참조를 실제 Dungeon Scene에서 확인한다. 무적/면역은 정식 계약 확정 전 통과로 기록하지 않는다.

미실행: Production Dungeon Inspector·수동 Play Mode, 실제 입력/애니메이션·UI 가독성, Player Build, 별도 PlayMode assembly, 재시작 Save read, 명세가 없는 무적/면역·DoT/HoT/출혈 동작. 격리 Editor EnterPlayMode는 Production 통합 검증이 아니다.

## Development tracking

- System: Combat / Mental, Status, Item, Ultimate, AllOut.
- Feature: CBT-R2-06B 실행 경계 재검증 및 승인된 효과의 중복·사망/종료 경계 보강.
- Production owner: EntryPoint → Session/StateMachine/Orchestrator → SkillRunner 및 Item Executor; Inventory/Reward 소유권 유지.
- Requirement: 현재 CBT-R2-06B 작업문. 별도 디자인 문서의 미확정 수치를 채택하지 않았다.
- Implementation: **In Progress — 기존 지원 범위 보강, 무적/면역 등 정식 계약 대기**.
- Compile: 실제 Unity 테스트 실행 결과에 따른 확인.
- Inspector: Production 미검증. Serialized asset은 이번 작업에서 변경하지 않음.
- Play Mode: 격리 fixture 결과와 Production 미검증을 구분.
- Integration: 격리 Combat/Item/Reward 회귀와 실제 Scene 통합을 구분.
- Recommended tracking: **In Progress — Supported Paths Verified / Status Contracts and Production Integration Pending**.
- 05/06A의 별도 보류와 8개 기존 실패는 해결했다고 기록하지 않는다. 전체 시스템 완료 아님.

## 최종 점검 기록

최종 전체 결과는 1,554 pass / 8 fail / 1 ignored다. 06A의 1,538 pass / 8 fail / 1 ignored와 비교하면 실패 이름과 ignored 이름이 같다. 테스트가 추가되어 전체 수는 달라졌다.

시작 시 기존 변경 42개 중 이번 범위 외 33개는 SHA-256이 유지됐다. 나머지 9개는 위에 명시한 기존 전투 코드·테스트를 기준으로 확장했다. Quest/Reward/Save 및 기존 Scene/Prefab/기타 Asset 수정은 덮어쓰지 않았다. 이번 수정 파일 범위의 `git diff --check`는 통과했다. 저장소 전체 검사는 기존 폰트·Scene 파일의 공백 문제를 보고하므로 그 파일들은 수정하지 않았다.
