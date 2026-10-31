# CBT-R2-08-P2B3D-4E1D-C2A-R2 — Legacy Save 전투 호환 최소 패치안

2026-10-31. **설계·승인 범위 확정 보고서이며 패치를 구현하지 않았다.** 권장 기본안은 Runtime2개 파일 + 기존 데이터2개 SO의 승인된 설정 + 후속 Scene 연결이다. 직접 부팅의 정의 조회를 해결하려고 Core/Save에 새로운 조회 경로나 Manager를 추가할 필요는 없다. 다만 기존 legacy 정의의 재사용, legacy 카드 수량, 미해석 키 처리 정책은 별도 승인 대상이다.

## 1. 지침 / Git / 이번 실제 범위

- 적용: 루트 `AGENTS.md` 전체. 중첩 `AGENTS.md`/`AGENTS.override.md` 없음.
- 저장소: `C:/Users/eunio/OneDrive/문서/GitHub/GAME_002`.
- branch `main`, HEAD `b42544824838e7de0e70b71afc2ca21a59db8192`. Unity `6000.3.23f1 (09d2ecc7fb28)`.
- 기존 tracked 수정12개 및 기존 미추적 SO/테스트/보고서는 모두 보존 대상이다. C2A에서 작성한 공식 Start의 초기 두 스킬도 현재 기준선이다.
- 필수 입력으로 C2A 완료 보고, RegressionFinal.xml/Precondition2.xml/Cold JSON과 실제 현재 호출 지점만 재검토했다. 이전 전체 감사·Unity 실행을 반복하지 않았다.
- 이번 수정/추가 범위는 본 보고서1개뿐이다. Runtime/Scene/Prefab/SO/Tests/기존 meta 수정 없음. 아래 API·코드는 승인 후 제안이며 실제 추가된 API가 아니다.
- 컴파일/Unity/Play Mode/테스트를 이번에 새로 실행하지 않았다. 과거 PASS를 이번 PASS로 보고하지 않는다. commit/push/PR/branch/패키지 변경 없음.

## 2. C2A 재현 증거와 현재 데이터

C2A 최종 기존 실행: 1,062 total /1,055 PASS/6 FAIL/1 IGNORED, 신규 호환 검사12/12, 초기 지급15/15. 별도 Unity writer29400/reader31564에서 저장5종 복원. 증거는 `C:/Users/eunio/.codex/visualizations/2026/10/31/01a190ef-97ac-70c3-86be-879fdbc0b8b9/D4E1DC2A-evidence/`에 있다. 이것은 이번 재실행 결과가 아니다.

실제 C2A Cold JSON의 hero.test 기록은 모두 Schema14, member/leader/combat lineup hero.test, level2/EXP5다.

| fixture | acquiredSkillKeys | equippedSkillKeys | 현재 활성 후보 결과 |
|---|---|---|---|
| empty | [] | [] | Load 성공, Snapshot 없음으로 전투 거부 |
| unknown | [legacy.skill] | [legacy.skill] | Load 성공/키 보존, Registry 미해석으로 Snapshot 없음 |
| known | [skill.production.0001, skill.production.0002] | 같은 순서 | Snapshot 생성, skill1 Hand rule 부재로 거부 |

`legacy.skill`은 재현 fixture용 미해석 문자열이다. 실제 어떤 역사적 스킬의 별칭인지 입증하는 자료는 없다. 이를 기본 공격·촌하우 또는 기존 skill1/2로 임의 매핑할 수 없다. 실제 개인 저장 파일은 제공되지 않았다.

현재 Production Registry는 아래 첫4개다. 후속 승인 시 신규3개를 추가한7개를 유지해야 한다.

| ID | persistentKey | 자산 |
|---|---|---|
| 1 | skill.production.0001 | Assets/GAME/Data/Skill/Skill_BasicAttack.asset |
| 2 | skill.production.0002 | Assets/GAME/Data/Skill/Skill_skill2.asset |
| 11 | skill.production.0011 | Assets/GAME/Data/Skill/Angel_Skill.asset |
| 12 | skill.production.0012 | Assets/GAME/Data/Skill/Angel_Skill2.asset |
| 10001 | skill.playtest.anand.basic | Assets/GAME/Data/Skill/Skill_AnandBasic_PlaytestV1.asset |
| 10002 | skill.playtest.anand.zornhau | Assets/GAME/Data/Skill/Skill_Zornhau_PlaytestV1.asset |
| 10003 | skill.playtest.angel.prophecy | Assets/GAME/Data/Skill/Skill_AngelTheProphecy_PlaytestV1.asset |

후보 Config의 Hand rules는10001 Normal copies2 /10002 Clash copies3뿐이다. 기존1/2 규칙은 없고, Enemy Hand opt-in은 빈 배열이므로 적11/12용 Hand rule은 현재 필요하지 않다. Skill ID·persistentKey·SO 수치는 바꾸지 않는다. 실제 hero 저장이11/12 또는 다른 기존 키를 장착했다면, Registry 해석 가능성과 아군 Hand 사용 가능성은 별개다. 이 경우에도 아군의 명시적 rule이 필요하며 enemy opt-in이 비어 있다는 이유로 검사를 건너뛰지 않는다. 현재 증거는 hero 장착1/2까지이므로 추가 키의 원래 용도·kind/copies를 승인받기 전에는 상태 보존 후 전투를 거부한다.

## 3. 실제 거부 경로와 소유권

### 저장과 Snapshot

- `SaveLoadService.TryLoad`(line175) → `RestoreGameSaveDataSnapshot`(line211) → Party/Progression/CharacterSkill 저장 Owner의 Restore → `RestoreAndFinish`에서 Exploration.
- `CharacterSkillRuntime.Restore`(line193)는 Registry에서 스킬을 찾지 않는다. 유효한 acquired/equipped 문자열은 보존한다. equipped는 acquired 또는 shared Film에 속해야 한다는 기존 조건이 있으며, 임의로 손상된 저장 전체를 그대로 보존하는 API는 아니다.
- `PersistentSkillCombatLoadoutBridge.ApplyToRequest`(line17)는 key→등록 SO를 해석한다. 미해석 key는 현재 warning 후 생략하고, resolvedSkills.Count>0일 때만 request snapshot을 만든다. unknown만 있으면 snapshot이 없다. known+unknown은 현재 부분 Snapshot을 만들 수 있으므로 새 정식 경로에서는 이를 허용하면 안 된다.
- `CombatEntryPoint.StartCombat`(line269)의 현재 Bridge 호출은 line289, startup try/catch보다 앞이다. 이 위치에서 새 예외를 던지는 설계는 rollback catch로 보호되지 않는다. 명시적인 Try 결과/기존 LogStartRejected 반환 경로가 필요하다.
- `FieldCombatantFactory.CreateSide`(line41)는 실제 actor 스킬을 구성한다. 현재 Player prefab에는 CombatSkillLoadoutComponent가 없으며 Registry의 호환 ID1/2/10 fallback 중1/2만 해석된다(10 미등록). 이 fallback이 actor에 있어도 request snapshot은 생성되지 않는다.
- bootstrap callback(line353~356) → `InitializeProductionHandBp(session, request)`(line456)의 ally identity/snapshot guard(line484~492) → 이후 explicit card-rule 검사. empty/unknown은 첫 guard, known은 skill1 rule 검사에서 거부된다.
- `NormalizedCombatStart.CreateRequest`(line924)는 같은 field object와 identity/snapshot을 새 startup request에 복사한다. 승인된 legacy eligibility를 호출 로컬 context로 전달할 수 있으며 Save/Scene에 저장할 필요가 없다.

### 정의와 직접 부팅

- Title Scene의 기존 `CharacterProgressionService`는 `CP_TestHero.asset`를 이미 참조한다. GUID `289a48d3275f99649a3f67dbc426837f`, characterId hero.test, level1/max5, 기존 EXP 구간10/20/30/40이다.
- 공식 Start는 Definitions에 CP_Anand만 갖고 있다. 정의 카탈로그와 initial party는 별도 필드다. `CharacterStartDefinitionSO.TryValidate`는 initial members가 definitions의 부분집합이면 허용한다.
- `RuntimeBootstrapper.ConfigureCharacterServices`(line120)는 Start.Definitions를 기존 Progression에 합치고 Party/Skill Owner에 동일 Start를 설정한다. `CharacterProgressionService.CanConfigureStartDefinition/TryConfigureStartDefinition`(line35/51)은 동일 ID에 같은 SO 참조면 수용하고 다른 SO면 거부한다.
- `CharacterPartyCombatAdapter.TryBindSinglePlayerRequest` → BuildRequest(line84~85)는 정의와 progression state를 모두 요구한다. 아난 전용 카탈로그로 hero 저장을 Load한 직접 Dungeon 부팅은 이곳에서 실패한다.
- `PartyRuntime.TryConfigureStartDefinition`(line49)은 기존 runtime/restore state를 무조건 리셋하지 않는다. `RestoreSaveData`와 `ResetForNewGame`는 각각 저장 party와 initial party를 소비한다. Skill 지급도 `CharacterSkillSaveParticipant.ResetForNewGame`에서만 한다.

새 Owner는 필요 없다. 기존 EntryPoint/Factory/Session/Flow/Director, RuntimeBootstrapper/Party/Progression/Skill/Save/GameFlow Owner를 유지한다.


직접 Dungeon의 유효한 순서는 **동일 Start로 정의 카탈로그 등록 → 기존 SaveLoadService.TryLoad → restore 완료/Exploration → 기존 Contact/Field Attack**이다. CanLoadNow는 Exploration에서도 허용된다. 이는 Scene을 직접 열기만 하면 아난 스킬을 자동 지급한다는 뜻이 아니다. ConfigureCharacterServices는 grant를 실행하지 않고 NewGameRequest는 idle Title을 요구한다. fresh Dungeon에서 Load나 명시적 New Game 없이 즉시 새 게임을 자동 시작해야 한다면 별도 boot-intent 정책/Runtime 승인이 필요하다. 이 기본안에서 임의 자동 Reset/grant로 해결하지 않는다.
## 4. 권장 Legacy 정책 — 입력을 섞지 않음

| 입력 | 승인 후 목표 |
|---|---|
| hero.test, 정상 장착 key1/2 | 저장 순서/소유 상태 그대로 → 등록된 원래 SO의 Snapshot → 명시적 legacy Hand rules |
| hero.test, equipped가 실제로 비어 있고 해석된 Snapshot도 없음 | 저장에는 지급/장착하지 않고 기존 Factory가 해석한1/2만 일회성 전투 Snapshot으로 고정. 원래 스킬/MP/효과 유지 |
| acquired에 미해석 키가 있으나 장착하지 않음 | 보유 key는 보존. 전투에서 선택하지 않은 key 때문에 유효한 장착 Snapshot을 변경하지 않음 |
| unknown 또는 known+unknown 장착 | Load는 성공/상태 보존. 정식 전투는 식별 가능한 진단으로 거부, 부분 Snapshot·빈 장착 fallback 금지 |
| 다른 Character 또는 아난 빈 장착 | hero fallback 적용 금지. 기존 유효 Snapshot/Unique 계약 또는 기존 거부 유지 |
| Registry key/ID 중복·잘못된 SO·명시 규칙 누락 | 전투 거부. 임의로 첫 정의를 선택하거나 규칙을 생략하지 않음 |

빈 equipped는 과거 schema의 필드 부재와 사용자가 의도적으로 해제한 상태를 현재 Runtime 데이터만으로 구별할 수 없다. 두 경우 모두 기존 hero의 field fallback 의미를 유지한다는 **명시적 호환 정책 승인**이 필요하다. 아난 초기 스킬을 hero에 지급하는 정책이 아니다.

실제 미해석 역사 key를 자동 복구하려면 입증된 key→원래 SO 대응표가 별도로 필요하다. 현 Registry의 단일 persistentKey/ID 고유성 계약에서 alias SO를 중복 등록해 우회하면 안 된다. 지원 불명 key를 안전하게 거부하는 정책을 승인하지 않고 “모든 임의 key의 전투 성공”을 요구한다면 C2A 활성화는 계속 차단한다.

## 5. A — 데이터/Inspector로 해결하는 범위

| 정확한 파일/필드 | 현재 → 목표 | Save/직렬화 및 승인 영향 |
|---|---|---|
| Assets/GAME/Data/Character/CharacterStart_AnandDeMorte.asset / definitions[1] | CP_Anand1개 → 기존 CP_TestHero 참조 추가 | 동일 GUID289a48d... 재사용. 신규/복제 Character 정의 없음. initialMemberIds/leader/combat/defaultReward/initialSkillGrants는 아난만 유지 |
| 같은 Start / initial fields | 현재 아난1명·두 grant | 그대로 유지. legacy 정의 등록은 파티 합류나 스킬 지급이 아님 |
| Assets/GAME/Data/Combat/Runtime/FinalCombatRuntimeConfig_AnandStart_PlaytestV1_Candidate.asset / handCardRules[2],[3] | 기존1/2 규칙 없음 → 원래 SO의 명시 규칙 추가 | 권장 호환 제안: skill1 Normal copies4, skill2 Clash copies6. 아래 승인 조건 필수. 기존 아난2/3, capacity2/3, MP/BP/Clash/seed0/Ultimate/Enemy opt-in은 보존 |
| Assets/GAME/Scenes/TitleScene.unity / Systems/RuntimeBootstrapper.characterStartDefinition | null → 공식 Start GUID5b2e29e279d96f64291e0d33e37f39a2 | legacy Title 정의 참조도 유지. 첫 bootstrap 전에 설정 |
| Assets/GAME/Scenes/Dungeon_1_Production.unity / Runtime의 같은 필드 | null → Title과 같은 SO | 직접 부팅/restore readiness가 같은 카탈로그를 소비. Start 교체 guard 완화 불필요 |
| 같은 Dungeon / Runtime/Combat/CombatRuntime EntryPoint.skillDefinitions, finalExchangeRuntimeConfig |4개/기존 Config → 기존4+신규3/승인 후보 | Scene override만. Prefab source 무변경. 활성화는 검증/승인 후 별도 마지막 단계 |

**데이터 승인 유의점:** CP_TestHero는 경로가 Data/Test이고 기존 Title에서 이미 사용하는 legacy 자산이다. 이것이 다른 TestOnly fixture의 새 Production 등록을 허용하는 근거는 아니다. 이 특정 기존 정의를 공식 Start의 조회 카탈로그에 재사용할지 명시적으로 승인받아야 한다. 이전 “TestOnly 신규 연결 금지”를 임의로 무시하지 않는다. 재사용이 승인되지 않으면 직접 부팅 해결도 보류하며 새 definition/catalog/Runtime 조회기를 임의 제작하지 않는다.

**카드 수량 제안 근거:** 현 Scene Hand Debug의 InspectorFixture는 skill1 Normal4장/skill2 Clash6장, visible capacity2/3이다. 기존 두 스킬의 빈 장착 fallback을 복구할 때 관찰된 덱 구성을 보존하는 제안이지, 과거 Debug 값을 이미 승인된 Production 정책이라고 부르는 것이 아니다. 사용자 승인 후 정적 Config 데이터로 명시하며 Debug가 규칙 Owner가 되지는 않는다. legacy4/6과 아난2/3을 분리해 검사한다. 다른 legacy 수량을 선택하면 그것은 별도 동작 변경이다.

이 데이터 설정이면 직접 부팅의 정의 조회에 RuntimeBootstrapper/CharacterProgressionService/PartyRuntime/GameFlowController/SaveLoadService 코드를 수정할 필요가 없다. 알려진 장착1/2의 rule 부재도 데이터로 해결한다. 그러나 빈 장착 request snapshot 문제는 데이터만으로 해결되지 않는다.

## 6. B — 필수 Runtime2개 파일의 구체적 패치안

### B1. PersistentSkillCombatLoadoutBridge.cs

경로: `Assets/GAME/Scripts/Combat/Runtime/Integration/PersistentSkillCombatLoadoutBridge.cs`.

- 기존 public `ApplyToRequest(request, definitions, diagnosticContext)` 시그니처/호환 동작 유지. 직접 호출하는 기존 Bridge 단위 검사는 보존한다.
- 내부 Try 경계 추가 제안:
  `internal static bool TryApplyForProduction(CombatStartRequest request, IEnumerable<SkillDefinitionSO> definitions, UnityEngine.Object context, out HashSet<GameObject> legacyEmptyAllies, out string error)`.
- 기존 BuildDefinitionMap/실제 해석 loop를 private 공통 경계로 묶어 두 개의 서로 다른 스킬 해석기를 만들지 않는다.
- 정식 경로는 먼저 전체 장착 key를 preflight한다. missing/ambiguous key, 충돌한 runtime ID 등은 명시적으로 false. known+unknown에서 known만 채택하지 않는다. 거부 전에 저장 Runtime의 TryAcquire/TryEquip/TryUnequip/Reset을 호출하지 않는다. 실패 시 request의 stale temporary snapshot도 사용하지 않는다.
- 유효한 입력에는 기존 Compatibility/Film/Unique 및 ownership/level 평가를 재사용한다. Film ownership 설정 등 기존 부수 효과를 검사하되 unresolved preflight 거부 전에 영구 스킬 상태를 변경하지 않는다.
- canonical Skill Owner가 존재하고 실제 Party의 소유·선택 멤버이며 유효한 정의가 확인된 ID가 정확히 hero.test, equipped.Count==0, 기존 resolver 결과 snapshot이 없는 actor만 로컬 eligibility set에 포함한다. all-unresolved non-empty 배열은 절대 포함하지 않는다.
- 이 메서드는 Factory의1/2/10 fallback 알고리즘을 복제하지 않는다. 실제 fallback 스킬은 기존 Factory에서 한 번만 계산한다.

### B2. CombatEntryPoint.cs

경로: `Assets/GAME/Scripts/Combat/Runtime/Core/CombatEntryPoint.cs`.

- `StartCombat`에서 global owner/Exploration guard를 먼저 통과한 뒤 loadout 해석. Production FinalExchange(`StandoffClashChain`)는 새 Try 경계를 사용하고, LegacyPlanning 호환 경로/기존 public Bridge API는 유지한다.
- Try 실패는 기존 `LogStartRejected`와 false 반환. 현재 Bridge 호출이 try 바깥이라는 이유로 새 예외를 누출하지 않는다. `_startingCombat`/ActiveSession/Director/FieldLock/상태가 부분 생성되지 않아야 한다.
- 새 정식 경로는 Config flag가 false인 FinalExchange라도 unknown 장착을 부분 Snapshot으로 통과시키지 않는 **의도적인 강화**다. C2A의 current-disabled-config unknown 성공 관찰은 미래 승인 계약이 아니다. 해당 테스트 입력은 유지하되 새 거부 이유/상태 불변을 검사하도록 갱신한다. empty/known의 기존 비활성 모드 시작은 유지한다.
- 기존 bootstrap callback에서 `VerifyConstructedRoster` 후, 정식 Hand/BP flag가 true일 때만 새 private `AttachValidatedLegacyFallbackSnapshots(session, startupRequest, legacyEmptyAllies)`를 호출하고 **기존 InitializeProductionHandBp(session, request)를 그대로 호출**한다.
- private helper는 eligibility set+동일 field/hero identity+snapshot 부재를 다시 확인한다. actor.Skills는 실제 Factory가 이미 계산한 결과다. 모두 현재 Registry의 원래 skill1/2 SO, 올바른 persistentKey, Compatibility category, 고유 ID, 비어 있지 않은 목록이어야 한다. 예상 밖 skill10/아난10001/10002/가짜 ISkill/미등록 SO는 거부한다. 이 정책 범위 확장은 별도 승인이다.
- 검증한 목록만 `new CombatSkillLoadoutSnapshot(actor.Skills)`로 **일회성 startupRequest**에 고정한다. includeCompatibilityLoadout=false로 이미 완성된 목록을 다시 합치지 않는다. 저장 장착/획득 key에는 쓰지 않는다.
- 그 뒤 기존 initializer의 identity/nonempty snapshot, 등록 SO, explicit Hand rule, BP/Clash/Ultimate 검사를 전부 통과해야 한다. 필수 guard를 삭제하거나 hero면 무조건 허용하는 분기는 만들지 않는다.
- 실패는 기존 startup catch/AbortStartup/RollbackStartup에 맡긴다. 정상 경로만 HasProductionHandBpInitialization marker를 세우고 기존 Debug가 skip한다. Debug 제거/승격 없음.

검토용 호출 순서(의사 코드, 미구현):

```text
StartCombat:
  기존 중복/요청/global owner/Exploration 검증
  FinalExchange -> TryApplyForProduction(... out legacyEmptyAllies, out error)
    false -> LogStartRejected(error); return false
  그 외 -> 기존 ApplyToRequest
  기존 Normalize / Roster 검증
  기존 bootstrap callback:
    VerifyConstructedRoster
    flag=true -> AttachValidatedLegacyFallbackSnapshots(... eligible)
    InitializeProductionHandBp(session, startupRequest)  // 기존 검사 유지
  기존 bind / publish / Director / 종료 흐름
```

### 보존할 코드와 API

- `FieldCombatantFactory`, `CombatStartRequest`, `CombatSkillLoadoutSnapshot`, `CharacterPartyCombatAdapter`, Core/Save/Character Runtime은 기본안에서 무변경이다.
- 특히 `InitializeProductionHandBp`는 기존 private 두 인자 시그니처를 유지하고 overload도 만들지 않는다. CombatProductionHandBpStartupTests의 line185/248/492가 이름으로 reflection 후 두 인자로 호출하므로 signature/overload 변경은 무관한 실패를 만든다.
- public API 변경0, Save Schema 변경0, SerializeField 추가/이름/형식 변경0, meta/GUID 변경0. 추가하는 것은 내부 Try 메서드와 호출 로컬 context, private helper다. public API나 저장 metadata가 필요한 설계로 확대되면 별도 차단/승인 사항이다.

## 7. 진행 상태 보존의 데이터 한계

`CharacterProgressionService.RestoreSaveData`는 정의를 찾으면 `NormalizeRestoredState`(line201)에서 level을1..MaximumLevel로 clamp하고 EXP를 정산한다. 현재 C2A fixture의 hero level2/EXP5는 기존 CP_TestHero와 일치한다. 새 정의 제작 없이 기존 정의를 쓰는 이유도 이 계약을 바꾸지 않기 위해서다.

그러나 실제 저장에 hero level>5 또는 정의와 불일치하는 EXP가 있다면, 새로 카탈로그를 등록한 직접 부팅에서 값이 달라질 가능성이 있다. 이를 “데이터 연결만 하면 모든 저장 상태 보존”이라고 단정하면 안 된다. 실제 지원 저장 범위/기존 정의와의 일치 확인 및 비정상·경계 fixture가 활성화 선행 조건이다.

불일치 사례도 **원값 그대로 Load·보존**해야 한다면 `CharacterProgressionService.RestoreSaveData`/definition 해석의 추가 내부 정책(예: 미해결 상태 보존과 전투만 거부)이 필요하다. 이 파일은 기본2개 패치에 자동 포함하지 않는다. clamp를 조용히 허용하거나 모든 Character의 성장/정산 규칙을 광범위 변경하지 않는다. 지원되지 않는 progression에서 데이터 손실 없는 처리가 승인·검증되기 전에는 C2A 연결/활성화를 중단한다.

## 8. 승인 후 테스트와 통과 기준

Runtime 단계는 우선 메모리 Start/Config clone에서 실행하고 보호 자산은 변경하지 않는다. 이후 데이터·Scene 승인이 따로 충족되면 실제 연결 최종 상태에서 재검증한다.

| 검사 | 필수 통과 기준 |
|---|---|
| hero known1/2 | 같은 원본 SO/ID/key/장착 순서, 새 hand rule로 실제 Session·UI 선택·MP/Discard/Clash 사용. 영구 상태 전후 동일 |
| hero empty | 저장은 빈 acquired/equipped 유지. 전투 snapshot만 기존1/2, 아난10001/10002 없음. 제안 legacy 덱4/6·visible2/3·MP2·BP0을 별도 확인 |
| unknown 및 mixed known+unknown | Load 성공/원본 디스크 및 보유/장착/성장 불변. FinalExchange 시작 false, 이벤트0/ActiveSession 없음/Exploration·Input·FieldLock 유지. 부분 snapshot 금지 |
| unknown acquired but unequipped | 보유 key 보존, 유효한 선택/명시적 empty 정책만 사용. unknown을 다른 key로 변환하지 않음 |
| Registry 중복/누락·legacy 규칙 누락·예상 밖 fallback | precise rejection, 잘못된 SO/카드 선택 없음, 부분 초기화/owner 누수 없음 |
| 공식 아난 New Game | 실제 공식 Start, party1·level1/EXP0·두 스킬 보유/장착·Normal2/Clash3 유지. hero catalog 추가가 party/grant를 늘리지 않음 |
| 중복/재시작 | 같은 요청은 변경 장착·EXP 보존, 새 명시 요청만 초기화. 기존15개 모두 유지 |
| Title→Dungeon / 직접 Dungeon | 같은 Start SO의 두 CP 조회, 같은 hero snapshot/EXP recipient, Load 순서에서 재지급/파티 덮어쓰기 없음 |
| 정의 범위/순서 | level1/4/5 경계, CP와 일치하는 EXP, 초과 level/EXP, conflicting definition, 반복 bootstrap/반복 Load. 승인한 지원 상태에서 원값 손실 없음 |
| 실제 Combat/Director | Contact 적 선공, FieldAttack 아군 선공, 기존 적11/12, 승리/패배/Abort/Result1회/Reward·Quest/Exploration/Input/Camera/TimeScale·재조우. 실제 Director 완료와 수동 완료 probe 구분 |
| Cold Load | 별도 writer/reader PID, fresh owner, hero empty/known/unknown/mixed + 아난 초기/변경 저장. legacy empty/known은 Load뿐 아니라 실제 Combat까지. unknown은 승인한 safe rejection 검증 |
| Film/Unique | shared Film/Character 보유 경계, 소유자·level unlock, 저장복원/현재 public Bridge 호환, 정식 경로 strict preflight와 충돌 없음 |
| 기존 회귀 | C2A1,062건 선택 범위, 초기 지급15, Encounter111, UI58, BP15/Hand/Clash/MP, reflection 초기화 검증, Missing Script/Reference/GUID/Prefab/Debug marker 보존 |
| 기존6개 | 각각 새로 실행. Debug를 보존하는 단계에서는 기존6 FAIL과 신규 실패를 분리. 6개 gate 삭제/Ignore/조건 완화 금지 |
| 컴파일/Play Mode | C# errors/warnings 별도 기록, 자동 EnterPlayMode 및 실제 사용자 수동 Play Mode 구분. 실제 수행하지 않은 것은 미검증 |

기존 `ProductionInitializationCompatibilityTests`는 empty/known을 실제 성공 시나리오로 보강하고 unknown은 Load 보존+명시적 전투 거부로 유지한다. definition/rule를 의도적으로 빼는 원래 negative 사례도 남긴다. pending-boundary footprint는 실제 Scene 연결 승인이 이뤄질 때만 새 연결·기존 Debug·Prefab source 보존을 명시적으로 검사하도록 갱신한다. 직접 public Bridge의 기존 omit/duplicate 검사를 삭제해 정식 경로 실패를 숨기지 않는다.

## 9. 필요한 승인 패키지 / C2A 및 C2B 조건

1. **Runtime 승인:** 위 Bridge/CombatEntryPoint2개와 직접 관련 검증 코드. public API/Schema/SerializeField 무변경, 실제 Factory 결과의 hero empty 한정 임시 Snapshot, FinalExchange unknown/mixed 거부 정책.
2. **데이터 정책 승인:** 기존 CP_TestHero의 정확한 참조 재사용, legacy skill1 Normal4/skill2 Clash6 제안 또는 명시적으로 선택한 대안. 경계 progression 보존/미해석 key 지원 범위를 확정. 새 TestOnly fixture를 공식 데이터로 등록하는 승인으로 확대하지 않는다.
3. **연결 승인:** Runtime·데이터 격리 검증/Cold Combat 통과 후 기존 C2A 네 자산의 정의/Hand rules/동일 Start·Registry7·Config 참조 연결을 구체적으로 재확인. enableProductionHandBp=true는 마지막 선행 조건 충족 후에만 허용.
4. **C2B:** 위 연결과 실제 부팅/Continue/직접 부팅 회귀가 완료될 때까지 진입 차단. Debug 제거는 이후 별도 승인 대상이며 원본 Prefab/Cutover까지 완료했다고 선언하지 않는다.

미해석 키를 아무 스킬로 바꾸어 전투를 성공시키는 것, Load 때 hero에 아난 스킬을 지급하는 것, initializer snapshot 검사를 없애는 것, 정의 없는 hero를 무조건 유효하다고 간주하는 안은 제외한다.

## 10. 이번 검증·보존 및 Development tracking

이번은 기존 C2A 증거 대조/현재 코드·정적 참조 조사와 보고서 작성만 했다. 새로운 Unity compile/Inspector 실행/Play Mode/테스트는 미실행이다. 패치의 compile/Play/통합 검증은 **Unverified**다.

기존 파일 **2,668개 SHA-256 전부 일치**, 누락0개. 새 저장소 파일은 본 보고서1개뿐이다. 기존 tracked 수정12개 및 기존 미추적 SO/테스트/보고서를 그대로 보존했다. branch/HEAD 동일. `git diff --check` exit0이며 새 보고서 trailing whitespace 없음. 기존 코드/자산/API/meta를 수정하지 않았다.

- System: Legacy Save / Character Definitions / Combat Snapshot & Hand.
- Requirement: D4E-1D-C2A-R2 준비.
- Production Owner: 기존 Core/Character/Save/Combat Owner 유지. 신규 Manager/EntryPoint 없음.
- Implementation: 패치안 작성 완료. Runtime/데이터/Scene 패치 Not Implemented (승인 전 금지).
- Compile / Inspector / Play Mode / Integration: 이번 새 실행 없음. 과거 C2A 실행 결과 재사용. 제안 패치 자체는 모두 Unverified.
- Recommended tracking: Patch Scope Identified — Two Runtime Files; Existing Definition/Data Reuse and Legacy Policy Approval Required; C2A Activation/C2B Blocked.
- Stale mapping: 직접 부팅은 새 Runtime 조회기 없이 기존 Start 카탈로그로 해결 가능하다. CP_TestHero 등록을 initial party로 혼동하지 않는다. unknown=empty 취급은 금지하며 C2A negative PASS가 legacy 전투 PASS는 아니다.