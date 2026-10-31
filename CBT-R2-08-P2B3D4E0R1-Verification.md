# CBT-R2-08-P2B3D-4E0R1 검증

## 구현 및 재현

최종 `Assets/GAME/Tests/PlayMode/AnandAngel_IsolatedPlaytest.unity`에서 Collider를 끄는 D4E0 우회를 제거하자 Abort 결과 UI 종료 후 같은 전투가 자동 재시작됐다 (`Logs/CBT-R2-08-P2B3D4E0R1-Repro.xml`, 1 PASS / 1 FAIL).

전투 formation/field lock이 만든 물리 Exit가 접촉 기록을 제거하고, 복귀 시 Encounter Owner가 접촉 영역을 이미 벗어났다고 판단한 것이 원인이다. 기존 CombatEncounterGroup과 독립 CombatEncounterTrigger2D의 UnregisterPlayerCollider에서 ActiveCombat/AwaitingPostCombat 및 Exploration 이전 RearmPending의 Exit를 보류했다. Trigger는 이미 관찰한 Collider만 보관하고, RearmPending/Exploration의 LateUpdate에서 실제 모양의 분리를 검사해 Owner에 이탈을 알린다. 이는 물리 Exit가 다시 오지 않아 생길 수 있는 영구 잠금도 방지한다. 실제 이탈 후 Owner의 기존 Idle/예약 경로를 재사용한다.

새 EntryPoint·Manager·Input/UI Owner는 없고 CombatEntryPoint → 기존 CombatSession/StateMachine/Flow → Director → Result → Reward UI → GameFlowController → Exploration을 유지했다. Collider 강제 비활성화, 적 무조건 삭제, 영구 재조우 금지, 정책/자산 우회는 사용하지 않았다.

## 시나리오 및 증거 수준

자동 테스트는 Unity Editor에서 EnterPlayMode로 실제 최종 격리 씬을 연다. 테스트 코드는 Transform 이동, 기존 PlayerFieldAttackController.RequestPrimaryAttack, 전투 명령, Reward close Button을 호출한다. 실제 사용자 키보드/마우스 수동 Play Mode는 수행하지 않았다. Director의 완료 이벤트나 TryComplete를 테스트가 직접 호출하지 않는다. 애니메이션 이벤트와 Director timeout 중 각 연출이 어느 방식으로 끝났는지는 구분해 검증하지 않았다.

- Contact/Abort: 실제 Collider 접촉과 아난 기본 공격·촌하우, MP 소비, 실제 Director를 통한 Chain, 강제 Abort 요청, Result 1회, Reward UI 닫기, 중첩 유지 1초 동안 재시작 없음, 실제 이탈 후 Idle, 새 접촉으로 재진입을 검사한다. Collider는 계속 활성이다.
- Field Attack/Victory: 실제 필드 공격의 windup/hitbox 경로로 PlayerFirstHit/Allies 시작, HP 320/14에서 승인된 실제 스킬을 사용해 승리까지 진행한다. Terminal/결과 1회, Reward UI 및 Cleared, 복귀 후 재시작 없음을 검사한다.
- Enemy Contact/Defeat: 씬을 저장하지 않고 살아 있는 Trigger의 StartReason/initiative를 PlayerGotHit/Enemies로 바꾼 격리 fixture다. 승인 HP 320/14 시작을 확인한 후 **Session 아군에 319 피해를 적용해 HP1로 만드는 종료 fixture**를 사용한다. 이후 실제 The Prophecy 피해와 Director/Terminal로 Defeat를 검사한다. 정상 HP320의 자연 패배 밸런스를 검증한 것은 아니다.
- 종료 검사: GameState Exploration, AllowsExplorationInput, FieldLock 해제, Camera IsInCombatMode=false, timeScale1, fixedDeltaTime 원복. 입력 장치의 실제 이동/표시 품질 검증은 아니다.
- Group/독립 Trigger 양쪽의 synthetic exit 보류 및 실제 Exploration exit 이후 재예약 허용 단위 검사를 추가했다. 기존 WorldEncounterIntegrationTests의 다중 Collider·승리 clear·다른 세션 identity·보상 등 회귀도 선택 범위에 포함했다.

## 기존 씬 설정 주의

격리 Scene/Prefab/SO는 이번에 수정하지 않았다. 현재 씬 Contact의 authored 값은 PlayerFirstHit/Allies이며 Enemy Contact 검사는 메모리 override로 분리했다. Contact를 항상 적 선공으로 만들지 여부와 그 Inspector 변경은 별도 확인 대상이다. FieldAttack는 PlayerFirstHit/Allies다. 실제 다른 필드 조우 개체를 배치한 재사용, 플레이어 입력 이동·순찰·지면 충돌의 수동 검증은 남아 있다.

## 한글 글리프

`RewardUIPanel.AddRewardRow`가 TextMeshProUGUI를 동적 생성하면서 font를 지정하지 않는다. 따라서 TMP Settings의 기본 LiberationSans SDF(`8f586378b4e144a9851e7b34d9b748ee`)를 사용한다. 해당 fallback도 LiberationSans 원본 TTF를 사용하는 동적 폰트이고 전역 fallback은 빈 배열이다. 결과 문자열 `획득 보상 없음`의 획/득/보/상/없/음에서 missing glyph/사각형 대체 경고가 발생했다. 기존 `Assets/GAME/Fonts/DungGeunMo SDF.asset`은 동적 한글 폰트이고 ProductionDungeonUI의 여러 텍스트가 이미 사용한다.

최소 후속안: 동적 reward row가 기존 한국어 UI의 font를 명시적으로 이어받게 하고, 결과 UI의 LiberationSans 참조를 기존 DungGeunMo로 통일하거나 승인된 TMP fallback 정책을 연결한다. 보호 대상 `Assets/GAME/Prefabs/UI/ProductionDungeonUI.prefab`의 RewardUI/ResultTitleText 및 관련 텍스트, TMP Settings와 폰트 Asset 영향이 있으므로 이번에는 수정하지 않았다. Font 표시 문제는 Pending이다.

## 지침·Git·보존

루트 AGENTS.md 적용, 중첩 없음. main / b42544824838e7de0e70b71afc2ca21a59db8192. ProjectVersion은 실제 6000.3.23f1이며 Editor도 같은 버전을 사용했다. Unity/Package, 공개 API/SerializeField, 기존 GUID 및 모든 Scene/Prefab/SO를 유지했다. 새로운 meta/GUID도 없다.

수정: CombatEncounterGroup.cs, CombatEncounterTrigger2D.cs, WorldEncounterIntegrationTests.cs, 기존 untracked AnandAngelIsolatedSceneTests.cs. 추가: 이 보고서. 삭제/이동 없음. 작업 전 2,647파일 SHA-256을 기록했고 위 의도한 코드 4개 외 기존 파일 보존을 확인했다. Quest/Reward/Save 계약 및 Legacy 호환, 사용자 데이터, 05/06A 보류, TestOnly 덱 상태를 유지한다. commit/push/PR 없음.

## 최종 자동 검증

Unity 6000.3.23f1 최종 컴파일 C# 오류/경고 없음. 최종 씬·Prefab 해시 그대로 유지한 상태에서 `Logs/CBT-R2-08-P2B3D4E0R1-Regression.xml`의 선택 회귀 **989 PASS / 6 FAIL / 1 IGNORED**, 총996이다. 기존 D4E0 범위881건에 격리 검사4건 및 WorldEncounterIntegrationTests111건을 추가했다. 격리4/4 및 Encounter111/111 통과, 기존881 범위는 **874 PASS / 6 FAIL / 1 IGNORED**로 동일하다. 전체 회귀나 Player Build가 아니다. 필터는 `Logs/CBT-R2-08-P2B3D4E0R1-TestFilter.txt`, 컴파일/런타임 로그는 같은 접두사의 Regression.log다.

4개 격리 검사: CandidateHasAuthoredSourceAndLoadoutWithoutScenePatrolReferences, ContactStartsCombatFromIsolatedScene, EnemyContactDefeat_LowHpSessionFixture, FieldAttackVictory_ActualSkillsAndDirector. 테스트가 수동 연출 완료 이벤트를 호출하는 경로는 이 4개 검사에 없다. 기존 선택 범위에는 별도의 수동 presentation fixture도 포함되므로 전체989건을 실제 Director 검증으로 해석하면 안 된다.

기존 실패 이름 집합은 이전 XML과 동일하다. 아래 다섯 개는 `Game.Tests.Integration.DungeonOneProductionSceneTests`의 `Forbidden ... CombatHandValidationOptIn on CombatRuntime` 선행 검증 오류다.

1. ProductionDungeonOne_HasCanonicalCombatRewardUiCameraAndBootstrapOwners
2. ProductionDungeonOne_HasExplicitInteractionOwnersWithoutLegacyInteractionPaths
3. ProductionDungeonOne_HasOneCanonicalRepeatableNpcInteraction
4. ProductionDungeonOne_HasQuestDerivedCompletionContractWithoutLegacyExitOrFallbackDestination
5. ProductionDungeonOne_IsCleanEnvironmentMigration

여섯 번째 `Game.Tests.Integration.ProductionSceneWiringTests.DungeonTemplate_IsCanonicalSingleOwnerProductionSource`는 기존 Template의 상속 BP Debug gate다. 모두 Asset Change Required로 유지하며 테스트를 삭제·Skip·기대값 완화하지 않았다. IGNORED는 `PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners`로 별도 Editor 재시작 파일이 필요한 기존 검사다.

`git diff --check` 오류 없음, 기존 및 수정 파일의 Git CRLF 변환 안내만 별도 출력됐다. 한글 missing glyph 경고와 기존 빈 Tests.asmdef 안내는 컴파일 오류와 구분한다. 최종 해시 재검사에서도 의도한 코드4개 외 기존 파일은 모두 일치했다.

## 남은 검증과 연결 조건

별도 수동 순서: 격리 씬 단독 시작 → 플레이어 이동/천사 순찰/지면 → Contact와 FieldAttack 각각 → 양측 선공·MP/카드·합/Chain → Victory/Defeat/Abort → 결과 UI 닫기 → 중첩 상태에서 자동 재시작 없음 → 이탈/재접촉 → 카메라/입력/UI/시간/잠금 복귀. 화면 가독성, 실제 애니메이션 이벤트, 다른 조우, Quest/EXP 수령·Save 디스크 재시작도 별도로 확인해야 한다.

회피20%/방어0/캐릭터 속도9의 Feature Gap, 정식 덱 수량/초기 스킬 지급/Film mapping, 실제 Production Debug 제거와 Config 활성화는 Pending이다. D4E-1의 **연결 계획 검토는 가능**하나 보호 자산 편집·Production Cutover 진입은 별도 승인, 위 콘텐츠와 Contact 정책, Inspector/수동 검증 충족이 필요하다.

## Development tracking

- System: World Encounter / Combat return.
- Requirement: D4E0R1 natural overlap / Contact / FieldAttack / terminal recovery.
- Production owner: existing Encounter Group/Trigger, CombatEntryPoint, GameFlowController, UI/Input owners.
- Implementation: contact-latch fix implemented; isolated scene automated scenarios extended.
- Inspector: source/serialized inspection only; manual verification pending.
- Play Mode: automated isolated evidence only; manual input not run.
- Integration: Production unverified; no Cutover.
- Recommended: Implementation Complete — Automated Isolated Verification / Manual Production Validation Pending.
