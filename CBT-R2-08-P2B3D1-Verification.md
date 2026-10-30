# CBT-R2-08-P2B3D-1 — 승인 설정 반영 및 Production 전환 준비

2026-10-30 KST. **Clash3/3·최대3회·선택5초를 Playtest V1 승인값으로 기록·검증했다.** 기존 테스트 SO의 값이 이미 일치하므로 숫자는 다시 저장하지 않고 승인 라벨만 추가했다. 보호 Production 자산 편집·활성화·Debug 제거·Cutover 없음.

## 1. 지침·Git·범위

- 적용 지침: 루트 `AGENTS.md`; 중첩 지침 없음. Branch `main`, HEAD `6fa26d294472574097d33b884858ba0ee8bdc8b7`. Unity 6000.3.23f1 유지.
- P2B3A/B/C 보고서·실제 SO와 P2B2D 초기화 코드, Session/Bootstrapper/Orchestrator/StateMachine, Hand/BP/Ultimate, Debug opt-in, UI Binder/Controller, Scene/Prefab/source GUID/instance override를 조사했다.
- 편집 전 상태 및 SHA-256 기록: `Logs/CBT-R2-08-P2B3D1-BeforeStatus.txt`, `...-BeforeHashes.json`. 기존 Quest/Reward/Save 및 Unity 자산·코드의 dirty/untracked 변경 보존.
- Runtime은 승인값을 이미 지원한다. Runtime/공개 API/SerializeField/enum/패키지 변경 없이, 기존 테스트 SO 승인 annotation·새 집중 검사·전환 계획으로 범위를 제한했다.
- 이전 선택 기준선은 P2B3C **780 PASS / 6 FAIL / 1 IGNORED**, 총787. 이전 전체 **1,562/8/1**은 과거 전체 결과이며 이번 실행과 구분한다.

## 2. 승인 Config 반영 결과

재사용 SO: `Assets/GAME/Tests/Editor/Fixtures/CombatPlaytestV1/FinalCombatRuntimeConfig_ClashPlaytestV1.asset`.
GUID `77fff0b326a327943b8f564b26f03718` 유지. 파일 바이트와 수치·참조는 동일하다. Unity AssetDatabase에서 meta labels에 **ApprovedPlaytestV1Numbers**만 추가했다. 기존 TestOnly / PlaytestV1 / ClashFixture / ContentPending 라벨도 보존했다. 수치 승인은 콘텐츠 완비나 Production 연결/활성화 승인을 뜻하지 않는다.

| Config/SO | 실제 적용 상태 |
|---|---|
| 기존 Production `Assets/GAME/Data/Combat/Runtime/FinalCombatRuntimeConfig_V1.asset` | 무변경. 새 초기화 flag 미작성=false, Hand/BP/Clash 작성 데이터 미완성 |
| B3A `FinalCombatRuntimeConfig_PlaytestV1.asset` | 무변경. 일반2/합3·BP3/2/3, Clash0/0/0/0·selector Unspecified, enable=false |
| B3B 재사용 `FinalCombatRuntimeConfig_ClashPlaytestV1.asset` | 일반2/합3·BP3/2/3, Clash3/3/3/5, enable=false 유지. 수치 승인 라벨 추가 |
| 테스트 실행용 clone | 기존 Owner로만 실행하며 테스트 콘텐츠/부족 자원 조건을 명시 공급. 저장 Asset을 활성화하지 않음 |

승인값의 정확한 계약:

- `clashAttackerDurability=3`, `clashResponderDurability=3`, `clashMaxComparisons=3`, `clashDecisionDurationSeconds=5`.
- `normalHandCapacity=2`, `clashHandCapacity=3`, `bpMaximum=3`, `bpRoundCyclesPerGain=2`, `bpMaximumInvestment=3`.
- 초기 BP0과 Ultimate 누적 승인 BP 소비1/2/3은 기존 Runtime 계약이며 새 SO 필드를 추가하지 않았다.
- 기존 selector 필드 LastCommittedWinningSkill(1) / FirstAvailableSkill(1)은 변경하지 않았다. 이번에 별도 새로운 선택 정책을 만들지 않았다.
- 기존 `initialMp=2`, `maxMp=3`, Standoff 회복1/초 유지. MP3을 시작 조건으로 추가하거나 일괄 지급하지 않았다. 명시적인 MP3/3회 검사는 해당 격리 사례의 사전조건일 뿐이다.
- `handCardRules`, `ultimateSkills`, `enemyHandSourceKeys`는 저장 테스트 SO에서 빈 배열이다. copies/seed·공식 Skill/Enemy/Character/효과/추가 MP·Ultimate/AllOut 콘텐츠는 계속 Pending이다.

재현 가능한 Editor 메서드 `CombatApprovedClashPlaytestV1Tests.MarkApprovedNumbers`는 승인 수치와 enable=false가 일치하는지 먼저 검사하고, 기존 라벨에 승인 라벨만 합친다. 값이 다르면 오류를 내며 임의 덮어쓰지 않는다. 반복 실행 시 중복 라벨·새 자산을 만들지 않는다.

## 3. 실제 초기화 호출 순서와 Production Owner

현재 코드의 정확한 경계:

1. `CombatEntryPoint.StartCombat`: 기존 중복 시작/GameState·참가자·Character ID 검사와 장착 snapshot/적 authored provenance 준비.
2. `CombatBootstrapper`: Session/roster/StateMachine 구성 및 combat state 준비.
3. Bootstrapper 사전 callback: roster 재검증 → 모든 Hand/BP/Ultimate/Clash 데이터 검증 → 대상별 Hand → BP → Ultimate mapping → Session의 Clash 설정 snapshot 및 Production 성공 표식.
4. Opening Effect → 첫 StateMachine Tick → Standoff.
5. EntryPoint ActiveSession 설정·World context·기존 GameFlowController 동기화.
6. `CombatFlowOrchestrator.BindSession`: Session의 Clash snapshot을 첫 pump 전에 설치한 후 첫 요청 발행. **Clash 설정은 Opening Effect 전 검증·snapshot되고, Orchestrator 설치는 Opening Effect 뒤/첫 명령 전**이다.
7. 기존 Director/Enemy startup bind → OnCombatStarted의 UI/Input 등 기존 listener → Enemy 요청 소비 활성화.

성공 표식이 있는 Session에는 Hand/BP/Clash Debug opt-in이 값을 주입하지 않는다. 정식 초기화는 Debug 이벤트로 Runtime을 만들지 않는다. 실제 ClashSeries는 첫 공격/대응 승인 뒤 생성된다.

필수 데이터가 없으면 Opening Effect/첫 명령 전에 거부한다. 중간 예외는 기존 Session.AbortStartup 및 EntryPoint rollback으로 부분 Hand/BP/Ultimate/Clash와 바인딩·전역 흐름을 정리한다. 기존 P2B2D 실패 주입 및 Debug overwrite 검사를 재실행한다.

Owner 유지: CombatEntryPoint(시작/종료), Session(상태), StateMachine(phase/승인), Orchestrator(진행), 기존 SkillRunner/Outcome 경로(효과), Director(연출), GameFlow/UI/Input/Inventory/Reward/Save 각 기존 Owner. Contact/FieldAttack은 같은 요청/EntryPoint 계약을 사용하며 별도 실행기를 만들지 않았다. 실제 Scene 입력 연결은 아직 수동 검증 Pending이다.

## 4. Clash/자원/UI 검증 해석

3회는 최대 비교 수다. 위력 차이3은1회, 차이2는2회, 차이1은3회에 강인도3을 소진할 수 있다. 동점이면 양쪽−1, 양측0에서는 Speed→공격권 우선순위가 상한 Tie보다 먼저 평가된다. 양측이 남은 채 상한 도달하면 기존 Tie/Standoff다.

- 공격자가 다음 비용을 낼 수 없으면 승인 거부, 가용 공격0 상태의 Timeout 뒤 Tie 복귀.
- 대응 MP/카드 부족은 기존 거부/무대응 경로를 따른다. 모든 부족 MP를 공격 강행으로 바꾸지 않는다.
- Clash 중 MP 자동 회복을 추가하지 않았다. MP2에서 두 번 비용1 지출 후 Tick을 진행해도 MP0이다.
- 실제 FinalCombatUIBinder의 동적 Button/Text와 ViewState를 생성해 MP 부족의 OFF/선택 불가, 카드 부족의 pseudo-card 미생성/가용 스킬 거부/확인 불가를 검사했다. 실제 Dungeon HUD의 Inspector/시각 배치는 이 검사와 별개다.
- 카드 부족 사례는 용량3 유지·공급 카드1의 작은 덱을 clone에만 공급한다. 비용0으로 MP 원인을 제거해 카드 원인만 관찰했다. 정식 copies/MP 비용 승인이 아니다.
- 5초 결정 timer는 unscaled time이다. timeScale0.25에서 scaled0.25초/unscaled1초 Tick이면4초 남는다. GameFlow.Pause 중에는 멈추고 Resume 후 나머지 실제 시간으로 진행한다.
- 3라운드 중 별도3턴 Status는3을 유지하며, 완료 Exchange 경계에서2로 한 번만 감소한다. 중복 Outcome 완료로 다시 감소하지 않는다.

기존 Runtime 규칙과의 신규 충돌은 최종 검사에서 확인되지 않아 Runtime 수정이 필요하지 않았다.

## 5. 보호 Asset 전환 계획 — 이번 미실행

### 정확한 대상과 현재 참조

| 경로/Hierarchy | 현재 Component·식별 | 후속 변경 범위 |
|---|---|---|
| `Assets/GAME/Prefabs/CombatRuntime.prefab` / 루트 CombatRuntime | EntryPoint fileID8945921886167358035, `finalExchangeRuntimeConfig` → GUID55e6978e3cf88ca4994f7855f907bc6c. `skillDefinitions=[]` | 준비된 Production Config/registry 참조를 기존 Owner에 작성·검증 |
| 같은 prefab 루트 | **원본 BP Debug** fileID3049129679861795314, script GUID4bd31fad07c44e9795d18530d37a6855, enableValidation1·initialPlayerBp3 | 정식 초기화 검증 후 별도 승인 범위에서 serialized Debug component 분리. Debug 초기3을 정식 BP로 복사 금지 |
| `Assets/GAME/Scenes/Dungeon_1_Production.unity` / Runtime/Combat/CombatRuntime | prefab instance1665839071176935753, source GUID1bfc52cd235e809449d17d48571f5a38. 기존 registry4개 instance override | 승인된 실제 데이터와 snapshot 연결 확인. 레지스트리 전체 Apply/Revert 금지 |
| 같은 Dungeon instance | **추가 Hand Debug** fileID1665839071176935759, script GUIDbce1360c580248db935ccf2b6f845871. InspectorFixture source·카드4/6·용량2/3·seed2303/2304·Enemy Hand on | 기존 field/fixture 정책을 Config 작성 계약으로 대체한 뒤 added component만 분리 |
| 같은 Dungeon instance | **추가 Clash Debug** fileID1665839071176935761, script GUID040a0e4f14ef4872ac50661f4ee830da. 3/3/3/5 | 승인된 동일 수치를 준비된 Production Config로 작성한 뒤 added component 분리 |
| 같은 Dungeon instance | **추가 BP Debug** fileID1665839071176935766, BP script GUID동일, initialPlayerBp3 | prefab에서 상속되는 BP Debug와 이 Scene 추가 BP를 모두 구분해 분리 |
| `Assets/GAME/Scenes/Dungeon_Template.unity` / Runtime/Combat/CombatRuntime | 같은 prefab source/instance ID, 원본 BP Debug를 상속. added component는 Camera Controller | 원본 BP 분리의 전파와 Template Config/registry override를 확인. 카메라 유지 |
| `Assets/GAME/Data/Combat/Runtime/FinalCombatRuntimeConfig_V1.asset` | GUID55e6978e3cf88ca4994f7855f907bc6c, 기존 type/script GUID914e9a1f4d1248d982fb012634938a2a | 별도 승인 후 실제 제작 데이터만 작성. 이번 테스트 SO를 직접 연결하지 않음 |

Prefab GUID는 `1bfc52cd235e809449d17d48571f5a38`를 유지한다. Scene의 `m_RemovedComponents=[]`이며 Dungeon에는 위3개 Debug added component가 있다. 원본 BP와 Scene 추가 BP가 모두 serialized 경로에 남아 있으므로 하나만 처리했다고 완료로 보지 않는다. **추가 Camera Controller fileID1665839071176935757 및 그 참조는 보존 대상**이다.

세 Debug script GUID를 Scene/Prefab에서 검색한 직접 포함 자산은 위 prefab과 Dungeon Scene이다. Template은 prefab 상속으로 영향을 받는다. 코드의 Debug 클래스/검증 fixture/LegacyPlanning은 삭제 대상이 아니다.

### Production Config 준비 조건

다음 조건이 준비되어야 기존 Production Config를 안전하게 작성할 수 있다. 새로운 Config 시스템/CombatRuntime은 만들지 않는다.

| 구분 | 필수 작성/결정 |
|---|---|
| 승인 숫자 | 이번 Clash3/3/3/5 및 기존 Hand2/3·BP3/2/3. 초기 BP0/Ultimate 단계는 기존 Runtime |
| 참가자 | 공식 Character ID 및 장착 snapshot, 살아 있는 적의 authored skills, registry의 유일한 SkillId/persistentKey와 정확한 SO 참조 |
| Hand | 사용 actor의 단일 Normal/Clash rule·양수 copies·drawable 카드·seed 제작 결정. 동일 Skill 양쪽 배치 금지 |
| Enemy | Hand가 필요한 타입의 실제 Enemy Source Key와 snapshot 확보. 일반 적은 no-Hand 유지 |
| Clash 선택 | 기존 지원 selector를 명시해 초기화 요구를 충족. Debug fallback이나 0/Unspecified로 채우지 않음 |
| BP 강화 | 실제 Skill별 enhancement kind/perPoint. 테스트 계수1을 임의 정식 복사하지 않음 |
| Ultimate/AllOut | Runtime gauge와 실제 Skill/Effect/MP 콘텐츠는 별개. 빈 optional Ultimate mapping은 startup 코드상 허용되지만 기능 완성으로 표시하지 않음. 필요한 콘텐츠/명령 계약을 별도로 결정 |

테스트 SO는 Editor/Fixtures 경로에 있으므로 Production에 직접 연결하지 않는다. 승인된 숫자만 실제 Production 데이터 작성에 사용하고 임시 hero/skill/source key·copies·효과·MP 비용은 옮기지 않는다. 현재 Content/Contract Pending이 남아 있어 활성화하지 않는다.

### Inspector 변경·검증·복구 순서

1. **별도 자산 전환 승인과 필요한 데이터 준비**를 먼저 완료한다. Play Mode를 종료하고, 현재 dirty/untracked 상태를 포함한 Scene/Prefab/SO/meta 사본·해시·Inspector screenshot·UnityEvent/override 목록을 기록한다. HEAD 기준 복구는 하지 않는다.
2. 기존 Production Config를 승인된 데이터로 작성하되 우선 enable=false 유지. EntryPoint의 기존 Config/registry 및 참가자 snapshot/Enemy source·AI·UI/Input/World/Reward 참조를 확인한다.
3. 별도 격리 검증에서 동일 작성 데이터가 Debug 없이 초기화·효과·실패 rollback을 통과하는지 확인한다. 테스트 fixture 통과만으로 실제 Inspector 확인을 생략하지 않는다.
4. 원본 prefab에서 BP Debug를 분리하는 작업과 Dungeon의 added Hand/Clash/BP Debug를 분리하는 작업을 명시적으로 나눈다. base 원본 변경의 Template/Dungeon 전파를 확인한다. 카메라·Director·Orchestrator·EntryPoint 및 기존 override를 보존한다.
5. component 참조/UnityEvent PersistentCall/methodName을 재검색하고 Missing Script/Reference를 확인한다. 현재 조사에서 Debug를 향한 UnityEvent 호출은 확인하지 못했지만 작업 직전과 변경 후 다시 대조한다. SerializeField 이름/타입·GUID·fileID를 재사용/임의 변경하지 않는다.
6. 수치·콘텐츠·단일 Owner·Debug 분리·참조 검사가 모두 끝나고 활성화가 별도로 승인된 시점에만 production opt-in을 켠다. 초기화 거부를 UI 숨김/Debug 재주입으로 우회하지 않는다.
7. 기존6개 gate와 후속 검증을 재실행하고 실제 Contact/FieldAttack, 1~3인, MP2/3·카드 부족, 반복 합/동점/Timeout/Pause/Abort/인계/Ultimate/Terminal/Reward/EXP/입력·UI 복귀를 수동 검증한다. 05/06A 수동 보류는 별도 해제 지시가 있어야 한다.
8. 문제 시 변경 직전 사본에서 **승인된 component/field 변경만** 복구한다. prefab 원본과 Scene override를 함께 대조하고 중간 사용자 변경을 자동 덮어쓰지 않는다. meta/GUID 복원과 Config enable=false를 확인한다. 전체 Apply/Revert/Reset 금지.

### 기존 실패6건 해결 조건

공통 namespace `Game.Tests.Integration`:

1. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasCanonicalCombatRewardUiCameraAndBootstrapOwners`
2. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasExplicitInteractionOwnersWithoutLegacyInteractionPaths`
3. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasOneCanonicalRepeatableNpcInteraction`
4. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasQuestDerivedCompletionContractWithoutLegacyExitOrFallbackDestination`
5. `DungeonOneProductionSceneTests.ProductionDungeonOne_IsCleanEnvironmentMigration`
6. `ProductionSceneWiringTests.DungeonTemplate_IsCanonicalSingleOwnerProductionSource`

앞5개는 Dungeon Hand Debug 선행 gate, 마지막은 Template의 상속 BP Debug gate다. **enableValidation=false나 Component 비활성만으로는 serialized Debug 존재 검사를 해결하지 못한다.** 승인된 정식 초기화·데이터를 준비한 후 실제 Production serialized Debug 경로를 분리하고, gate를 넘은 뒤 NPC/Quest/UI/Reward 등 각 검사의 나머지 조건까지 통과해야 한다. 현재 후속 조건이 모두 검증됐다고 가정하지 않는다. 이번 상태는 **Existing Confirmed / Asset Change Required**다.

## 6. 테스트·변경 파일·검증 범위

수정: 위 기존 Clash 테스트 Config의 `.asset.meta` 승인 라벨1개. SO 숫자/파일/GUID는 그대로다.
추가: `Assets/GAME/Tests/Editor/CombatApprovedClashPlaytestV1Tests.cs` 및 Unity 생성 meta, 이 보고서. 무시되는 Logs에는 실행/해시/filter 기록을 남긴다. 삭제/이동/기존 Runtime 코드 수정 없음. 새 script GUID `7bc052c3e1b1541449b0a7da74f70b2c`. 기존 public Runtime API/SerializeField/Save schema 무변경.

새8개는 승인값/MP2 시작, 1~3회 조기/최대 종료, unscaled 슬로모션/Pause 시계, 실제 UI Binder의 MP/카드 부족 표시, 3턴 Status의 Exchange당1회 감소를 검증한다. B3A22/B3B15/B3C23/B2D40 및 Legacy/Item/Film/상태·Mental·Terminal/Reward/EXP 회귀를 함께 실행한다. 전체 회귀는 보호 자산 설치·저장 테스트 때문에 미실행한다.

실제 Production Inspector/수동 Play Mode/카메라·애니메이션·UI/Input 통합, Player Build, 디스크 재시작 Save 검증은 미실행이다. 격리 EnterPlayMode 검사가 실제 Dungeon 검증을 대체하지 않는다.

## Development tracking

- System: Combat / Approved Playtest V1 configuration and asset transition readiness.
- Requirement: CBT-R2-08-P2B3D-1 승인 Clash3/3/3/5 및 기존 Hand/BP/Ultimate.
- Owner: 기존 EntryPoint/Session/StateMachine/Orchestrator/SkillRunner/Director 유지.
- Implementation: 승인 annotation·재현 검사·정확한 자산 전환 계획 완료. Runtime 변경 없음.
- Compile/자동 검사: 아래 최종 기록. 실제 Production Inspector/Play Mode/Integration Pending.
- Recommended: **Approved Playtest V1 Config Verified — Production Content / Asset Migration / Manual Integration Pending**.
- Production 초기화 비활성, Debug/Legacy 보존, Cutover 미실행. 과거 C 보고서의 B 미승인 문구는 역사적 상태이며 현재4개 숫자는 Playtest V1 승인됨.

## 최종 실행 및 보존 기록

Unity 6000.3.23f1 최종 선택 회귀 **788 PASS / 6 FAIL / 1 IGNORED**, 총795. 기록: `Logs/CBT-R2-08-P2B3D1-Regression.xml/.log`, 정확한 범위 `...-TestFilter.txt`. 종료2는 기존6개 Asset gate 실패를 반영한다. 이전 C XML과 실패 이름을 직접 비교해 동일6개, 신규 실패 없음(선택 범위)을 확인했다.

- 승인 집중 **8/8 PASS** (`...-Focused2.xml/.log`).
- B3A **22/22**, B3B **15/15**, B3C **23/23**, B2D **40/40 PASS**.
- Legacy 관련 **220/220 PASS**: EntryConsolidation36 / FinalExchangeDriver15 / FinalRulesPolicy52 / Foundation2 / ProductionOwnership13 / SessionTurnFlow44 / UIRouting58.
- 기존 실제 Director/격리 UI Play Mode, Item UI/Film Hand, Mental/Status/Ultimate 및 Chain/Terminal·Reward/참가자 EXP 검사를 포함했다. 실제 Production 씬 수동 검증으로 기록하지 않는다.
- 실패6개 원문: `...-SixEvidence.txt`. 앞5개 `Forbidden Demo/Debug/Test/Legacy component remained: Game.Combat.Debugging.CombatHandValidationOptIn on CombatRuntime`, 마지막 Template predicate `Expected: False / But was: True`. 모두 **Asset Change Required** 유지.
- Ignored는 기존 `Game.Tests.Integration.PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners`. 별도 쓰기/읽기 Editor 프로세스 재시작 검증을 실행하지 않았다. 새 Skip 없음. 전체1,562/8/1은 과거 기준선이다.

중간 최초 집중은7 PASS/1 FAIL이었다. 작은 덱 검사에서 기존 B3B fixture의 ‘5장 draw’ 전용 시작 helper를 재사용한 것이 원인이다. 기존 helper/assertion은 그대로 두고, 새 검사만 실제 공급 카드 수에 맞는 Normal2/Clash1 시작 조건을 EntryPoint로 확인하도록 수정했다. 이후8/8 및 최종 회귀 통과. Runtime/기존 테스트 기대값을 수정해 숨긴 것이 아니다.

Unity 신규 테스트 컴파일 오류 및 `warning CS` 없음. 기존 Runtime 전체 강제 재컴파일를 수행한 것으로 주장하지 않는다. 기존 OfficeFlowController meta/빈 Tests.asmdef import 안내는 기존 파일 무변경으로 보존하며 Asset gate 예외와 컴파일 오류를 구분한다.

최종 보존: 시작 **1,815개 중 테스트 SO meta의 승인 라벨1개만 의도적으로 변경**, 나머지 **1,814개 SHA-256 동일**. SO 본문, 기존 Production Scene/Prefab/SO와 기존 GUID, Packages/ProjectSettings, 그 외 dirty/untracked 코드·보고서는 보존했다. 신규 파일은 새 테스트/meta와 이 보고서3개이며 삭제/이동 없음. 새 GUID 중복 및 테스트 SO의 Production 참조 없음. 신규 C#/보고서 공백 검사 통과. 기존 폰트/Scene의 이전 trailing whitespace는 수정하지 않았다. 승인 라벨은 기존 GUID `77fff0b326a327943b8f564b26f03718`을 바꾸지 않는다.

재현: 데이터가 이미 있는 환경에서 `-executeMethod Game.Tests.Integration.CombatApprovedClashPlaytestV1Tests.MarkApprovedNumbers`로 일치 검사/승인 라벨을 멱등 반영한 후 Test Runner에서 새8개와 위 선택 범위를 실행한다. 실제 Inspector 연결은5절의 별도 승인·콘텐츠 준비·변경 전 사본을 선행한다.

최종 권장 상태: **Approved Playtest V1 Config Verified — Asset Transition Plan Ready / Production Content & Manual Integration Pending**. Production 초기화는 비활성, Debug/Legacy 유지, Cutover 미실행. commit/push/PR 없음.
