# CBT-R2-08-P2B3D-4E1C0 — 잔여 Production Debug 제거 준비

## 결론과 실행 가능 여부

**준비 감사 완료. 보호 자산·Runtime·테스트는 변경하지 않았다.** 추가 파일은 본 보고서뿐이다. D4E-1D-A/B의 조사와 실행 결과를 재사용하고 변경 후 상태 및 놓치기 쉬운 의존성만 대조했다.

- 현재 Production5건의 첫 실패 대상은 **상속 CombatBpValidationOptIn**이다. “Hand Debug 금지5건”은 이 테스트 묶음의 과거 설명이며, 현재 첫 검출 타입으로 쓰면 부정확하다. Hand/Clash도 여전히 같은 금지 조건에 해당한다.
- 최종 정리 대상은 **Production Instance의 BP/Hand/Clash 3개 + Template Instance의 상속 BP 1개**다. 이미 제거된 Added BP `1665839071176935766`는 대상에서 제외한다.
- **CombatRuntime.prefab 원본 수정은 필수가 아니다.** 두 Scene의 instance 제거 override만으로 잔여 Debug를 제외할 수 있다. 원본과 검증용 Debug 클래스/fixture는 보존하는 안을 권장한다.
- 하지만 현재 Production은 이 Debug가 초기화 데이터를 공급한다. 현재 Config 그대로 전부 제거하면 Hand/BP/Clash series가 자동으로 생기지 않는다. 컴파일이나 자산 gate 통과만으로 기능 보존을 인정할 수 없다.
- **D4E-1D-C의 전체 Production 제거 실행은 현재 조건부 보류**다. 정식 초기화 입력·장착 Snapshot·기존 Save 전투 호환 정책과 연결/활성화 승인이 필요하다. 제거 API나 기존 Runtime 소유권 자체의 부족이 원인은 아니다. Template1개만의 분리된 구조 정리는 별도 승인 후 검증 가능한 작은 단계다.

## 지침·Git·증거 범위

- 루트 `AGENTS.md`를 읽었고 적용 중첩 지침은 없다.
- branch `main`, HEAD `b42544824838e7de0e70b71afc2ca21a59db8192`.
- 시작 시 tracked 수정11개와 기존 미추적 Character/Skill/Config 후보, 테스트·격리 자산·선행 보고서를 기준선에 포함했다. D4E-1D-B의 Scene24줄 삭제와 footprint 검증 보강도 기존 사용자 변경으로 보존했다.
- 필수 자료: 저장소의 D4E-1D-A 보고서와 저장소 밖 `D4E1DB-evidence/CompletionReport.md`, `Regression.xml`, 현재 Scene/Prefab/SO 및 구현/호출자.
- 이번에 Unity/컴파일/테스트를 새로 실행하지 않았다. 아래 PASS 수치는 모두 **선행 B 실행 증거**이며 이번 새 PASS가 아니다. 현재 작업은 파일 읽기·참조 검색·XML 대조·해시 검사다.
- 기준 해시는 저장소 밖 `D4E1C0-evidence/Before.json`에 기록했다. 최종 보존 결과는 끝에 기재한다. commit/push/branch/PR 없음.

## 잔여 실패6건: 각각의 직접 원인

현재 기준 XML은 D4E-1D-B의 `Regression.xml`이다. 총1,041건/1,034 PASS/6 FAIL/1 IGNORED이며 이번에 재실행하지 않았다.

다음 첫5개는 `Game.Tests.Integration.DungeonOneProductionSceneTests`의 메서드다. 각각의 실제 실패 메시지를 XML에서 확인했다. 모두 `Assert.DoesNotThrow(DungeonOneProductionMigrationUtility.ValidateProductionScene)`에서 중단한다.

| 테스트 | 현재 실제 실패 / 기대값 | 직접 대상과 최종 제거 요구 |
|---|---|---|
| ProductionDungeonOne_HasCanonicalCombatRewardUiCameraAndBootstrapOwners, line338 | Forbidden…CombatBpValidationOptIn on CombatRuntime / 예외 없음 | Production 상속 BP가 먼저 검출됨. Hand/Clash까지 모두 제외해야 Debug 부재 조건을 충족. UI/Camera 자체 실패로 오판하지 않음 |
| ProductionDungeonOne_HasExplicitInteractionOwnersWithoutLegacyInteractionPaths, line431 | 같은 BP 금지 예외 / 예외 없음 | 동일 상속 BP. 뒤 Interaction Owner 검사는 미도달 |
| ProductionDungeonOne_HasOneCanonicalRepeatableNpcInteraction, line399 | 같은 BP 금지 예외 / 예외 없음 | 동일 상속 BP. NPC/SO/반복 상호작용 수정을 요구하는 실패가 아님 |
| ProductionDungeonOne_HasQuestDerivedCompletionContractWithoutLegacyExitOrFallbackDestination, line166 | 같은 BP 금지 예외 / 예외 없음 | 동일 상속 BP. Quest/목적지 검사는 미도달 |
| ProductionDungeonOne_IsCleanEnvironmentMigration, line61 | 같은 BP 금지 예외 / 예외 없음 | 동일 상속 BP. 이후 환경/참조 검사의 새 실패가 없다는 보장은 아직 없음 |
| Game.Tests.Integration.ProductionSceneWiringTests.DungeonTemplate_IsCanonicalSingleOwnerProductionSource, block253–259 | components.Any(Debugging/Legacy/구형 type)=true / false | Template의 상속 BP1개. 중복 BP나 Hand Debug가 없어도 실패 |

Production validator는 namespace `.Debugging`/`.Legacy` 및 Demo/Test 타입을 찾고, Template 검사도 타입/namespace를 검색한다. Enabled=false 또는 enableValidation=false로는 해결되지 않는다. 검사 삭제·whitelist·기대 조건 완화를 계획하지 않는다.

## 정확한 잔여 컴포넌트와 상속

공통 source Prefab: `Assets/GAME/Prefabs/CombatRuntime.prefab`, GUID `1bfc52cd235e809449d17d48571f5a38`, GO `CombatRuntime` source fileID `983610231012664106`.

| 제거 후보 | 파일 / GameObject | 식별자·현재 설정 | 승인 후 조작 |
|---|---|---|---|
| Hand Debug 1개 | `Assets/GAME/Scenes/Dungeon_1_Production.unity` / `Runtime/Combat/CombatRuntime` | **Scene Added** CombatHandValidationOptIn `1665839071176935759`; script GUID `bce1360c580248db935ccf2b6f845871`; Enabled/enableValidation=1. Player/Enemy fixture 각 Normal4/Clash6, capacities2/3, seeds2303/2304, Enemy Hand=true | 정확한 Added Component 제거. 이4/6 수량을 승인된 초기 덱2/3으로 승격하지 않음 |
| Clash Debug 1개 | 같은 Scene/GO | **Scene Added** CombatClashSeriesValidationOptIn `1665839071176935761`; script GUID `040a0e4f14ef4872ac50661f4ee830da`; Enabled/enableValidation=1, durability3/3, comparisons3, timeout5초 | 정식 Clash 설정/정책 연결 검증 후 Added Component 제거 |
| BP Debug 1개 | 같은 Scene/GO | **Prefab 상속** CombatBpValidationOptIn, source component `3049129679861795314`; script GUID `4bd31fad07c44e9795d18530d37a6855`; initialBP3, skill2 Ultimate 및 ClashPower fixture1 | **Scene instance 제거 override**로만 제외. source Prefab의 컴포넌트를 삭제하지 않음 |
| BP Debug 1개 | `Assets/GAME/Scenes/Dungeon_Template.unity` / `Runtime/Combat/CombatRuntime` | 동일 source BP 상속. Added BP/Hand/Clash 없음 | 이 Scene에만 source BP 제거 override 저장. 원본 Apply 금지 |

두 Scene의 해당 PrefabInstance ID는 `1665839071176935753`, stripped GO는 `1665839071176935756`다. local ID는 Scene 문맥으로 구별한다. 현재 source BP 제거 override는 없다.

유지 대상: Added Camera `1665839071176935757`, EntryPoint `1665839071176935754`, Orchestrator `1665839071176935755`, Director/WorldLifecycle/FieldLock/RewardBinder 및 UI Router/Binder. 기존 Encounter ID·Contact 적 선공·HP·스킬 identity·초기 지급/Save 계약은 이 정리의 변경 대상으로 삼지 않는다.

D4E-1D-B의 실제 Editor 관찰을 우선한다. 변경 전 YAML상 source+Added BP가 있었어도 live count는 Added1개였으며, 변경 후는 **source에서 상속한 BP1개**였다. 두 callback이 실제로 동시에 실행됐다는 추론은 재사용하지 않는다.

## A / B / C 분류

### A — Production에서 최종 제거할 대상

위4개 Scene 인스턴스가 최종 Debug 부재 조건의 최소 대상이다. 이 객체를 가리키는 incoming serialized reference는 현재 검색에서 발견되지 않아 물리적인 참조 삭제 범위는 좁다. 그러나 **Production3개를 지금 제거해도 동작이 안전하다고 확정한 것은 아니다.** 아래 B의 데이터/테스트 의존성이 먼저 충족돼야 한다.

Template 상속 BP 단독 제거는 다른 Scene에 전파되지 않는 가장 작은 독립 후보다. 실제 Template 전투/검증 회귀 후 안전 여부를 확정해야 하며, 이 보고서에서 이미 PASS한 변경으로 취급하지 않는다.

### B — 제거 전 분리·준비가 필요한 의존성

1. **현재 초기화 데이터 공급 경로.** Clash Debug가 EntryPoint.OnCombatStarted를 구독해 series 검증 초기화를 호출하고, 그 Orchestrator 이벤트에 Hand/BP가 구독한다. Hand는 카드 인스턴스를 만들고 BP는 잔액·강화·Ultimate를 공급한다. 단순 표시용 Debug가 아니다.
2. **현재 연결 Config.** `FinalCombatRuntimeConfig_V1.asset`(GUID `55e6978e3cf88ca4994f7855f907bc6c`)에는 새 Hand/BP 필드가 없어서 enableProductionHandBp=false, handCardRules=[], capacities0/0, BP 및 Clash 설정0 기본값이다. Debug를 제거해도 이 값이 자동으로 채워지지 않는다.
3. **Registry/장착 Snapshot.** Production Scene의 EntryPoint.skillDefinitions override는 기존1/2/11/12다. 정식 경로는 유일한 Character ID, 등록된 SoSkill 장착 Snapshot, Hand 대상의 모든 스킬에 대한 명시적 rule을 요구한다. 기존2/3 후보는10001/10002만 담는다. 현재 registry나 fallback 스킬1/2에 그대로 적용하면 입력이 일치하지 않는다.
4. **New Game 설정 연결.** Title의 `Systems` GO에 있는 `RuntimeBootstrapper.characterStartDefinition`은 미직렬화(null), Dungeon의 `Runtime` GO에 있는 같은 필드는 null이다. 승인된 `CharacterStart_AnandSkills_PlaytestV1_Candidate.asset`은 존재하지만 Production 연결이 없다. 설정 연결과 New Game 지급/Load 복원은 구별해야 한다. Bootstrap은 스킬을 지급하지 않으므로 Dungeon 직접 Play 진입도 별도 검증 대상이다.
5. **기존 Save의 전투 호환.** 새 Start 후보의 definitions는 아난만 포함한다. `hero.test` 저장의 ID/스킬을 보존하는 것과, 새 정식 Config에서 그 저장으로 전투 가능한 것은 다른 검증이다. 정의 없는 저장 캐릭터는 Progression의 미해결 상태로 보존되고, configured Party 전투 adapter는 authored definition을 요구한다. 기존 `ColdDungeonConfiguration_WithLegacyDefinition_RestoresTestHeroCombatIdentity`는 legacy definition을 명시적으로 포함한 fixture로 이를 확인한다. 이 테스트의 Test 데이터 자산을 Production에 등록하라는 뜻은 아니다. 정식 legacy 지원 정책/자산을 먼저 확정해야 한다. 아난 후보 연결만으로 이 호환이 해결됐다고 가정하지 않는다. hero.test 변환·아난 스킬 소급 지급·Save 초기화로 해결하지 않는다.
6. **사용자 장착/Film/Unique.** 정식 Hand는 실제 장착·자동 해금 Unique까지 포함한 스킬마다 rule을 요구한다. Film/Unique/빈 장착 fallback/과거 스킬을 포함하는 저장의 registry 및 rule 정책이 필요하다. 초기 두 스킬 테스트 성공만으로 모든 저장 덱을 승인하지 않는다.
7. **BP 콘텐츠 차이.** 기존 BP Debug는 initialBP3/skill2 강화·Ultimate를 준다. 정식 Runtime의 기본 잔액은0이다. 아난 후보의 ultimateSkills와 enemyHandSourceKeys는 빈 목록이고, 두 Skill SO의 bpEnhancementKind/perPoint는 None/0이다. 정식 flag만 켜면 현재 skill2 강화·Ultimate나 Enemy Hand가 자동 이전되지 않는다. 이를 명시적으로 보류할지 정식 데이터를 작성할지 승인해야 한다. BP 강화/Ultimate/적 Hand 정책을 이번에 임의로 정하지 않았다.
8. **footprint 검사.** `CombatProductionCutoverRehearsalTests.SourceAssets_ReadOnlyFootprint_ReportsDataAndInstanceOverrides`는 B 이후의 상속 BP1개와 Added Hand/Clash 존재를 요구한다. 다음 승인 구성에서 BP/Hand/Clash0개, Camera1개, 동일 Core/source와 정식 입력을 검사하도록 갱신해야 한다. 여섯 canonical gate는 그대로 유지한다.
9. **숨은 실제 Scene 테스트 의존성.** `FilmUniqueProductionPlayModeTests.ConfigureSceneInstances()`는 Production의 Hand Debug를 GetComponent하고 NotNull을 확인한 뒤 playerCardSource/normalHandCapacity를 바꾼다. Hand 제거 시 이 fixture가 먼저 실패한다. 정식 Config의 **메모리 복제본**과 해당 Film/Unique snapshot용 명시적 규칙으로 이전해야 한다. Production에 Debug를 재삽입하거나 테스트를 삭제하는 해결은 제외한다. 이 클래스의 실제 시나리오 `ProductionScene_ContactAcquire_UIEquip_FieldInputSkill_ColdSaveLoad`는 B의1,041건 XML에 **0건 포함**이다. 기존 수치가 이 의존성까지 커버했다고 주장하지 않는다.

### C — Demo/Test/호환 검증용으로 보존

- `Assets/GAME/Scripts/Combat/Debugging/CombatHandValidationOptIn.cs`, `CombatClashSeriesValidationOptIn.cs`, `CombatBpValidationOptIn.cs`와 meta/API는 유지한다.
- 원본 CombatRuntime.prefab의 BP는 재사용되는 역사적 검증 fixture로 유지하는 안이다. Production Scene에서는 override로 제외하므로 Production 필수 Owner로 남기는 것이 아니다.
- `CombatHandValidationOptInTests`, `CombatProductionHandBpStartupTests`의 transient Debug fixture/정식 초기화 guard 검사, CutoverRehearsal의 메모리 복제본 검사를 보존한다. synthetic 제거 개수를 실제 변경된 Production asset 개수로 바꾸지 않는다.
- `CombatFinalExchangeProductionWiringTests.CombatRuntimePrefab_EnablesBpValidationWithApprovedVerificationValues`는 원본 BP 존재를 요구한다. 원본을 유지하면 이 계약을 바꿀 필요가 없다.
- `AnandAngelPlaytestSetup`은 격리 Scene에서 Config를 메모리 복제·활성화하고 스킬을 주입하는 Editor fixture다. Production 초기화 대체물로 복사하지 않는다.
- 감사 도구의 watched type 문자열은 inventory 보고용이며 필수 Runtime 참조가 아니다. Debug0개 결과를 보고할 수 있으므로 삭제할 이유가 없다.

## 정식 Runtime 경로가 이미 가능한가

가능하다. `CombatEntryPoint.InitializeProductionHandBp`가 Session 생성 시점에 모든 참여자 입력을 검증하고 Hand/BP/Ultimate/Clash 설정을 구성한다. 기존 코드와 B의 메모리 복제본 검사에 Debug 없는 실행 증거가 있다. 새 Manager나 BP 계산 Runtime 변경이 필요하다는 증거는 없다.

반면 **현재 Production 자산 그대로 Debug만 제거해서 같은 기능을 유지할 수는 없다.** 기존 `RemovingDebugAlone_WithOriginalDisabledConfig_DoesNotEnableApprovedMode`는 원래 비활성 Config를 둔 복제본에서 HasHandValidation=false, BpRuntime=null, series validation=false를 확인한다. 이 테스트의 PASS는 정식 모드가 활성화됐다는 PASS가 아니다.

Production Scripts에서 세 OptIn 타입을 직접 요구하는 참조는 해당 Debug 파일 밖에서 찾지 못했다. Core의 `TryEnable…Validation`이라는 역사적 API 이름만 보고 Runtime을 Debug namespace로 옮기거나 공개 API를 바꾸지 않는다. UI Binder는 Session의 Hand/BP/Ultimate 및 기존 command controller를 사용하고 Debug 인스턴스를 SerializeField로 받지 않는다. UI가 깨지는 주된 위험은 Missing Reference보다 **공급 데이터가 사라지는 것**이다.

## 원본 Prefab 변경과 Scene Override 비교

| 방법 | 실제 영향 | 판단 |
|---|---|---|
| 원본 BP 삭제 | 네 소비 Scene 상속, 원본 BP 검증, 복제본 fixture 및 기존 제거 개수에 파급. 그래도 Production Added Hand/Clash는 별도 정리 필요 | 최소안 아님. 이번 후속 승인안에서 제외 권장 |
| Production/Template에서만 source BP 제거 override | 두 Instance에서만 BP 제외. 원본/다른 Scene/Debug 클래스 유지. Production Hand/Clash는 Added 제거 | **권장 최소 구조안**. m_RemovedComponents를 Unity Editor API로 작성하고 source에 Apply하지 않음 |
| Enabled=false/enableValidation=false | 타입이 남아 여섯 gate를 해결하지 못함 | 사용하지 않음 |

원본 Prefab 참조 Scene 전부: `Assets/GAME/Scenes/Dungeon_1_Production.unity`, `Assets/GAME/Scenes/Dungeon_Template.unity`, `Assets/GAME/Scenes/Testing_Dungeon_Template.unity`, `Assets/GAME/Tests/PlayMode/AnandAngel_IsolatedPlaytest.unity`. 다른 Prefab/asset의 추가 source 소비는 현재 검색에서 없다. source 변경을 피하면 뒤 두 검증 Scene에 제거 override가 전파되지 않는다. Template에서 앞으로 새 격리 Scene을 만드는 builder의 복사 결과는 달라질 수 있으므로 별도로 확인한다.

## 예상 변경 범위표 — 다음 승인 단계용

아래는 제안이며 이번에 수정한 파일 목록이 아니다.

| 파일 | 정확한 GO/Component/필드 또는 참조 | 최소 조치 / 검증 |
|---|---|---|
| `Assets/GAME/Scenes/Dungeon_1_Production.unity` | `Runtime/Combat/CombatRuntime`; Added Hand `...5759`, Clash `...5761`, source BP `3049129679861795314` | 준비 완료 후 Added2개 제거+상속 BP 제거 override. Camera `...5757` 및 EntryPoint/Orchestrator/Director/WorldLifecycle/FieldLock/Reward 참조 유지. Debug0/Owner1/Serialized refs 검사 |
| 같은 Production Scene | `Runtime/Combat/CombatRuntime`의 `CombatEntryPoint.skillDefinitions`, `finalExchangeRuntimeConfig`; `Runtime` GO의 `RuntimeBootstrapper.characterStartDefinition` | 동작 보존을 위해 승인된 registry/profile/start 연결 필요. 기존 IDs는 호환 검증 없이 제거하지 않음. Profile/Party/equip 일치·Contact·Load 검증 |
| `Assets/GAME/Scenes/Dungeon_Template.unity` | `Runtime/Combat/CombatRuntime`의 같은 source BP | Scene별 제거 override만. Camera/조우·퀘스트 ID/기존 선공 정책 보존. Template gate 및 자동/수동 전투 검사 |
| `Assets/GAME/Tests/Editor/CombatProductionCutoverRehearsalTests.cs` | 기존 footprint 메서드 | 승인된 새 inventory와 Debug 없는 정식 초기화/참조를 검증. 기존 canonical6개 조건은 유지 |
| `Assets/GAME/Tests/Editor/FilmUniqueProductionPlayModeTests.cs` | `ConfigureSceneInstances()`의 Hand Debug GetComponent/fixture 설정 | 실제 Production Scene 테스트의 transient 정식 Config/rule 설정으로 이전. Film/Unique/UI/Input/저장/보상 검사를 유지 |
| `Assets/GAME/Scenes/TitleScene.unity` | `Systems` GO의 `RuntimeBootstrapper.characterStartDefinition` | 아난 경로를 선택할 경우 Dungeon과 동일한 승인 Start 설정. Title New Game/Continue 경계 확인 |
| 새 정식 Runtime Config SO + meta, 경로는 승인 시 확정 | 예: `Assets/GAME/Data/Combat/Runtime/FinalCombatRuntimeConfig_AnandStart_PlaytestV1_Production.asset`(제안, 아직 없음) | 기존 후보/V1을 덮지 않고 Unity 새 GUID. Hand2/3, MP2 및 승인 BP/Clash·Enemy/강화/Ultimate/호환 rule 명세 필요. 작성/연결과 활성화 승인은 구분 |
| 필요 시 별도 Start 호환 설정 또는 Skill/Enemy 데이터 | 위 저장/콘텐츠 정책에서 확정되는 정확한 정의 | **현재 승인 범위에 자동 포함하지 않음.** 정책 결정 후 별도 파일을 특정해야 함. 기존 persistent ID·Save schema 변경 없이 가능한 데이터 연결을 우선 |

구조적 제거만의 기존 파일 최소 범위는 **두 Scene + 두 테스트 파일**이다. 그러나 이를 동작 보존까지 완료하는 전체 범위로 제시하지 않는다. 아난 정식 경로를 쓰면 Title/Production 데이터 연결 및 새 profile 작성/활성화 승인도 필요하고, legacy/Film/Unique/BP/Enemy 정책에 따라 데이터 범위가 추가 확정돼야 한다. 현재 불명확한 콘텐츠를 임의의 수치·새 Runtime 분기로 숨기지 않는다.

수정 불필요/보존 권장: CombatRuntime.prefab 원본, ProductionDungeonUI.prefab, Runtime 코드, 기존 Skill/Character 정의 및 후보/V1 SO, Input/TMP/패키지. HP320·천사 교체·미승인 능력치·폰트·기타 로드맵 항목은 이 Debug 정리 최소안에 포함하지 않는다.

## 직렬화·이벤트·다른 Scene 영향

- 현재 Hand/Clash local ID 참조는 자기 Instance의 m_AddedComponents 목록뿐이다. source BP local ID는 원본 m_Component 목록에 있고, 외부 `{source BP fileID, prefab GUID}` incoming 참조는 없다. 제거 대상에 연결된 persistent UnityEvent도 기존 감사/이번 검색에서 발견되지 않았다.
- Hand/BP의 Orchestrator 이벤트 구독과 Clash의 EntryPoint 구독은 C# OnEnable/OnDisable 경로다. 제거 시 다른 Debug로 참조를 재연결하지 않고 기존 수명주기 해제를 확인한다.
- 위 결과는 정적 참조 검사다. 실제 제거 후 Editor 재로드, PrefabUtility의 source/Added/Removed 확인, Missing Script/ObjectReference/UnityEvent 검사를 다시 통과해야 한다.
- B에서 검증한 Scene GUID `50985daa858d218419e02a946bd86287`, Template GUID `c678625b599246741b41613b007285ac`, 원본/스크립트 GUID/meta 보존. Scene override와 source asset 삭제를 혼동하지 않는다. Apply All/Revert All 및 대량 YAML 편집 제외.

## 승인 순서와 D4E-1D-C 통과 기준

### 승인 패키지

1. **의존성·데이터 준비:** 테스트2파일의 정식 fixture 이전, 승인된 Config/Registry/Start/legacy 및 BP·Enemy 정책을 확정한다. 비활성 데이터 작성/격리 리허설과 실제 Production 연결·활성화 권한을 분리해 명시한다.
2. **실제 제거:** 정확한 Production3개/Template1개에 대한 두 Scene 편집 승인. 원본 Prefab 유지, Debug 소스/테스트 fixture 보존, Runtime·identity·Save 계약 무변경을 조건으로 둔다.
3. **기능 전환:** 정식 초기화 실제 연결/활성화는 별도 명시적 승인 후에만 수행한다. 이를 승인하지 않는다면 현재 Production 동작을 유지하면서 전부 제거하는 작업은 보류한다. Template 단독 정리는 분리 승인 가능하다.

### 검증 계획

- Editor: 승인 대상만 제거→저장→재로드. Production/Template의 BP/Hand/Clash0개, Camera/EntryPoint/Orchestrator/Director/WorldLifecycle/FieldLock/Router/Binder 단일 소유와 기존 참조를 확인. Missing Script/Reference/UnityEvent0, source/다른 두 Scene/기존 GUID·meta 보존.
- 정식 Snapshot: Debug 없이 HasProductionHandBpInitialization=true. 아난 보유·장착2개/Normal2·Clash3/초기MP2, 독립 카드 소비·MP·Discard/재순환·새 전투·BP/Clash 정책 확인. 단순 세션 시작 성공은 통과 기준이 아님.
- 상태·저장: New Game 최초/중복/명시 재시작, 장착 변경 저장복원, Load 무지급, hero.test/Legacy/Film/Unique 저장을 실제 새 profile에서 Load→전투까지 확인. Dungeon 직접 진입 정책도 별도 확인. identity 변환/Save 초기화 금지.
- UI/World: 실제 Contact/FieldAttack, BP 투자/강화·Ultimate·적 Hand의 승인/보류 상태, 동적 버튼·MP 표시, Abort/Victory/Defeat/Reward 닫기/입력·카메라 복귀 및 재조우를 확인.
- 자동: 기존6개 각각 재실행하여 **6/6 PASS**를 목표로 하되 뒤에 가려진 새 실패가 나타나면 개별 분석한다. 갱신 footprint, **FilmUniqueProductionPlayModeTests의 실제 Scene 시나리오를 추가**해 누락된 의존성을 검증한다. B의1,041건 선택 범위, NewGame15, Encounter111, UI58, BP15, Hand/Clash/MP, Character/Save 회귀를 재실행한다.
- Source fixture: 원본 BP 존재 검사와 Debug 단위/메모리 복제본 검사를 유지한다. tests를 삭제하거나 final gate를 완화해6건을 없애지 않는다.
- 구분: EditMode/자동 EnterPlayMode/사람의 수동 Play Mode/실제 별도 프로세스 디스크 재시작/Player Build를 따로 기록한다. Console 경고와 C# 오류도 분리한다.

## 이번 실제 검증과 보존

새 컴파일·Unity 실행·테스트는 **미실행**이다. B 결과1,034 PASS/6 FAIL/1 IGNORED 및 컴파일 오류·경고0은 이전 실행 기록으로만 인용한다. 이번에 XML의6개 실제 검출 대상, Film 실제 Scene 테스트가 B 선택 범위에 없었다는 사실, 현재 구조·데이터/호출자·정적 참조를 확인했다. 이번 `git diff --check`는 exit0이고 새 보고서의 trailing whitespace도 없다.

최종 저장소 변경은 본 보고서1개 추가뿐이다. **기존 파일2,658개 SHA-256 전부 일치**, 누락0개이며 기존 파일 수정/삭제/이동, Inspector 재연결, 공개 API/Save schema/Runtime 변경이 없다. 기존 tracked 수정11개와 미추적 자료도 그대로 보존됐다. branch/HEAD는 시작과 같다. 외부 `D4E1C0-evidence/Preservation.json`에 대조 결과를 기록했다.

## Development tracking

- System: Production Combat Debug Dependency / Asset Cutover Preparation.
- Requirement: 이번 CBT-R2-08-P2B3D-4E1C0 요청.
- Production owner: 기존 CombatEntryPoint/CombatSession/CombatFlowOrchestrator, RuntimeBootstrapper/SaveLoad/CharacterSkill Owner, UI Router/Binder 유지.
- Implementation: 감사·제거 대상·검증 계획 작성 완료. 실제 자산 제거는 Not Implemented(이번 단계 금지).
- Compile / Inspector / Play Mode / Integration: 이번 새 실행 없음. 기존 B 증거와 현재 파일의 정적 대조만 수행.
- Recommended tracking: **Preparation Complete — Scene Override Scope Identified; Production Execution Pending Data/Compatibility/Test Separation and Explicit Approval**.
- Remaining: BP/Enemy/장착·legacy 데이터 정책, 실제 Scene 테스트 fixture 이전, 정식 profile 연결/활성화 승인 및 수동·전체 통합 검증.
- Stale mapping: “Hand Debug 금지5건”의 현재 첫 검출은 상속 BP다. A의 serialized BP2개 동시 실행 추론 대신 B의 actual Added1→inherited1 관찰을 사용한다. B의1,041건은 FilmUniqueProductionPlayModeTests 시나리오를 포함하지 않는다.
