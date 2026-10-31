# CBT-R2-08-P2B3D-4E1A — Production 연결 사전 감사

## 결론과 증거 범위

**연결 대상과 승인 항목 식별 완료. 실제 Production 적용/Cutover는 차단 상태다.** 격리 검증의 성공만으로 현재 Dungeon의 초기화가 준비된 것은 아니다. 현재 Dungeon은 기존 스킬/Debug 초기화, 새 후보 Config는 비활성/Hand 규칙 없음, 새 게임 초기 스킬 지급 연결은 부재다.

이번 작업은 파일 읽기와 기존 XML 대조만 수행했다. Unity Editor를 실행하거나 Scene을 열고 저장하지 않았고, 컴파일·테스트·수동 Play Mode를 새로 실행하지 않았다. 추가 파일은 이 보고서 하나다. 기존 Runtime/Scene/Prefab/SO/meta/ID 및 사용자 변경은 보존했다.

지침: 루트 `AGENTS.md`, 적용 중첩 없음. branch `main`, HEAD `b42544824838e7de0e70b71afc2ca21a59db8192`. 실제 ProjectVersion `6000.3.23f1`. 기존 tracked 변경은 Encounter Group/Trigger와 WorldEncounterIntegrationTests 3개이며, 아난 SO·스킬·격리 씬·관련 테스트/보고서는 미추적 상태다. 모두 현재 감사의 기준선으로 보존한다. commit/push/PR 없음.

읽은 선행 자료: D4A ContentProduction, D4B/BR1, D4C/C2, D4D, D4E0, D4E0R1 보고서. 이전 문서의 “천사 Prefab 없음”, “아난 ID 미승인”은 해당 배치 당시 기록이다. 현재는 실제 신규 자산이 존재하고 ID/EXP와 일부 Playtest 수치가 승인됐으므로 과거 Pending을 그대로 현재 상태로 쓰지 않는다. 반대로 이후 격리 검증도 Production 검증으로 승격하지 않는다.

## 실제 Production 구조와 Owner

`Assets/GAME/Scenes/Dungeon_1_Production.unity`:

- `Runtime/RuntimeBootstrapper.characterStartDefinition = null`.
- `World/Encounters/Encounter_01/Enemy_01`, `Encounter_02/Enemy_02`, `Encounter_03/Enemy_03`: HP/maxHP10, CombatSkillLoadout IDs11/12, EnemySourceComponent 없음. 각 Group은 autoCollectChildren=true, manual enemies=[], useEncounterFlowMode=true, StandoffClashChain이다.
- 각 Enemy 하위 ContactTrigger는 같은 CombatEntryPoint와 해당 Group/Enemy를 명시 참조한다. encounterId `dungeon1.encounter.01/02/03`, OpeningEffect=null, **PlayerGotHit/Enemies**다. 이 ID는 World clear 저장·퀘스트 연결의 계약이므로 새 천사 source key나 격리 ID로 바꾸지 않는다.
- Player 프리팹 인스턴스는 `PlayerRoot`로 명명된다. 원본 `Assets/GAME/Prefabs/Player.prefab`의 HP/maxHP10을 상속한다. FieldAttack는 PlayerFirstHit/Allies, targetMask128(Enemy layer7), hitDelay0.12, cooldown0.35, hitBox2.4×1.6이다. InputController/FieldAttack/Motor 소유권을 유지한다.
- `Assets/GAME/Prefabs/CombatRuntime.prefab` 인스턴스의 EntryPoint registry는 Scene override로 기존 ID1/2/11/12 네 SO를 참조한다. 원본 Prefab registry는 빈 배열이다. 원본 EntryPoint config는 `Assets/GAME/Data/Combat/Runtime/FinalCombatRuntimeConfig_V1.asset`이며 Scene에서 후보 Config로 교체한 흔적이 없다.
- Director/카메라·CombatRewardUIBinder.rewardPanel·FieldLock의 플레이어 참조는 Scene override로 연결돼 있다. 원본 Prefab의 cameraController/rewardPanel null만 보고 Production 연결 누락으로 판정하면 안 된다.
- UI는 `Assets/GAME/Prefabs/UI/ProductionDungeonUI.prefab` 인스턴스다. root GameUIRootController/UIScreenRouter, `CombatCanvas/CombatHUD/FinalExchangePanel`의 FinalCombatUIBinder, `RewardCanvas/RewardUI`의 RewardUIPanel을 재사용한다.

`Dungeon_Template.unity`는 같은 CombatRuntime·Player를 사용하지만 RuntimeBootstrapper가 Scene에 직렬화돼 있지 않아 기존 AutoBootstrap 경로가 적용된다. 조우는 `World/Encounters/TestEncounter_01/EnemyRoot`, `TestEncounter_02/EnemyRoot`; IDs `validation.production.npc.quest.kill`, `template.test.02`, Contact **PlayerFirstHit/Allies**, 기존 OpeningEffect 참조가 있다. Production과 선공/Opening/퀘스트 ID가 다르므로 Template 설정을 Production에 일괄 복사하지 않는다.

실제 호출 관계:

1. Contact 또는 PlayerFieldAttackController → Group/독립 Trigger의 예약 → CombatStartRequest. Group의 flowMode가 두 경로의 공통 기준이다.
2. CharacterPartyCombatAdapter가 공식 Party ID를 field object에 결합한다. 단일 아군 경로는 StartDefinition이 있으면 리더·전투편성·Progression·HP를 검증한다. 다인 파티는 명시적 field bindings가 필요하다.
3. CombatEntryPoint에서 PersistentSkillCombatLoadoutBridge/EnemyCombatProvenanceBridge가 요청 snapshot을 준비하고 FieldCombatantFactory를 명시 전달한다. CombatBootstrapper의 DummyFactory 기본 인자는 별도 호환/fixture 경로이며 현재 Production이 선택한 factory는 아니다.
4. CombatSession/StateMachine 생성 → roster 검증 → 정식 Hand/BP/Ultimate/Clash 초기화 → OpeningEffect → Standoff → GameFlow 상태 동기화 → Orchestrator/Director/적 명령 Owner bind. 실패 시 AbortStartup/부분 구성 정리.
5. 기존 SkillRunner/executor 효과 → Terminal → EntryPoint의 CombatResult/CompletionId → 기존 Reward/EXP 및 World lifecycle → Reward UI 닫기 → GameFlowController → Exploration. World는 transform/active/physics·FieldLock·카메라를 복구한 뒤 Encounter Owner에 ObserveExploration을 알린다.

Core 서비스는 RuntimeBootstrapper의 기존 FindOrCreate로 GameStateMachine/GameFlowController, InputInstaller, SaveLoadService, Party/Progression/CharacterSkillSaveParticipant 등을 마련한다. 새 씬마다 새 영구 owner를 별도로 추가하지 않는다. 씬 파일 조사에서 별도 경쟁 CombatEntryPoint/Combat Manager는 식별하지 못했다. 실제 로드 순서의 singleton 중복은 Play Mode에서 확인해야 한다.

## Debug·Legacy 책임 충돌

Production CombatRuntime에는 Scene-added Hand Validation, ClashSeries Validation, BP Validation이 활성이다. 또한 원본 CombatRuntime Prefab의 BP Validation이 활성이고 Scene의 m_RemovedComponents=[]이므로 **상속 BP와 Scene-added BP가 함께 존재**한다. 원본 BP는 initialPlayerBp3/skill2 Ultimate·enhancement fixture를 갖고 Scene-added BP는 skillFixtures=[]다. Hand Debug는 player copies4/6·seed2303, enemy Hand=true/copies4/6·seed2304를 주입한다. 승인된 정식 BP초기0 및 Enemy Hand source opt-in과 동일한 제작 데이터가 아니다.

이는 새 Production owner를 추가할 이유가 아니라 기존 정식 초기화로 책임을 넘겨야 하는 대상이다. 정식 초기화 플래그에 따른 Debug 중복 적용 방어는 있어도 Debug가 직렬화돼 있다는 자산 gate를 해소하지 않는다. 별도 승인 이후 base BP와 Scene-added BP/Hand/Clash를 구분해 제거해야 한다. 이번에는 제거하지 않았다.

LegacyPlanning, 기존 ID1/2/11/12와 compatibility fallback, Dummy fixture 및 Demo/Debug 코드는 보존한다. Registry의 기존 스킬을 먼저 삭제하면 다른 기존 loadout/저장 key가 해석되지 않을 수 있으므로 새 세 스킬은 우선 추가하고 실제 전환 대상만 교체한다.

## Production 자산 영향표

표의 신규 목표 파일명은 **제안 경로**이며 아직 생성되지 않았다. 모든 보호 자산 편집은 별도 승인이 필요하다.

| 실제 파일 | 현재 참조 | 목표 참조 | 필요한 변경 | 위험 | 승인 필요 |
|---|---|---|---|---|---|
| Assets/GAME/Scenes/TitleScene.unity | RuntimeBootstrapper의 characterStartDefinition 미직렬화(null) | CharacterStart_AnandDeMorte | 기존 Bootstrapper Inspector에 연결 | New Game/Continue 영구 서비스에 먼저 적용되는 설정; Dungeon만 연결하면 기존 Party 상태가 남을 수 있음 | 보호 씬 연결 승인 |
| Assets/GAME/Scenes/Dungeon_1_Production.unity | Runtime.characterStartDefinition=null | 동일 Start SO | 직접 씬 시작도 동일 설정, Title과 일치 | 서로 다른 Start owner 거부·기존 Save 상태 보존 | 보호 씬 연결 승인 |
| Assets/GAME/Data/Character/CharacterStart_AnandDeMorte.asset | CP_Anand, initial member/leader/combat/default reward 모두 char.anand_de_morte | 유지 | 자체 수정 불필요; Bootstrapper가 소비 | 초기 스킬/HP/전투 능력치를 담는 SO가 아님 | SO 재승인 불필요 |
| Assets/GAME/Data/Character/CP_AnandDeMorte.asset | level1/max60/59개 EXP 구간/Unique빈목록 | 유지 | Start.definitions의 기존 참조 재사용 | 승인 안 된 성장/Unique를 추가하면 안 됨 | 새 성장 콘텐츠는 별도 승인 |
| Assets/GAME/Prefabs/Player.prefab 및 Dungeon_1_Production의 PlayerRoot | CombatHpComponent hp/maxHp10 | 아난 field hp/maxHp320 | 최초 전환은 Scene instance override 권장; 다른 씬용 원본 일괄 변경은 피함 | 저장·전투 결과 HP writeback과 New Game 재초기화 정책; CP가 HP를 공급하지 않음 | 보호 씬 override 승인 |
| Assets/GAME/Scenes/Dungeon_1_Production.unity / CombatRuntime instance | EntryPoint.skillDefinitions IDs1/2/11/12 | 기존 유지 +10001/10002/10003 | Scene override array에 실제 세 SO 추가; key/id 중복 검사 | Prefab만 바꿔도 기존 Scene array override가 덮을 수 있음 | 보호 씬 승인 |
| Assets/GAME/Prefabs/CombatRuntime.prefab | EntryPoint.skillDefinitions=[]; config=FinalCombatRuntimeConfig_V1 | Owner와 API 유지; 승인 완비 새 Config | 전체 소비 씬 영향 평가 후 config 기본 참조를 바꿀지 Scene override만 둘지 결정 | Template/다른 씬에 승인 안 된 전환 전파 | 보호 Prefab 또는 Scene 승인 |
| Assets/GAME/Data/Combat/Runtime/FinalCombatRuntimeConfig_ProductionPlaytestV1_Candidate.asset | 비활성, handCardRules=[], ultimateSkills=[], enemyHandSourceKeys=[] | 제작 승인 완료된 별도 Production Playtest Config | 예: 같은 폴더의 FinalCombatRuntimeConfig_ProductionPlaytestV1.asset을 새 GUID로 만들고 승인 rules/copies/seed 작성; candidate 유지 | Fixture 복사/활성화만으로 데이터 누락 은폐 금지 | 카드 정책 및 새 자산 승인 |
| Assets/GAME/Scripts/NonCombat/Progress/CharacterSkillSaveParticipant.cs, CharacterSkillRuntime.cs 및 기존 New Game 초기화 경계 | Reset은 빈 skill runtime, TryAcquire/TryEquip API 존재; 초기 두 스킬 지급 호출 없음 | char.anand_de_morte에 두 key를 승인 시점에 1회 지급·장착 | 정책 승인 후 기존 소유권 안에서 최소 데이터/초기화 연결 코드 필요; 새 Manager 금지 | Continue 덮어쓰기·매 씬 재지급·사용자 장착 초기화 금지 | 초기 보유/장착 정책 및 후속 코드 Batch 승인 |
| Assets/GAME/Data/Skill/Skill_AnandBasic_PlaytestV1.asset |10001 / skill.playtest.anand.basic / MP0 | 유지; Normal card rule | Registry 추가와 영구 장착 key 연결 | 기존 Basic ID1의 MP1과 다름 | 기존 스킬 수치 재승인 불필요 |
| Assets/GAME/Data/Skill/Skill_Zornhau_PlaytestV1.asset |10002 / skill.playtest.anand.zornhau / MP1/power3/damage4 | 유지; Clash card rule | Registry/장착/rule 연결 | SO ownership Compatibility와 Hand kind 독립; speed 기본5·표현 미확정 | speed/표현은 별도 결정 |
| Assets/GAME/Data/Skill/Skill_AngelTheProphecy_PlaytestV1.asset |10003 / skill.playtest.angel.prophecy / MP1/power2/damage3 | 유지; enemy skillIds10003 | Registry 및 enemy loadout | 새 효과 없음, speed5·trigger빈문자열/VFX·SFX null | 표현/스킬속도 추가 결정 |
| Assets/GAME/Data/Enemy/Enemy_Angel.asset | enemy.chapter1.angel; Film빈목록/Profile null | source identity 유지 | EnemySourceComponent.definition에서 참조 | Source가 HP/loadout/회피/방어/속도를 공급하지 않음 | Film/AI 변경만 별도 승인 |
| Assets/GAME/Tests/PlayMode/Enemy_Angel_Playtest_Candidate.prefab | 실제 source·HP14·loadout10003·motor/patrol/physics·천사 art; 비활성, patrol/EntryPoint/Group null | 승인된 Production 후보 제작 | 예: Assets/GAME/Prefabs/Enemies/Enemy_Angel_PlaytestV1.prefab 새 GUID로 제작·의존성 검사. Tests prefab 직접 등록 대신 승인된 제작자산 마련 | scene-only 참조·활성 상태·스케일/Collider·Animator 검수 필요 | 제작/표현 및 새 경로 승인 |
| Assets/GAME/Scenes/Dungeon_1_Production.unity / World/Encounters/Encounter_0N/Enemy_0N | HP10/loadout11·12/source없음 | 선정한 개체만 HP14/loadout10003/source Angel | 기존 Scene GO에 컴포넌트/참조 변경 또는 새 Prefab 인스턴스로 교체 중 최소안 선택 | GO 교체는 Trigger/Quest/AI/UnityEvent/local fileID 재연결 필요; 원위치 수정이 참조 위험 작음 | 어느 Encounter를 천사로 바꿀지 승인 |
| 같은 씬의 Encounter_0N Group 및 ContactTrigger | dungeon1.encounter.0N, Group/Entry/Enemy 명시 연결, Contact 적 선공 | 기존 조우 ID/정식 Owner 유지 | Enemy 교체 시 enemyObject와 Group enemies/auto-collect 검수, patrol points/player/motor 재연결 | helper/old enemy를 roster에 중복 포함하면 안 됨 | 보호 씬 승인; Contact 정책 결정 |
| Assets/GAME/Scenes/Dungeon_Template.unity | test IDs/아군 Contact 선공/OpeningEffect/기존 스킬 | 별도 승인 시 canonical template 정리 | Production 적용과 분리. 기존 검증 Quest ID 및 Legacy 사용처 유지 | Template 자동 변환은 기존 검사·콘텐츠에 파급 | 별도 Template 승인 |
| Assets/GAME/Prefabs/CombatRuntime.prefab + Dungeon_1_Production | base BP 및 Scene-added Hand/Clash/BP | 정식 Hand/BP/Clash 초기화만 | 데이터 완비·비활성 검수 후 승인된 순서로 Debug 제거와 설정 전환 | 상속과 added component 혼동, 기존6실패 gate | 보호자산/활성화 별도 승인 |
| Assets/GAME/Prefabs/UI/ProductionDungeonUI.prefab | 기존 router/root/binder/buttons/RewardUI | Owner/연결 유지, 한국어 폰트 일관화 | 아래 폰트 절의 제한적 변경만 | global root를 feature panel이 독립 제어하면 Input/UI 복귀 회귀 | UI 자산 승인 |

신규 자산 추가만으로 준비할 수 있는 것은 승인된 Config/Enemy Prefab의 **비활성 후보 제작**이다. 이것만으로 Production 연결은 성립하지 않는다. Inspector 변경 필수 항목은 Start/Registry/Config/field HP/Enemy source·loadout/조우·순찰 참조다. 추가 코드가 필요한 항목은 초기 스킬 지급·장착 연결, 동적 보상 행 font 선택이다. 미지원 적 능력치는 기능·정책 확정 전 구현하지 않는다.

## Hand/BP/Clash와 콘텐츠 승인 매트릭스

현재 후보는 initialMp2/maxMp3, Normal2/Clash3, BP상한3/주기2/투자3, Clash3/3·상한3·5초, enableProductionHandBp=false다. handSeed0은 저장된 현재값이지 정식 덱 셔플 승인으로 확대하지 않는다. Hand rule마다 skill/kind/copies가 필요하고 동일 key 양쪽 배치는 거부된다. Skill SO에 Normal/Clash enum을 추가할 필요는 없다. Skill key와 매 전투의 카드 RuntimeId는 독립이다. Enemy Hand는 Source Key 명시 opt-in이며 빈 배열을 유지하면 일반 적 authored loadout 정책을 사용한다.

| 정책 | 분류 A/B/C/D | 판단 및 필요한 결정 |
|---|---|---|
| 아난 덱 copies/seed | A+B+D | 기존 handCardRules로 설정 가능. 각각 몇 장/어떤 seed 정책인지 승인 필요. Tests의 Normal2장/Clash3장은 슬롯 승인의 자동 결과가 아님 |
| 초기 보유·장착 | B+C+D | TryAcquire/TryEquip·Save API는 존재. Start SO 초기 스킬 필드는 없고 Production 호출 경계가 비어 있어 새 게임 1회 연결이 필요. 자동 장착/획득 시점·Continue 보존 승인 |
| Contact 선공 | A+B+D | 실제 Production은 적 선공, Template/격리는 아군 선공. Production 기존값 유지가 최소안이나 사용자 정책 확인 필요. FieldAttack의 아군 선공은 별도 경로 |
| 천사 HP/MP/기술 | A+D | HP14는 field component, MP2는 Session config, 기술10003은 loadout. 기존 승인값을 승인 대상 조우에 연결 가능 |
| 천사 회피20%·방어0·캐릭터속도9 | C+B+D | 현재 adapter/Source에 해당 제작 적용 경로 없음. 스킬 speed5를 캐릭터속도9로 치환 금지; 공식/단위/영향을 승인한 별도 기능 필요 |
| Film 획득 | A+B+D | EnemyDefinition.acquirableSkillPersistentKeys로 설정 가능하나 승인 목록 없음. Compatibility 스킬을 Film으로 자동 승격하지 않음. 빈 목록은 현재 보류 표현 |
| 적 AI profile | A+B+D | finalCombatBehaviorProfile 선택 연결 가능. null은 기존 유효 스킬 정책을 유지하며 새 AI 엔진 필요 없음 |
| 천사 표현 | A+B+D | 기존 Animator/Sprite를 참조할 수 있으나 Skill trigger/VFX/SFX 미지정, 이벤트/timeout 구분·스케일·충돌 검증 필요. 기획 승인이 필요한 art/trigger 매핑 |
| Ultimate | B+D | BP 누적 Stage1/2/3 규칙은 유지. 전용 Skill/추가 MP/효과는 Content Pending. 기존 skill2 Debug Ultimate를 정식으로 사용하지 않음 |

## R1 수정 검수와 추가 위험

기존 실제 XML `Logs/CBT-R2-08-P2B3D4E0R1-Regression.xml`를 읽어 **989 PASS / 6 FAIL / 1 IGNORED**, 격리4/4와 WorldEncounter111/111을 확인했다. 이번에 재실행한 결과가 아니다. 격리 Contact Abort는 실제 중첩 유지 후 재시작 없음·분리 후 새 접촉 허용, FieldAttack Victory는 실제 HP320/14, Defeat는 Session HP1 fixture다. 전부 실제 Director를 사용하지만 애니메이션 이벤트와 timeout 각각을 구분한 증거는 없다.

| 검수 항목 | 기존 증거 | 추가 확인 |
|---|---|---|
| Abort 중첩·Exit 재접촉 | 격리 자동 PASS | Production의 실제 지형·AI 이동·입력으로 수동 재검증 |
| Victory clear | 격리 Cleared/중복시작 없음 PASS, 기존 clear 단위 검사 | 실제 dungeon1 IDs의 Quest/Reward/EXP·저장 clear 연결 |
| Defeat | 낮은 HP 종료 fixture PASS, RearmPending 정책 | 정상 HP320 자연 패배·field HP0 후 조작/회복·재전투 정책. RearmPending이 전투 가능 HP를 보장하는 것은 아님 |
| 다른 Encounter | 기존 second-session identity/restore 검사 | 실제 세 조우 연속 실행 및 한 조우 clear가 다른 조우에 영향 없는지 |
| 종료 중복 | 격리 Result1회/기존 Completion 회귀 | Production UI 중복닫기·보상/EXP ledger·Quest event 재전송 |
| 다중 Trigger/Group | collider별 HashSet이 있고 복수 collider 단위 검사 | **같은 Group의 여러 Trigger가 동일 player collider를 공유할 때**, 하나의 Exit/보정이 Group 전체 ID를 제거할 수 있음. Trigger별 참조수는 없음. 현재 각 조우는 Trigger1개이나 향후 복수 Trigger 전환 전 격리 재현 필요 |
| Collider 파괴 | 관찰된 collider만 LateUpdate 검사 | **전투 중 collider가 파괴되면** Unity null 비교 때문에 LateUpdate가 Owner.Unregister 호출을 건너뛰고 list만 제거할 수 있음. Group ID HashSet이 남는 정적 위험. disabled/파괴/교체/respawn 별도 검사 필요. 이번에 수정하지 않음 |
| Save/Load·씬 재시작 | Owner restore가 IDs/lifecycle을 재설정하는 기존 코드 | Trigger 관찰 list는 명시적 restore clear가 없어 실제 load 순서와 stale 관찰 정합성 확인 필요. 디스크 재시작 검사는 이전에도 Ignored |

R1 보류 Exit와 LateUpdate는 기존 모든 Group/독립 Trigger에 적용되는 Runtime 변경이다. “격리 씬에만 영향”으로 취급하면 안 된다. World 복귀는 transform/physics 복원 → unlock → camera → ObserveExploration 순서이고 Unity Enter/Stay/Exit 순서는 collider 활성·body simulation·씬 로드에 영향을 받는다. 이 경계의 추가 검증을 별도 Batch로 선행한다. 위 정적 위험은 실제 Production 재현 완료 결함으로 단정하지 않는다.

## UI/폰트 수정 대상

기존 `Assets/GAME/Scripts/UI/RewardUIPanel.cs`의 AddRewardRow는 TextMeshProUGUI를 생성하고 text/fontSize/color만 지정한다. TMP 기본 font는 `Assets/TextMesh Pro/Resources/TMP Settings.asset`의 LiberationSans SDF이며 global fallback=[]다. LiberationSans의 fallback도 같은 영문 TTF를 쓰므로 `획득 보상 없음`, `전투 승리/패배` 등 한글 보장이 없다. 기존 `Assets/GAME/Fonts/DungGeunMo SDF.asset`(GUID26e27789912c85e4f8fa0644b3537dbf)은 dynamic이고 기존 UI 여러 텍스트가 사용한다. FinalCombatUIBinder.displayFont는 DungGeunMo TTF를 이미 참조한다.

최소안은 RewardUIPanel의 동적 행이 승인된 기존 한국어 font를 명시적으로 이어받게 하는 작은 코드 수정과, `Assets/GAME/Prefabs/UI/ProductionDungeonUI.prefab`의 `RewardCanvas/RewardUI`가 가리키는 titleText/resultText TMP font/sharedMaterial 일치 확인이다. 정적 ResultTitleText 등에 남은 LiberationSans도 대조한다. protected UI Prefab, `RewardUIPanel.cs`는 **후속 승인 대상**, 이번 무변경. 전역 TMP fallback을 바꾸면 모든 씬에 파급되므로 제한적 UI 수정과 동시 시행하지 않는다. 폰트 Asset 자체 변경이 필요하다면 glyph 동적 추가/Player Build 포함을 따로 검증한다.

## 직렬화·Save·복구 계획

CharacterStart는 CP를 참조하지만 HP/MP/초기 Skill/상태 성장값을 담지 않는다. 공식 char.anand_de_morte와 skill.playtest.* key는 영구 저장 식별자다. Save schema14를 바꾸거나 hero.test를 새 ID로 자동 변환하지 않는다. Title/Dungeon 모두 동일 Start를 공급하고 New Game과 Continue를 분리한다. 기존 저장에 없는 새 캐릭터를 Continue에 무조건 추가하거나 장착을 덮지 않는다. 새 캐릭터를 포함한 별도 테스트 Save를 사용해 디스크 재시작을 검증한다.

Prefab 원본과 Scene override를 별도 diff한다. Registry/config는 instance override 우선순위, BP는 inherited/added 여부를 확인하고 Apply All/Revert All을 사용하지 않는다. Enemy GO 교체 시 UnityEvent·local fileID/Collider/Group/patrol/퀘스트 참조를 검사한다. source GUID를 재사용하지 않고 신규 제작 Asset만 Unity Editor에서 새 GUID를 부여한다. 기존 ID·GUID·meta는 유지한다.

실제 적용 전 당시 사용자 dirty/untracked 내용과 Asset 원본 hash/diff를 기록한다. 실패 시 승인 Batch가 만든 구체적인 component/field override만 역으로 복원하고 신규 후보만 분리한다. 저장 파일은 별도 테스트 사본을 사용한다. 저장소 전체 reset·사용자 변경 덮어쓰기·기존 meta 교체는 복구 절차가 아니다.

## 최소 적용 Batch와 수동 통과 기준

1. **정책/데이터 승인**: 덱 copies/seed, 초기 두 스킬 보유·장착 시점, 실제 적용할 Encounter, Production Contact 정책, 표현/스킬speed, Film 보류 유지 여부 결정. 새 비활성 Production Config/Prefab 제작만 별도 승인.
2. **초기 지급·접촉 경계 보강 검증**: 승인 후 기존 새 게임/CharacterSkill Owner 안에서 최소 연결. Continue 불변 및 다중 Trigger·Collider 파괴·Save restore의 R1 위험을 격리 재현/회귀. Runtime 변경이 필요하면 해당 Batch에서 범위 보고.
3. **보호 자산 연결**: 별도 승인 후 Title/Dungeon Start, Player HP Scene override, Registry/Config, 선정된 Enemy source/loadout/patrol/trigger 연결. Debug는 base와 Scene-added를 구분해 제거하며 데이터 검증이 끝나기 전 초기화를 켜지 않는다. Template 정리는 별도 승인 범위로 둔다.
4. **UI 폰트**: 제한적 코드/UI 자산 승인 후 한글 행·제목·material·레이아웃 검증. 글로벌 TMP 정책 변경은 분리.
5. **실제 통합 게이트**: 승인된 대상 씬의 Inspector/Play Mode 및 저장 검증을 완료한 뒤 Cutover 승인 판단. 이 계획 자체는 편집 승인이나 활성화 승인이 아니다.

수동 체크리스트(각 항목 결과/로그/Save 경로 기록):

- 새 게임 Title → Dungeon: 아난1명/리더/편성/level1 EXP0/HP320 MP2, 초기 장착·Registry·덱 수량 확인. Continue는 기존 상태를 덮지 않음.
- 이동/점프/필드공격은 기존 Input Actions로 작동, 천사 순찰·지면·시각 스케일/Collider 확인. Contact는 승인 선공, FieldAttack는 PlayerFirstHit/Allies이며 한 번만 시작.
- Normal/촌하우/Prophecy, MP/카드 discard·중복클릭/stale 거부, 강인도3/3·상한3·5초, Chain과 MP 부족 무대응 확인. 임의 MP 지급 없이 수행.
- Victory/정상 HP에서 Defeat/Abort 각각 실제 Director/Terminal 및 결과1회. 애니메이션 이벤트와 fallback timeout, Pause/재개·중단을 구분 기록.
- 결과 UI 닫기 전 입력 잠금 유지, 닫은 뒤 Exploration/Input/카메라/UI/timeScale/fixedDeltaTime/FieldLock 복구. Collider 활성 그대로 중첩 유지 시 자동 재전투 없음, 실제 이탈·새 접촉은 정책대로 허용.
- 승리 개체 clear 후 재진입 없음, 다른 두 조우는 정상, Quest objective/Reward/참가자별 EXP는 CompletionId당1회. 중복닫기·중복결과도 무변경.
- 모든 한글 행/제목/버튼 글리프·가독성·경고 확인. actual Player Build는 별도 표시.
- 테스트 Save 저장 → 프로세스 종료 → 재시작 Continue: Character/skill key/장착/HP 정책/Encounter clear/Reward ledger 일치, un-cleared 재진입 정책 확인. 테스트 디스크 경로를 Production Save와 분리.

## 검증 상태와 차단 조건

이번 감사 구현 상태: **감사/연결 계획 작성 완료**. 새 기능 구현 없음. 검증 상태: 코드·직렬화 파일 읽기 및 기존 XML 확인; 새 Compile/Inspector/Play Mode/Integration 실행 없음. 기존 05/06A 보류는 유지한다.

이전 6실패는 DungeonOneProductionSceneTests의 Hand Debug 선행 gate5건과 ProductionSceneWiringTests.DungeonTemplate_IsCanonicalSingleOwnerProductionSource의 inherited BP gate1건이다. 이전 기록의 1 IGNORED는 PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners다. 새 실행 결과로 표기하지 않는다.

Cutover 차단: 별도 보호 자산 변경/활성화 승인, 덱/초기 장착 정책 및 초기 지급 연결, 제작 Config 완비, source/loadout/Registry 실제 연결, Debug 상속/added 정리, R1 추가 경계 검사, 폰트/표현·수동 통합, Save/Continue 검증. 미지원 천사 능력치는 Playtest 범위에서 제외할지 정책 승인도 필요하다. 현 상태에서 전체 시스템 완료나 Production Cutover 가능으로 표시하지 않는다.

Development tracking 권장: World/Combat Production Wiring — **Audit Complete / Policy & Asset Wiring Pending / Production Integration Unverified**. Production Owner는 기존 CombatEntryPoint와 Group/Trigger/Core/UI/Input 그대로다. stale 문서는 당시 배치 결과로 보존하고 현재 상태는 본 영향표 및 실제 파일을 기준으로 추적한다.

최종 보존 검사: 작업 전 tracked/untracked 기존 파일 **2,648개 SHA-256 전부 일치**, 추가는 본 보고서1개뿐이다. 기존 tracked diff도 시작과 동일한3파일/59추가줄이다. `git diff --check` 오류 없음(기존 Git CRLF 안내 별도). 이 검사는 컴파일/Unity 실행 증거가 아니다.
