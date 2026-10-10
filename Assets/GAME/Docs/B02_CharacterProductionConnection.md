# B02 — Character / Initial Party / Progression 연결

## 판정과 승인 근거

B02 전체는 **구현 중 — 요구사항 미확정**이다. 승인 정책에 의존하지 않는 시작 설정, 초기화, 단일 플레이어 identity/EXP, 저장 호환 연결을 구현했다. 정식 캐릭터 콘텐츠를 Production Scene에 배치한 작업으로 판정하지 않는다.

- [기획서](https://docs.google.com/document/d/1gfyV1lSm2IXKR9Xx--43ICBXCMj9q5ny0_ekxWZB9e0/edit): 주인공 아난 드 모르테와 동료 설정, 자유 편성/성장 의도는 있으나 저장용 Character ID와 초기 Party 구성의 승인 정의는 찾지 못했다.
- [기능구현 문서](https://docs.google.com/document/d/1JF92rWAxIJ-RNvqJL5lw8CAa_OsVQu61ohmvH9YfVkA/edit): 주인공 HP 320, 정신력 100, AP 3, 공격력 18, 방어력 11, 속도 16, 정밀도 20, 크리티컬 14, 회피 9를 기술하지만, 레벨별 성장/필요 EXP 표와 기존 Combat 모델에 대한 수치 적용 계약은 없다. 이름을 임의의 기술 ID로 변환하지 않았다.
- 현재 제작된 성장 데이터는 `Assets/GAME/Data/Test/CP_TestHero.asset` (`hero.test`, 시작 1/최대 5, 필요 EXP 10/20/30/40)이다. 이 데이터는 테스트 호환 데이터이며 정식 캐릭터 승인 근거가 아니다.
- `GAME002_SkillContent_AuthoringGuide.md`도 Production 캐릭터 진행도 정의를 authoring pending으로 기록한다.
- 실제 사용한 **정식 Character ID와 정식 초기 Party: 없음**. `b02.test.*`, `b02.scene.test.*`는 테스트 중 메모리에서만 만든 데이터이며 저장소 콘텐츠/실제 사용자 저장에는 추가하지 않는다.

## Production 책임과 구현 계약

| 책임 | 기존 Owner | B02 변경 |
| --- | --- | --- |
| 레벨/EXP | CharacterProgressionService | 시작 Definition 등록. 기존 Definition과 저장 state를 보존하고 충돌한 ID는 거부 |
| 멤버/리더/전투 편성 | PartyRuntime | 설정이 있는 첫 초기화와 New Game에서만 초기 Party 적용. 명시적으로 복원한 빈 Party도 유지 |
| 서비스 연결 | RuntimeBootstrapper | 선택적 `characterStartDefinition` 참조. 두 Owner의 설정을 사전 검증한 뒤 연결 |
| 필드→전투 경계 | CharacterPartyCombatAdapter | 설정된 단일 플레이어 편성/Definition/HP 검증. 기존 요청의 flow/configuration은 유지 |
| 전투 진입 | CombatEntryPoint | 기존 단일 진입점 유지. 요청 identity를 기존 정규화 경로로 전달 |
| 지급 | RewardService | 단일 아군 전투 시작 시 고정된 ID를 EXP 대상으로 사용. 기존 idempotent ledger 유지 |
| 저장/복원 | SaveLoadService | 기존 participant 순서와 schema 11 유지. 별도 저장 모델을 추가하지 않음 |

`CharacterStartDefinitionSO`는 기존 `CharacterProgressionDefinitionSO` 참조, 초기 멤버/리더/편성 ID, 기본 EXP 대상만 담는 제작 설정이다. Runtime state나 새 Manager는 아니다. 초기화 API는 같은 설정을 반복 설치해도 진행/편성을 덮어쓰지 않는다. 서로 다른 설정 또는 기존 Definition과 동일 ID의 다른 Asset은 거부한다.

단일 플레이어 경로는 선택된 리더 한 명과 실제 Player 객체를 연결한다. 추가 멤버가 전투 편성에 포함됐지만 field binding이 없다면 전투 진입을 거부하고 encounter reservation을 해제한다. 다인 Party의 필드 객체 배치와 EXP 분배 정책은 이번 작업에서 임의 구현하지 않았다.

EXP는 기존 필요 경험치 표, 시작/최대 레벨, 잔여 EXP 및 최대 레벨 정산 규칙을 따른다. 단일 아군 ID는 session→result→RewardGrantRequest로 전달되므로 전투 중/종료 후 리더나 기본 지급 대상이 바뀌어도 해당 전투의 EXP 대상은 바뀌지 않는다. 미등록 대상은 기존 pending 처리로 보존한다. 레벨에 따른 HP/공격력/방어력 증가를 새로 계산하지 않으며 필드 HP와 기존 Combat 수치 계약을 유지한다.

## Save / Test Hero 호환 정책

- schema는 **11** 그대로이며 B01 Story bool/int Flag와 migration을 수정하지 않았다.
- `hero.test`를 정식 ID로 자동 치환하거나 저장에서 제거하지 않는다. 기존 authored Definition과 기존 공개 API/직렬화 필드를 유지한다.
- 승인 후 공유 시작 설정의 Definitions에는 `CP_TestHero`를 호환 Definition으로 함께 참조해야 한다. 새 초기 Party에 Test Hero를 넣을 필요는 없다. 이렇게 하면 Dungeon 직접 Cold Load에서도 과거 Party leader/lineup의 `hero.test`를 검증하고 기존 레벨/EXP 곡선을 사용할 수 있다.
- 정의가 없는 과거 ID의 레벨/EXP도 기존 Restore 방식으로 보존한다. 그러나 설정된 Party의 실제 전투 진입에는 해당 ID의 Definition과 명시적 field binding이 필요하다.
- Save participant 순서는 기존 Party(225)→Progression(250)→Reward(600)이다. Continue는 저장된 Party를 대체 복원하며 New Game reset을 실행하지 않는다.

## Scene / Inspector 연결

기존 Scene, Prefab, SO, `.meta` 및 GUID는 변경하지 않았다. 새 스크립트와 테스트/문서의 `.meta`만 추가했다. 현재 Title의 `hero.test` Definition/default target과 Dungeon의 미설정 캐릭터 서비스 생성 동작은 유지된다.

승인된 콘텐츠가 준비되면 다음 최소 연결이 필요하다.

1. 승인된 Character ID, 시작/최대 레벨, EXP 표로 기존 CharacterProgressionDefinitionSO를 제작한다.
2. CharacterStartDefinitionSO에 해당 Definitions 및 `CP_TestHero` 호환 Definition을 참조하고 승인된 초기 멤버·리더·편성·기본 EXP 대상을 입력한다.
3. TitleScene과 Dungeon_1_Production의 RuntimeBootstrapper에 **같은 시작 설정 Asset**을 연결한다. Cold Load와 Dungeon 직접 실행도 동일 Definition을 사용할 수 있어야 한다.
4. 단일 Player가 나타내는 Character가 리더이며 단독 전투 편성인지 확인한다. 여러 전투 멤버가 승인되면 추가 field binding/배치 작업을 별도 최소 Batch로 수행한다.

## 검증

- Unity `6000.3.23f1` 유지.
- 첫 집중 실행: 34개 중 27 통과, 기존 성장 테스트 7 실패. 정리 코드가 로드된 영속 CP_TestHero Asset에 DestroyImmediate를 호출한 것이 원인이었다. 영속 Asset 제외 조건 한 줄로 수정했다.
- 집중 재실행: **36/36 통과**, 0 실패/0 건너뜀. 여기에는 Production Scene Play Mode 테스트 2개가 포함된다.
- 첫 전체 EditMode 회귀: **1,271/1,272 통과**, 기존 Film/Unique 통합 테스트 1 실패. 중단 전투의 Reward 닫기 시 World가 캡처 위치를 복원하여, 닫기 전에 수행한 테스트 이동이 덮어써지고 재접촉한 것이 원인이었다. 테스트에서 닫기 직후 안전 위치로 이동하도록 최소 수정했다. Production 전투 규칙/World 생명주기는 변경하지 않았다.
- 해당 Film/Unique 테스트 단독 재검증: **1/1 통과**, 0 실패/0 건너뜀 (`Logs/B02_FilmRegression_Fixed.xml`).
- 최종 전체 EditMode 회귀: **1,272/1,272 통과**, 0 실패/0 건너뜀, Unity exit 0 (`Logs/B02_FullEditMode_Final.xml`, `.log`). B02 신규 20개 EditMode 테스트와 실제 Play Mode를 사용하는 Production Scene 테스트 2개, B01 Story Flag 및 기존 전체 회귀를 포함한다. Unity 컴파일 오류 없음, `git diff --check` exit 0.
- 경고는 오류/실패와 구분한다. 기존 TMP 폰트의 누락 글리프 및 Scene/테스트의 compatibility 경고는 남아 있다. 관련 없는 폰트/UI 정비는 수행하지 않았다.
- 테스트는 임시 저장 경로와 메모리 설정을 사용한다. 실제 사용자 저장 파일, 승인 캐릭터 Asset, 정식 Scene 배치를 검증한 것으로 간주하지 않는다.
- Cold Load 검증은 같은 Unity 프로세스에서 Party/Progression Owner를 실제 제거한 후 재생성하여 수행한다. 별도 Player 프로세스 종료/재실행은 수동 확인 대상이다.

## 수동 Play Mode 절차

1. 승인된 공유 시작 설정을 두 Scene에 연결하고 Title에서 New Game을 진행한다. 멤버 순서/리더/편성, 레벨/EXP가 승인 정의와 일치해야 한다.
2. 처음 전투와 필드 공격 진입을 각각 확인한다. 실제 아군, 선택 캐릭터의 persistent skill, EXP 지급 ID가 일치해야 한다.
3. 레벨 경계 전후 EXP를 지급하고 잔여 EXP와 최대 레벨 처리, 중복 보상 방지를 확인한다. Stats 변화는 승인된 계약 구현 후 별도로 검증한다.
4. 편성/리더를 변경하고 저장한 뒤 Play Mode를 종료한다. 다시 Title→Continue로 시작해 동일 상태가 복원되는지 확인한다.
5. Scene을 왕복하고 동일 저장을 반복 Load한다. 초기 편성 재주입이나 잔존 멤버/EXP가 없어야 한다.
6. 다시 New Game으로 시작해 이전 캐릭터 진행/편성이 초기화되는지 확인한다. 기존 저장 파일은 보존되어야 한다.
7. Dungeon 직접 실행과 기존 hero.test 저장/schema 11 저장을 각각 확인한다. B01 bool/int Flag와 다른 진행 데이터도 유지되어야 한다.

## Development tracking

- System: Character / Party / Progression / Combat integration.
- Feature: 시작 Definition 설치, 초기 Party reset, 단일 플레이어 identity→EXP, 저장 호환 연결.
- Production owner: CharacterProgressionService / PartyRuntime / CharacterPartyCombatAdapter / CombatEntryPoint / RewardService / RuntimeBootstrapper / SaveLoadService.
- Requirement: B02 현재 작업 지시 및 위 기획서/기능구현 문서.
- Implementation: 기반 연결 Implementation Complete. **B02 전체 In Progress — 요구사항 미확정**.
- Compile: 최종 전체 Unity 실행 통과.
- Inspector/serialized assets: 기존 참조 유지, 승인된 시작 설정은 아직 미연결.
- Play Mode / Integration: 실제 Title/Dungeon + 임시 테스트 데이터에서 기반 흐름 검증. 정식 콘텐츠 통합 미검증.
- Remaining: 정식 technical ID, 초기 멤버/리더/편성, 시작/최대 레벨·EXP 표, 레벨별 Stats 및 Combat 적용 계약, 다인 EXP 분배, 정식 공유 Asset 배치, 별도 프로세스 Cold Load.
- Recommended tracking: 기반 연결 완료 / 정식 콘텐츠·성장 정책 확정 및 Production 배치 대기. 전체 Character, Story, Save/Load를 100%로 변경하지 않는다.
- Stale mapping: ProductionInventoryProgressionSetup.md의 기본 target 미설정 권고와 현재 Title의 hero.test 설정이 다르다. Test 호환 데이터와 정식 콘텐츠를 구분해야 한다.

## 수정 / 추가 파일

수정 13개 파일이다. 기존 uncommitted 수정에 B02 변경만 추가했다.

| 경로 | 변경 |
| --- | --- |
| Assets/GAME/Scripts/Core/RuntimeBootstrapper.cs | 선택적 시작 설정과 기존 Owner 설치 연결. B01 Story Flag 설치 유지 |
| Assets/GAME/Scripts/NonCombat/Progress/CharacterProgressionService.cs | 기존 state를 보존하는 시작 Definition 등록 |
| Assets/GAME/Scripts/NonCombat/Party/PartyRuntime.cs | 초기 Party 설정 / New Game reset / 복원 보호 |
| Assets/GAME/Scripts/Combat/Runtime/Adapters/CharacterPartyCombatAdapter.cs | 단일 플레이어 요청 검증/identity 연결 |
| Assets/GAME/Scripts/Player/Runtime/PlayerFieldAttackController.cs | 기존 Adapter 호출, 실패 시 예약 해제 |
| Assets/GAME/Scripts/Combat/Runtime/Integration/CombatEncounterTrigger2D.cs | Contact 요청도 동일 Adapter 호출/예약 해제 |
| Assets/GAME/Scripts/Combat/Runtime/Adapters/FieldCombatantFactory.cs | 실제 단일 아군의 EXP 대상 ID snapshot |
| Assets/GAME/Scripts/Combat/Runtime/Model/CombatSession.cs | EXP 대상 ID의 session 계약 |
| Assets/GAME/Scripts/Combat/Runtime/Model/CombatResult.cs | 결과의 EXP 대상 ID |
| Assets/GAME/Scripts/Combat/Runtime/Core/CombatResultBuilder.cs | session identity를 결과에 전달 |
| Assets/GAME/Scripts/Reward/RewardService.cs | 결과 ID를 기존 RewardGrantRequest에 전달 |
| Assets/GAME/Tests/Editor/InventoryProgressionBatch5Tests.cs | 영속 Character Asset 삭제 방지 1줄 |
| Assets/GAME/Tests/Editor/FilmUniqueProductionPlayModeTests.cs | Reward 후 필드 복원 시점에 맞춘 테스트 이동 |

추가 8개 파일이다.

- Assets/GAME/Scripts/NonCombat/Progress/CharacterStartDefinitionSO.cs 및 .meta
- Assets/GAME/Tests/Editor/CharacterProductionStartupTests.cs 및 .meta
- Assets/GAME/Tests/Editor/CharacterProductionSceneTests.cs 및 .meta
- Assets/GAME/Docs/B02_CharacterProductionConnection.md 및 .meta

삭제/이동 파일 없음. 시작 시 branch `main`, HEAD `3b6a4b5bf2aba77b7efa276fc2cf27abe9263060`, 변경 199개 경로를 확인했다. 최종 작업 범위 밖 190개 기존 경로는 SHA-256이 동일하며 새 범위 밖 변경도 없다. 테스트가 변경한 TMP 폰트 캐시는 검증된 작업 전 백업에서 복원했다. Unity/패키지 버전 변경, commit, push, PR은 수행하지 않았다.

## 완료 보고 16개 항목

| 항목 | 결과 |
| --- | --- |
| 1. 구현 결과 | 승인 정책에 독립적인 시작 설정·초기 Party·단일 플레이어 identity/EXP·저장 연결 완료. 정식 콘텐츠 전환 보류 |
| 2. 읽은 지침 | 루트 AGENTS.md 전체. 적용 중첩 AGENTS.md/AGENTS.override.md 없음. 외부 문서 읽기에 Google Drive SKILL.md 적용 |
| 3. 수정·추가 파일 | 위 수정 13개 / 추가 8개. 삭제·이동 없음 |
| 4. Production Owner | 기존 Progression / Party / Adapter / Entry / Reward / Bootstrap / Save Owner 유지. 새 Manager 없음 |
| 5. 공개 API 영향 | Party와 Progression에 StartDefinition 및 TryConfigureStartDefinition 추가. 기존 API/필드 유지. Adapter에 TryBindSinglePlayerRequest 추가. Session/Result에 읽기 가능한 ProgressionTargetCharacterId 추가 |
| 6. Schema / Migration | schema 11 유지. GameSaveData/Migration/SaveLoadService는 B02에서 변경하지 않음. 기존 EXP ledger target 필드 재사용 |
| 7. Unity Asset 영향 | 기존 Scene/Prefab/SO/GUID/.meta 변경 없음. Bootstrapper에 선택적 직렬화 참조 필드 추가. 승인 후 공유 시작 설정 연결 필요 |
| 8. 실제 테스트 | 집중 36/36, Film 회귀 재검증 1/1, 최종 전체 1,272/1,272. Production Play Mode 2개 포함. diff-check 통과 |
| 9. 미실행 | 승인된 정식 캐릭터/초기 Party/성장 Stats의 Inspector·Play Mode·통합. 별도 Player 프로세스 재시작 Cold Load |
| 10. 수동 테스트 | 위 7단계 절차. 승인 정의/동일 공유 Asset을 두 Scene에 연결한 뒤 실행 |
| 11. 기존 저장 호환성 | hero.test 키/Definition 유지, schema 5/10/11 테스트, B01 Flag 보존. 새 공유 설정에도 legacy CP_TestHero 참조 필요 |
| 12. 관계없는 변경 보존 | 작업 전 199개 중 범위 밖 190개 해시 동일, 범위 밖 신규 변경 없음. 폰트 캐시 원본 복원 |
| 13. 남은 위험/후속 | 공식 technical ID, 초기 구성, 성장/EXP/Stats 계약, 다인 field binding/EXP 분배, 실제 Asset 배치 승인 필요 |
| 14. 구현 상태 | 기반 연결 Implementation Complete / **B02 전체 In Progress — 요구사항 미확정** |
| 15. 검증 상태 | 기반 연결 Compile + 임시 데이터 Production Play Mode/Integration Verified. 정식 콘텐츠 Inspector/Play Mode/Integration Unverified |
| 16. 개발현황 권장 | 기반 연결 완료와 공식 콘텐츠 대기를 구분. B02/Character 전체 및 Save/Load 전체를 100% 완료로 기록하지 않음 |
