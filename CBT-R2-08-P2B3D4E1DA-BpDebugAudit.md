# CBT-R2-08-P2B3D-4E1D-A — BP Debug 중복 및 기존 6실패 감사

## 결론

**감사 완료. 보호 자산·Runtime·테스트는 수정하지 않았다. Production Cutover 없음.** 추가 파일은 본 보고서뿐이다.

1. `Dungeon_1_Production`의 `Runtime/Combat/CombatRuntime`에는 Prefab 상속 BP Debug 1개와 Scene Added BP Debug 1개가 직렬화돼 있다. 둘 다 Enabled/enableValidation=true, initialPlayerBp=3, 같은 skill2 Ultimate다. Scene Added BP도 실제 설정을 가진다.
2. 기존 실패 6건을 새로 재현했다. 5건은 같은 Production validator의 **Scene Added Hand Debug 존재 금지**에서 먼저 실패한다. 1건은 Template의 **상속 BP Debug 존재 금지**에서 실패한다. 여섯 건 모두 BP 중복 실행이나 BP 수치 오류를 검출한 것은 아니다.
3. 두 BP는 같은 EntryPoint/Orchestrator/Session을 대상으로 하지만, Session의 기존 BP 생성 guard 때문에 BP가 두 번 누적된다고 단정할 수 없다. 먼저 생성한 BP Runtime의 enhancement fixture 차이는 실행 순서에 따른 위험이다. 실제 Production에서 두 callback의 순서를 계측한 것은 아니다.
4. 원본 BP를 일괄 제거하면 다른 소비 씬과 원본 BP 유지를 요구하는 기존 검사가 영향을 받는다. 원본을 유지하고 Scene별 제거 override를 사용하는 방안이 더 좁다.
5. Debug 전체 제거와 중복 BP 정리는 분리해야 한다. 현재 연결된 V1 Config는 정식 Hand/BP 초기화가 비활성이다. Debug 제거만으로 기존 6개 gate를 통과시키면 현재 검증용 Hand/BP/Clash 동작이 사라질 수 있다. 이번 감사는 이를 Cutover 승인으로 해석하지 않는다.

## 지침·기준 상태·변경 범위

- 지침: 루트 `AGENTS.md`를 읽었다. 숨김 폴더 포함 검색에서 적용 중첩 `AGENTS.md`/`AGENTS.override.md` 없음.
- 필수 선행 자료: `CBT-R2-08-P2B3D4E1A-ProductionConnectionAudit.md`, `CBT-R2-08-P2B3D4E1CR1-Verification.md`를 읽고 현재 코드/직렬화와 대조했다.
- branch `main`, HEAD `b42544824838e7de0e70b71afc2ca21a59db8192`.
- 시작 시 tracked 수정 9개: 기존 Encounter Group/Trigger, WorldEncounterIntegrationTests와 D4E-1C-R1의 Core/Title/Character Skill/SaveLoadPreparation 변경. 기존 미추적 Character/Skill/Config 후보, 테스트, 격리 Scene/Prefab 및 보고서도 기준선에 포함했다.
- 편집 전 범위 선언: 보고서 1개만 추가. 기존 파일 2,657개 SHA-256 기록 후 종료 시 재대조. 테스트 출력/기준 해시는 저장소 밖에 기록.
- Runtime/공개 API/SerializeField/Save Schema/패키지/자산 GUID 변경, 삭제·이동·컴포넌트 제거, branch/commit/push/PR 없음.

## Prefab 원본과 Scene Instance 구분

공통 Prefab: `Assets/GAME/Prefabs/CombatRuntime.prefab`, GUID `1bfc52cd235e809449d17d48571f5a38`.

| 대상 | 정확한 식별자 / 출처 | 현재 값 |
|---|---|---|
| 원본 GameObject | `CombatRuntime`, source GO fileID `983610231012664106` | active=1 |
| 원본 EntryPoint | source component `8945921886167358035` | director/flowOrchestrator는 같은 Prefab 내부 참조; registry=[]; Config GUID `55e6978e3cf88ca4994f7855f907bc6c` |
| 원본 Orchestrator | source component `3458696486045702784` | entryPoint=`8945921886167358035` |
| 원본 BP | `Game.Combat.Debugging.CombatBpValidationOptIn`, source component `3049129679861795314`, script GUID `4bd31fad07c44e9795d18530d37a6855` | m_Enabled=1, enableValidation=1, maximumBp=3, roundCyclesPerGain=2, maximumInvestment=3, initialPlayerBp=3 |
| 원본 BP fixture | skillFixtures[0].skill / ultimateSkill | `Skill_skill2.asset`, GUID `6242dada02b12a8468930dc1a8114c3e`; SkillId2 / key `skill.production.0002`; fixture kind=1(ClashPower), perPoint=1 |
| Production Instance | `Assets/GAME/Scenes/Dungeon_1_Production.unity` → `Runtime/Combat/CombatRuntime` | PrefabInstance fileID `1665839071176935753`; stripped GO `1665839071176935756`; parent Transform `1961567356` |
| Template Instance | `Assets/GAME/Scenes/Dungeon_Template.unity` → `Runtime/Combat/CombatRuntime` | 같은 source Prefab / 같은 Instance fileID. 다른 Scene 문맥이므로 local fileID가 같아도 충돌은 아님 |

Production/Template 모두 해당 Instance의 `m_RemovedComponents=[]`다. 원본 BP에 대한 property override가 없으므로 Enabled와 BP 설정 전체를 상속한다. 상속 BP의 stripped component block이 Scene YAML에 없다는 이유로 BP가 없다고 해석하지 않는다.

### Production의 Scene Added Components

모두 같은 stripped GO `1665839071176935756`에 속한다. `m_AddedComponents[].targetCorrespondingSourceObject`는 source GO `983610231012664106`, insertIndex=-1이다. 컴포넌트 자체의 m_CorrespondingSourceObject/m_PrefabInstance는0이며, Prefab의 property override와 구분된다.

| Scene local component ID | Component | Enabled / Inspector 설정 |
|---|---|---|
| `1665839071176935757` | CombatCameraController | m_Enabled=1. **정식 카메라 연결이므로 유지 대상** |
| `1665839071176935759` | CombatHandValidationOptIn | m_Enabled=1 / enableValidation=1; script GUID `bce1360c580248db935ccf2b6f845871`; playerCardSource=InspectorFixture; player Normal4/Clash6, capacities2/3, seed2303; enableEnemyHand=1, enemy Normal4/Clash6, seed2304 |
| `1665839071176935761` | CombatClashSeriesValidationOptIn | m_Enabled=1 / enableValidation=1; script GUID `040a0e4f14ef4872ac50661f4ee830da`; attacker/responder3/3, maxComparisons3, decisionDurationSeconds5 |
| `1665839071176935766` | CombatBpValidationOptIn | m_Enabled=1 / enableValidation=1; maximumBp3 / cycle2 / investment3; **initialPlayerBp3, ultimateSkill=같은 skill2**; skillFixtures=[] |

Scene Added Debug 3개 모두 entryPoint=`1665839071176935754`, orchestrator=`1665839071176935755`를 참조한다. 이 두 stripped component는 원본 EntryPoint/Orchestrator의 동일 인스턴스다. 상속 BP 역시 Prefab 내부 참조 remap을 통해 이 두 객체를 쓴다.

Template Instance에는 Camera(`...5757`)만 Added Component로 기록돼 있다. Scene Added BP/Hand/Clash는 없고, 상속 BP 1개가 남는다. 따라서 Template 실패는 BP 중복이 없어도 발생한다.

**선행 기록 보정:** Scene Added BP의 skillFixtures가 비어 있다는 설명은 맞지만, initial BP/Ultimate까지 빈 설정인 것은 아니다. 두 BP 모두 initialPlayerBp3/ultimateSkill을 가지므로 `IsShadowedByConfiguredFixture()`의 빈 fixture 차단 조건에 해당하지 않는다. 과거 보고서는 수정하지 않고 현재 판단을 이 보고서에 명시한다.

## 이벤트·쓰기·실제 Owner

### Debug 경로

- Hand/BP: OnEnable → ResolveReferences → 같은 Orchestrator의 `ClashSeriesValidationEnabled`에 각각 C# delegate 구독. 자체 `_subscribedOrchestrator` guard는 **각 인스턴스의 재구독**을 막으며, 서로 다른 BP 인스턴스 2개를 합치지 않는다. OnDisable에서 대칭 해제한다.
- Clash: 같은 EntryPoint의 `OnCombatStarted` 구독 → `TryEnableClashSeriesValidation` → 위 Orchestrator 이벤트 발행. OnDisable에서 해제한다.
- EntryPoint는 Session/StateMachine/Director/적 명령 Owner bind 후 `OnCombatStarted`를 발행하고 그 뒤 적 opening 선언을 활성화한다. 이 사이 Debug fixture가 개입한다.
- BP `TryApplyToSession`은 `TryEnableBpValidation`으로 BP/Ultimate Runtime을 만들고, initialPlayerBp3을 설정하며, skill2 enhancement/Ultimate를 등록한다. UI 표시 전용 컴포넌트가 아니다.
- `ApplyWhenClashEnabled`는 BP가 이미 있으면 다시 생성/초기 BP 지급하지 않고 Ultimate 할당만 시도한다. Session도 `BpRuntime != null`이면 재생성을 거부한다. **중복 컴포넌트 = BP3+3 누적**으로 보고하지 않는다.
- 먼저 상속 BP가 생성하면 ClashPower fixture가 들어가지만, 먼저 Added BP가 생성하면 빈 enhancement로 생성되고 뒤 callback은 원본 fixture를 보충하지 않는다. 이는 코드에서 도출되는 조건부 위험이다. 두 객체의 실제 Production callback 순서·최종 enhancement를 계측하지 않았으므로 발생 확정 결함으로 표기하지 않는다.
- `[DisallowMultipleComponent]` 선언만으로 이미 저장된 상속+Added 중복이 해결됐다고 가정하지 않는다. 이번 증거는 직렬화 중복과 코드 경로이며 실제 Production callback 수를 계측한 검사는 아니다.
- Hand/BP/Clash 모두 `HasProductionHandBpInitialization` guard가 있어 정식 초기화 완료 세션에 fixture를 덧붙이지 않는다. 단, guard는 **컴포넌트 존재 금지 자산 검사**를 통과시키지 않는다.

### Production 책임과 UI

| 책임 | 기존 Production Owner | Debug/후보 관계 |
|---|---|---|
| 전투 시작/종료·Snapshot/정식 초기화 | CombatEntryPoint → CombatSession / CombatBpRuntime / CombatUltimateRuntime | `InitializeProductionHandBp`가 정식 소유 경계. Debug를 두 번째 Production Owner로 승격하지 않음 |
| Clash 실행·명령 조정 | CombatFlowOrchestrator / CombatStateMachine / 기존 policy·resolver | Debug Clash가 검증값을 공급하는 현재 우회 경로 유지 중 |
| 전역 상태 | GameFlowController → GameStateMachine, RuntimeBootstrapper의 기존 서비스 생성 | Production Scene에 두 전역 Owner를 새로 추가할 이유 없음 |
| 화면 루트 | `Runtime/UI/ProductionDungeonUI`의 UIScreenRouter / GameUIRootController | router는 GameState에 따라 Combat/Reward 등을 표시 |
| 전투 버튼·BP 표시 | `Runtime/UI/ProductionDungeonUI/CombatCanvas/CombatHUD/FinalExchangePanel`의 FinalCombatUIBinder | 동일 세션의 BpRuntime/Hand/Ultimate를 읽고 기존 player command controller로 전달 |

두 BP에는 UI root/Button/Binder/Router를 가리키는 SerializeField가 없다. 같은 Session을 통해 **동일 UI가 관찰하는 데이터에 영향**을 줄 뿐, 각자 별도 BP UI를 생성하지 않는다. FinalCombatUIBinder도 BP Debug 객체를 참조하지 않는다. 원본 UIScreenRouter의 `uiRoot`는 같은 Prefab의 GameUIRootController, `stateMachine=null`은 런타임의 유일한 state owner로 해석되는 기존 경로다.

현재 실제 Config는 `FinalCombatRuntimeConfig_V1.asset`이다. 파일에는 새 Hand/BP 필드가 직렬화돼 있지 않아 C# 기본값으로 enableProductionHandBp=false, capacities0/0, handCardRules=[], BP 설정0, Clash 비교/시간0을 사용한다. 기존 V1 MP3/초기2 등은 유지된다. Production registry는 Scene override로 기존 Skill1/2/11/12 네 개이며 원본 Prefab의 빈 배열과 구분해야 한다.

D4E-1C-R1의 아난 Start/덱 후보는 별도 비활성 상태다. 기존 덱 후보 GUID `44a8b6f4c6314396953f0792e126d13a`, 2/3 규칙과 New Game/SaveLoad 코드는 변경하지 않았다. 이번 BP 감사가 후보 활성화 승인을 포함하지 않는다.

## 기존 6실패의 개별 직접 원인

아래는 **이번 새 XML의 실제 실패**다. 첫 5건은 각 stack trace를 별도로 확인했다. 이름에 UI/NPC/Quest가 들어 있어도 해당 개별 기능 Assertion에 도달하기 전 공통 validator에서 중단된다.

공통 Production 조건 P:

- Scene: `Assets/GAME/Scenes/Dungeon_1_Production.unity`.
- Assertion: `Assert.DoesNotThrow(DungeonOneProductionMigrationUtility.ValidateProductionScene)`.
- 내부 기대값: Debug/Legacy/Demo/Test namespace/type를 가진 `forbidden == null`.
- 실제 예외: `Forbidden Demo/Debug/Test/Legacy component remained: Game.Combat.Debugging.CombatHandValidationOptIn on CombatRuntime.`
- 정확한 직접 자산: `Runtime/Combat/CombatRuntime` Scene Added Hand `1665839071176935759`, m_Enabled=1, enableValidation=1, 위 표의 fixture4/6. 관련 원본 Prefab에는 Hand가 없다.
- 검사 구현은 `Assets/GAME/Editor/DungeonOneProductionMigrationUtility.cs`의 forbidden 검색/Require다. 현재 소스의 검색은 361행 부근이며, XML의 컴파일된 예외 위치는 456행으로 기록된다. 행 번호 차이보다 실제 예외/메서드/대상 객체를 기준으로 대조했다.

| 번호 / 테스트와 Assertion 위치 | 현재 실제 값 → 기대값 | BP 중복과의 관계 | 최소 수리 후보·참조·자동 검사 |
|---|---|---|---|
| 1. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_IsCleanEnvironmentMigration`, test line61 | 조건 P: Hand 객체 존재 → forbidden=null | 직접 원인은 Hand. BP 중복 수나 값은 검사하지 않음 | Hand 제거만 하면 남은 BP/Clash도 같은 금지 조건에 걸림. 최종적으로 이 Instance의 Debug4개(상속 BP+Added3개)를 제거해야 함. Camera/Core 참조 유지, 같은 테스트 재실행. 이후 환경 검사 통과 여부는 제거 전에는 미검증 |
| 2. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasQuestDerivedCompletionContractWithoutLegacyExitOrFallbackDestination`, line166 | 조건 P: 같은 Hand 객체 존재 → forbidden=null | Quest/SO 보상/목적지 실패나 BP 중복 검출이 아님 | 위 Debug4개 제거 후보. QuestRuntime/DungeonCompletionFlow 및 QuestDefinition은 이 실패 때문에 수정할 근거 없음. 동일 테스트로 자동 재검증, 후속 Quest Assertion은 현재 미도달 |
| 3. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasCanonicalCombatRewardUiCameraAndBootstrapOwners`, line338 | 조건 P: 같은 Hand 객체 존재 → forbidden=null | 두 CombatEntryPoint/UI/Camera Owner가 확인됐다는 뜻이 아님 | Debug4개 제거 후보, EntryPoint/Director/Camera/Reward/FieldLock 유지. 같은 테스트와 UI Routing/Owner 검사를 자동 실행. 이 예외로 UI Prefab을 재배선하지 않음 |
| 4. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasOneCanonicalRepeatableNpcInteraction`, line399 | 조건 P: 같은 Hand 객체 존재 → forbidden=null | NPC 개수/InteractionId/반복 사용 검사 실패가 아님 | Debug4개 제거 후보. NPC Prefab·StoryInteraction SO 수정 불필요. 같은 테스트 자동 재실행; NPC 후속 Assertion은 현재 미도달 |
| 5. `Game.Tests.Integration.DungeonOneProductionSceneTests.ProductionDungeonOne_HasExplicitInteractionOwnersWithoutLegacyInteractionPaths`, line431 | 조건 P: 같은 Hand 객체 존재 → forbidden=null | InteractionRuntime/Runner 중복 또는 BP 중복 실행을 검출한 결과가 아님 | Debug4개 제거 후보. Interaction Owner 연결 유지. 같은 테스트 자동 재실행; 뒤 Interaction Assertion은 현재 미도달 |
| 6. `Game.Tests.Integration.ProductionSceneWiringTests.DungeonTemplate_IsCanonicalSingleOwnerProductionSource`, assertion block253–259 | Template components.Any(Debugging/Legacy/구형 type)=true → false | **중복과 무관**. Template의 상속 BP 하나로 true | `Assets/GAME/Scenes/Dungeon_Template.unity` 같은 GO에서 source BP `3049129679861795314`를 Scene instance 제거 override로 제외하는 후보. Camera와 source Prefab 유지. 해당 테스트로 자동 재검증 가능 |

6번의 앞선 CombatEntryPoint count=1 Assertion은 통과했다. Production validator도 Missing Script/여러 canonical owner count 검사를 통과한 뒤 forbidden 검사에서 실패했다. 따라서 현재 여섯 결과만으로 “Production Owner 중복”, “Registry 손상”, “UI 배선 오류”를 원인으로 바꾸지 않는다. 반대로 첫 gate 제거 이후 추가 실패가 없다고 보장하지도 않는다.

두 검사는 Disabled/inactive Debug도 타입으로 검색하므로 `m_Enabled=0` 또는 enableValidation=false만으로 여섯 gate를 해결할 수 없다. 테스트 삭제/완화/Debug whitelist는 제안하지 않는다.

## 테스트 계약 충돌과 최소 변경 분류

현재 자산이 역사적 검증용 Prefab이라는 사실과 깨끗한 Production Scene 요구를 구분해야 한다.

- `Game.Tests.Combat.CombatFinalExchangeProductionWiringTests.CombatRuntimePrefab_EnablesBpValidationWithApprovedVerificationValues`는 원본 BP 존재/Enabled 값/3·2·3/skill2 fixture를 요구하며 이번에 PASS했다. 이것은 BP Debug가 Production Owner여야 한다는 승인이 아니다. 원본 전체에서 BP를 삭제하면 이 검사가 새로 실패한다.
- `Game.Tests.Integration.CombatProductionCutoverRehearsalTests.SourceAssets_ReadOnlyFootprint_ReportsDataAndInstanceOverrides`는 원본 BP1개뿐 아니라 Production의 Added Hand/Clash/BP fileID 세 개가 모두 존재한다는 **당시 footprint**를 고정한다. Scene Added BP만 제거해도 이 검사 기대값은 오래된 inventory가 된다. 이번에 수정하지 않았다.
- 실제 Production 계약은 사용자 지침의 단일 Owner/Debug 비의존과 승인된 자산 전환 범위가 기준이다. 과거 footprint를 지키기 위해 중복을 영구 유지하거나, 여섯 fail을 없애려고 새 정책 없이 테스트를 완화하는 양쪽 접근을 피한다.

| 분류 | 정확한 대상 / Inspector 조치 제안 | 판단 및 영향 |
|---|---|---|
| A — Prefab 기존 유지 | `CombatRuntime.prefab`의 EntryPoint/Orchestrator/Director/WorldLifecycle/FieldLock/RewardBinder 및 **현재 source BP** | 가장 작은 중복 정리에서는 원본 전부 유지. source BP는 임시 검증 호환 fixture로만 인정. 다른 소비 씬/원본 fixture 검사 파급 방지 |
| B — 중복 Added 제거 후보 | Production `Runtime/Combat/CombatRuntime` → **Added** CombatBpValidationOptIn `...5766`만 Remove Component. Overrides에서 해당 Added Component가 사라졌는지 확인 | D4E-1D-B의 최소 BP 중복 정리 후보. source BP는 남아 BP1개가 됨. 기존 5개 Production gate와 Template gate를 해결한 것으로 보고하지 않음. enhancement fixture의 실제 동작 회귀 필요 |
| B/D — 별도 최종 Debug 제거 후보 | 같은 GO의 Added Hand `...5759`, Added Clash `...5761`, 그리고 상속 BP `3049129679861795314` | 단순 중복 정리와 분리. 현재 Hand/BP/Clash를 공급하므로 정식 초기화 준비/연결/활성화 승인 없이는 동작 보존 보장 불가. 이번 및 Cutover 없는 B 범위에 자동 포함하지 않음 |
| C — Scene별 상속 제거 override | Production/Template 각 Instance의 source BP Remove Component를 **Scene에만 저장**, Prefab에 Apply하지 않음 | 최종 깨끗한 Scene에 필요한 override. m_RemovedComponents가 source BP를 가리켜야 함. source GUID/meta 유지. Template은 Production과 별도로 승인 |
| C — 기존 연결 유지 | Added Camera `...5757`; EntryPoint.director/flowOrchestrator/skillDefinitions/finalExchangeRuntimeConfig; Director/WorldLifecycle.cameraController; RewardBinder.rewardPanel; FieldLock 목록; Router/Binder UI 필드 | 현재 실패에 대해 재연결할 근거 없음. Debug만 제거할 경우 새 Inspector 연결 요구 없음. 카메라를 인접 Debug와 함께 제거하지 않음 |
| D — 책임 분리 | 기존 `CombatEntryPoint.InitializeProductionHandBp`, 기존 Config/Skill Snapshot 경계 | 새 Runtime/Debug Manager 및 Runtime 코드 변경 불필요. Debug를 끈 채 Config flag만 켜는 것도 불완전한 V1 입력으로 실패하므로 금지 |
| E — 기대값 검토 | 위 `SourceAssets_ReadOnlyFootprint_ReportsDataAndInstanceOverrides`의 Added BP 존재 Assertion | 실제 B 자산 변경을 승인할 경우에만 기대 inventory를 갱신하는 별도 테스트 수정 승인 필요. Added BP 부재/상속 BP1개/Core·Camera 연결 보존을 검사하는 방향. 기존 여섯 canonical gate는 그대로 유지 |
| E — 원본 BP 검사 | `CombatRuntimePrefab_EnablesBpValidationWithApprovedVerificationValues` | source BP를 유지하는 최소안에서는 수정 불필요. 나중에 원본 fixture 정책을 바꿀 때만 별도 검토. 현재 이름의 Production 표현만으로 global BP 삭제를 정당화하지 않음 |

가역적인 사전 관찰만 필요하면 Added BP의 `m_Enabled=false` 한 필드만 Scene override로 저장하는 더 작은 선택지도 있다. 이 경우 해당 인스턴스의 구독/자동 적용은 중단되지만 컴포넌트는2개로 남고 여섯 gate도 유지된다. footprint 테스트 수정은 필요 없으며, **구조적 중복 제거 완료로 판정하면 안 된다**. 본 B 제거안과 이 임시 관찰안을 구분해 승인해야 한다.

원본 Prefab을 전역 수정하지 않고 **두 Scene의 instance override만**으로 금지 타입을 없애는 최종 구조는 가능하다. 그러나 Production의 나머지 Debug를 전부 제거하기 위한 정식 Config/Registry/초기 장착 연결은 별도 Cutover 범위다. V1 Config나 후보 활성화로 이 경계를 조용히 넘지 않는다.

## Serialized Reference·GUID 위험

- source BP의 local ID 참조 검색은 원본 GO의 `m_Component` 항목만 발견했다. source BP에 대한 외부 `{fileID, prefab GUID}` 참조는 `.unity/.prefab/.asset` 검색에서 없었다.
- Added Hand/Clash/BP의 local ID를 참조한 항목은 해당 Instance의 `m_AddedComponents[].addedObject`뿐이다. 다른 컴포넌트 필드/UnityEvent의 incoming object reference는 발견하지 못했다.
- 해당 Debug 메서드를 향한 persistent UnityEvent `m_MethodName`도 발견하지 못했다. 구독은 위 C# lifecycle 경로다. 따라서 현재 검색상 Inspector 재연결이 필요한 incoming 참조는 없으나, 제거 후 Unity 재로드/SerializedObject 검사를 대체하지는 않는다.
- 원본 Prefab의 0이 아닌 local object reference는 모두 원본 문서 ID로 해석된다. Director.cameraController/RewardBinder.rewardPanel의 원본 null은 Production Scene property override에서 실제 연결되므로 Missing Reference로 오판하지 않는다.
- Script GUID는 Assets뿐 아니라 설치된 PackageCache까지 해석했다. Production Scene/Template/CombatRuntime/ProductionDungeonUI에서 미해결 script GUID=0, source Prefab GUID도 미해결0. UGUI/TMP/Input/URP 패키지 참조를 Missing Script로 세지 않았다.
- 원본 Prefab을 참조하는 **4개 Scene**: `Assets/GAME/Scenes/Dungeon_1_Production.unity`, `Assets/GAME/Scenes/Dungeon_Template.unity`, `Assets/GAME/Scenes/Testing_Dungeon_Template.unity`, `Assets/GAME/Tests/PlayMode/AnandAngel_IsolatedPlaytest.unity`. 전역 Prefab 수정은 이 네 Scene의 상속에 영향을 줄 수 있다.
- GUID/meta는 전부 보존한다. Scene 제거는 Unity Editor의 instance override로 수행하고 Apply All/Revert All, 수동 대량 YAML 편집, 컴포넌트 타입/SerializeField 이름 변경은 사용하지 않는다. local ID는 Scene 문맥과 함께 비교한다.

## 실제 실행 검증

Unity `6000.3.23f1`에서 기존 테스트만 실행했다. 새 테스트 코드/Editor helper를 만들지 않았다. 결과: **232 total / 226 PASS / 6 FAIL / 0 skipped**, Unity exit2. 이전 R1 결과와 실패 fullname 집합 차이는0이다. 저장소 전체 테스트를 실행한 결과는 아니다.

| 실제 실행 범위 | 결과 |
|---|---|
| 위 기존6개 | 6개 모두 개별 재현 |
| Production Missing Script 테스트 | Dungeon 1 / Dungeon_1_Production / Title 3case PASS |
| 기존 canonical owner 검사 | `Dungeon_HasExactlyOneCanonicalOwnerPerSystem` PASS(legacy Dungeon 대상); Production은 validator의 선행 owner counts 통과, Template EntryPoint1 통과 |
| Production UI 자산 Routing | `ProductionUIAssets_PassExplicitRoutingValidation` PASS |
| CombatUIRoutingTests | 58/58 PASS |
| CombatFinalExchangeProductionWiringTests | 22/22 PASS — 원본 BP fixture/Config/UI 참조 포함 |
| CombatProductionHandBpStartupTests | 40/40 PASS |
| CombatHandBpInitializationTests | 5/5 PASS |
| CombatBpValidationTests | 15/15 PASS |
| CombatHandValidationOptInTests | 6/6 PASS |
| CombatSkillIdentityValidationTests | 5/5 PASS |
| D4E-1C-R1 AnandNewGameSaveIntegrationTests | 15/15 PASS — 신규 지급·중복·재시작·Save/Load·실제 EnterPlayMode Snapshot 포함 |
| CharacterSkillRuntimeTests / SaveLoadPreparationTests | 11/11, 42/42 PASS |
| 기존 SourceAssets footprint / Debug 제거만으로 정식 초기화 안 됨 | 각각1건 PASS. 후자는 자산이 아닌 비영구 메모리 복제본에서 기존 fixture가 수행한 검사 |

Unity 로그의 script compilation stage는 약0.867초이고 이번 실행의 `error CS`/`warning CS` 진단은0이다. 현재 코드의 로드/증분 컴파일 및 테스트 실행 증거이며 새 강제 전체 재빌드를 뜻하지 않는다. 이전 보고서의 기존 경고4종을 이번에 새로 재현한 경고라고 쓰지 않는다. 원래 실패6건과 의도적 거부 경로의 로그를 포함하므로 Console 전체 Error/Warning0이라고 보고하지 않는다.

증거 파일은 저장소 밖 `C:/Users/eunio/.codex/visualizations/2026/10/31/01a190ef-97ac-70c3-86be-879fdbc0b8b9/D4E1DA-evidence/`의 `Tests.xml`, `Unity.log`, `Filter.txt`, `Before.json`이다. R1의 예전 PASS를 이번 결과로 복사하지 않았다.

### 미실행·증거 한계

- 사람의 실제 Production Inspector 열람/편집/재저장, Production Contact에서 BP callback 순서·enhancement·중복 구독 수 계측은 미실행.
- 보호 자산 제거 후 여섯 gate 재검증은 미실행(제거 금지이므로). 전체 Missing Object Reference 검사는 Production validator의 후반부에 있어 현재 forbidden gate가 먼저 차단한다. 정적 참조 검사/개별 wiring PASS로 후반부를 PASS 처리하지 않는다.
- Template Missing Script는 이번 named MissingScript 테스트의 대상이 아니며, 위 GUID/직렬화 해석 범위로만 검증했다.
- 신규 별도 프로세스 저장/읽기, 사람의 수동 Play Mode, Player Build 및 전체 회귀는 미실행. 이번 15건의 자동 검사를 별도 프로세스 재시작 검증으로 승격하지 않는다.
- BP Duplicate 자체를 정밀 계측하는 새 테스트는 만들지 않았다. 허용 신규 파일이 보고서뿐이라는 요청을 준수했다.

## D4E-1D-B에 필요한 정확한 승인 범위

**권장 최소 B — 중복 BP만 정리:**

1. 파일 `Assets/GAME/Scenes/Dungeon_1_Production.unity` 하나에서 `Runtime/Combat/CombatRuntime`의 **Scene Added BP `1665839071176935766`만 제거**. 원본 BP와 Added Hand/Clash/Camera, registry/config, 초기 덱2/3, Core/SaveLoad/UI는 유지.
2. `Assets/GAME/Tests/Editor/CombatProductionCutoverRehearsalTests.cs`의 footprint 테스트에 한해 승인된 새 inventory를 검사하도록 갱신하는 **별도 기대값 수정 승인**. 현재 부정합 구조를 영구 요구하는 부분만 바꾸며 원래6개 canonical gate는 그대로 둔다. 이 승인이 없으면 B의 새 footprint 실패를 명시적으로 남겨야 한다.
3. 제거 후 해당 Scene만 저장/재로드. base BP1개, Added BP0개, incoming reference 없음, 동일 Core/Camera/UI/Encounter 연결, BP3/skill2 enhancement/Ultimate 및 종료복귀를 검증. source BP를 Production Owner로 선언하거나 초기 BP3을 정식 정책으로 승인하는 작업이 아니다.
4. 기존6개 gate는 이 최소 B로 해소되지 않으며 미해결로 유지한다. 수치 검수와 서술을 일치시킨다.

**선택적 별도 승인 — Template 단독 정리:** `Assets/GAME/Scenes/Dungeon_Template.unity` 같은 Instance에서 source BP만 Scene 제거 override로 제외. 원본 Prefab 유지, 기존 여섯 중 Template1건의 직접 수리 후보다. 실제 저장 후 재검증 전에는 PASS를 예측 결과로만 취급한다.

**현재 B에 자동 포함하면 안 되는 범위:** Production의 Hand/Clash/상속 BP 전체 제거, 원본 CombatRuntime.prefab 수정, Production SO 변경/활성화, Registry/CharacterStart/적/HP 연결, UI 폰트, Runtime 수정, 테스트 canonical 조건 완화. Production5건을 동작 보존하면서 해소하려면 정식 Hand/BP/Clash 입력 및 연결 검증과 별도 활성화/Cutover 승인이 선행돼야 한다.

### 승인 후 검증 절차 제안

- 시작 시 dirty/untracked 해시를 새 기준선으로 기록하고 Unity Scene Instance Inspector에서 source/Added를 확인한다.
- Overrides의 Added Component 표시와 `skillFixtures=[]`로 Added BP를 식별한다. 원본 BP는 skillFixtures가1개다. 타입의 첫 `GetComponent` 결과나 Inspector의 첫 BP라는 이유로 제거 대상을 고르지 않는다. 정확한 Added BP만 제거하고 Overrides에서 Added 항목 소멸, 원본 BP 유지, Camera 유지, GUID/meta 무변경을 확인한다. Apply All 금지.
- 저장→Scene 재로드 후 Missing Script/Missing Object Reference/UnityEvent와 Prefab source/instance 참조를 검사한다.
- 실제 Contact에서 BP가 예상대로 초기화되는지, skill2 enhancement/Ultimate와 UI 표시가 동일 세션을 쓰는지, 전투 종료/재전투/입력·카메라 복귀를 확인한다. subscriber 순서를 추측하지 않고 관찰 결과를 기록한다.
- 이번232건, 아난 초기 지급·SaveLoad 및 조우 회귀를 다시 실행한다. 새 실패와 기존6개 잔존 여부를 분리한다.

## 보존 및 최종 상태

최종 대조에서 **기존 파일 2,657개 SHA-256 전부 일치**, 누락0개다. 새 저장소 파일은 `CBT-R2-08-P2B3D4E1DA-BpDebugAudit.md` 하나뿐이며 `git diff --check` exit0이다. 기존 tracked 수정9개와 미추적 자료를 그대로 보존했다. branch/HEAD도 시작과 같다. 기존 구현/자산 수정·삭제·이동 및 공개 API 변경은 없다. Inspector 재연결은 이번에0건이다.

## Development tracking

- System: Combat Integration / BP Debug / Production Asset Validation.
- Requirement: CBT-R2-08-P2B3D-4E1D-A 이번 요청; 외부 tracking 변경 없음.
- Production owner: CombatEntryPoint/CombatSession/CombatFlowOrchestrator, Core GameFlow, UI Router/Binder 유지.
- Implementation: 감사 및 최소 변경안 작성 완료. BP 자산 수정은 **Not Implemented(이번 단계 의도된 범위)**.
- Compile: 현재 소스 Unity 로드/증분 컴파일 검증, C# 진단0. 전체 강제 재빌드 미실행.
- Serialized assets: source/instance/Added/property override 정적 검사 완료. 실제 수정 후 Inspector 검증은 Unverified.
- Play Mode: 이번 선택 테스트의 격리 자동 Play Mode PASS. Production BP 중복 실행 순서/사람의 Play Mode는 Unverified.
- Integration: 기존6개 직접 원인 재현 완료. 정리 후 Production 통합은 Unverified.
- Recommended tracking: **Audit Complete — Exact Asset Scope Identified; Asset Fix Approval Pending; Existing 6 Failures Remain**.
- Stale mapping: 초기 지급 부재라는 D4E-1A 과거 서술은 R1 코드/후보에서 이미 해소. Scene Added BP를 빈/자동 shadow 상태로 해석하는 것은 현재 값과 다름. 역사적 footprint 검사와 최종 Production clean 계약을 분리할 필요가 있음.
