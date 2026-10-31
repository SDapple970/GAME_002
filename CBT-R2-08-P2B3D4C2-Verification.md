# CBT-R2-08-P2B3D-4C2 — 아난 초기 Hand 및 최초 Clash

실제 아난 Skill SO 두 개를 기존 Skill snapshot·Hand·Combat 흐름에 공급해 격리 Play Mode에서 최초 대응과 후속 Clash 공격을 확인했다. Production 자산 연결·Cutover는 수행하지 않았다.

## 후보 구성

- Character ID: `char.anand_de_morte`. 테스트에서 기존 CharacterSkillRuntime의 획득/장착 API로 실제 두 Skill의 persistentKey를 등록했다. 이것은 fixture 상태이며 New Game의 자동 획득/장착 데이터가 작성된 것은 아니다.
- Normal: `Assets/GAME/Data/Skill/Skill_AnandBasic_PlaytestV1.asset`, ID10001, `skill.playtest.anand.basic`, Final MP0.
- Clash: `Assets/GAME/Data/Skill/Skill_Zornhau_PlaytestV1.asset`, ID10002, `skill.playtest.anand.zornhau`, Final MP1, 위력3, 피해4.
- 신규 후보: `Assets/GAME/Tests/Editor/Fixtures/CombatPlaytestV1/AnandHand_C2_TestOnly.asset`, GUID `f461f3f5e5c322e4db2ee3bb71d5bfcd`. 기존 FinalCombatRuntimeConfigSO 타입의 **비활성 테스트 전용** 복사본이며 실제 Skill SO를 참조한다. Production에는 연결하지 않는다.
- `handCardRules`: Normal 기본 공격 copies2, Clash 촌하우 copies3. **2/3은 슬롯을 완전히 채울 수 있는지 확인하는 fixture 수량이며 정식/Production 덱 수량 승인이 아니다.** seed0도 fixture다. 기본 Config는 그대로다.
- 후보의 실제 필드: Normal capacity2, Clash capacity3, initialMp2, 강인도3/3, 최대 비교3, 선택5초, `enableProductionHandBp=false`. 테스트에서만 메모리 복제본을 enable했다. Enemy Hand source 목록은 비어 있어 기존 authored skill 경로를 사용했다.

Hand capacity는 동시 보유 상한이며 deck copies와 별도다. 같은 Skill SO의 여러 장은 각기 다른 RuntimeId를 가진다. 초기화 시 종류별 draw, 사용 시 해당 종류 discard, 이후 기존 cycle refill에서 draw pile 소진 시 discard를 seeded shuffle해 재사용한다. 따라서 서로 다른 Skill SO 5개를 추가할 필요는 없다. 실제 총 copies/seed·초기 획득/장착은 별도 결정이다.

## 격리 Play Mode

새 `AnandFirstClashPlayModeTests`는 빈 Scene에서 EnterPlayMode 후 기존 EntryPoint/Session/Orchestrator 및 B3B 임시 참가자 fixture를 사용한다. 기존 Director의 표현 gate는 수동 완료하여 비용/상태 경계를 제어했다. 실제 Dungeon 씬·Production UI를 실행한 것이 아니다.

- 후보 asset의 스킬 참조, 비활성, 2/3 슬롯, MP2, 강인도3/3·최대3·5초: PASS.
- 적 선공: 실제 촌하우를 **최초 대응**에 선택. MP2→1, 해당 카드 discard1회, 동일 요청 재전송 거부. 비교 완료로 MP가 다시 차감되지 않음.
- 같은 상황의 두 번째 촌하우 대응은 MP1→0. 세 번째 대응 시 남은 카드는 손패에 유지되고 명령이 거부되며 기존 `SubmitClashSeriesNoResponse`를 1회 처리하고 중복 요청은 거부했다. 상대의 세 번째 공격을 재현하기 위해 상대 스킬 MP0은 **임시 적 fixture에서만** 사용했다. 양측 시작 MP를 3으로 올리지 않았다.
- 아군 선공: 실제 Normal 카드로 선언해 MP2 유지·discard1회. 첫 비교 후 첫 Clash 공격 선택으로 촌하우를 제출하고, 두 번째 촌하우까지 각각 MP1을 소비해 MP0에서 3회 비교가 끝난다. 최종 피해4, 중복 outcome 거부, ChainDecision 확인.
- 기존 계약상 최초 공격 선언은 Normal 카드이며, 촌하우를 선언 단계 Normal 카드로 사용하는 것은 허용되지 않는다. 여기서 ‘첫 Clash 공격’은 첫 비교 후 최초 ClashAttack 선택을 뜻한다. 최초 **대응**에는 Clash 카드를 사용한다.
- Abort 후 Session/결정 요청 정리와 timeScale1·fixedDeltaTime 보존을 검사했다. 이 새 테스트는 수동 presentation gate를 사용하므로 실제 Director 슬로모션 취소나 FieldLock 복구를 이 두 스킬로 직접 입증하지 않는다. 기존 D2 실제 Director/FieldLock fixture 회귀는 별도로 실행한다.

기존 D4C의 실 SO 격리 검사에서 카드 instance 구분·장착 변경 후 snapshot 불변 및 중복 token을 확인했다. 기존 Terminal/Reward/EXP 검사는 fixture 데이터이며, 새 두 Skill SO로 Terminal을 직접 검증한 결과는 아니다. 동일 Skill 양쪽 배치 및 잘못된 ID 검증은 기존 초기화 회귀로 확인한다. 실제 Animator/VFX/SFX 및 촌하우 speed 기본값5는 승인/연결 Pending이다.

Unity 6000.3.23f1 신규 집중 검사 **3/3 PASS** (`Logs/CBT-R2-08-P2B3D4C2-Focused3.xml/.log`). 기존 선택 범위와 새 검사를 합친 회귀 **871 PASS / 6 FAIL / 1 IGNORED**, 총878 (`...-Regression.xml/.log`, 범위 `...-TestFilter.txt`). 기존 D4C XML과 실패 이름을 대조해 동일6건이다: Dungeon Hand Debug 선행 gate5건, Template의 상속 BP Debug gate1건. 모두 **Asset Change Required** 유지. Ignored는 별도 Editor 재시작 검사 `PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners`다.

기존 P2B3A22/B15/C23/D1 8/D2 14, 초기화40 및 Legacy 관련220/220 PASS. 새 후보·테스트 코드의 Unity 컴파일에서 CS 오류/경고 없음. 전체 회귀·실제 Production Scene Play Mode·Player Build·디스크 재시작은 수행하지 않았다. 신규 Editor 테스트의 조건은 실제 스킬 사용을 검증하며 fixture 적의 비용·수량을 정식 적/덱으로 승인하지 않는다.

## 변경 및 후속 연결

루트 `AGENTS.md` 적용, 중첩 없음. branch `main`, HEAD `b42544824838e7de0e70b71afc2ca21a59db8192`; D4B/D4C 등 기존 미커밋 자산 보존. 추가: 위 TestOnly SO/meta, `Assets/GAME/Tests/Editor/AnandFirstClashPlayModeTests.cs` 및 meta(GUID `cafeecfa969165749884c27d92791543`), 이 보고서. Runtime API/SerializeField/기존 GUID 변경 없음. 새 후보는 Tests 경로이며 Production에서 직접 참조하지 않는다.

시작 기록 기존 파일 2,625개 해시 동일, 두 새 GUID 중복 없음. 보호 Scene/Prefab/Production SO 및 사용자 변경은 보존했다. 신규 텍스트 공백 검사와 `git diff --check` 통과(기존 Git 줄바꿈 안내 별도). commit/push/PR 없음.

별도 승인 후 기존 CombatEntryPoint Registry에 실제 두 SO를 등록하고, 아난 ID의 초기 획득·장착 경로 및 실제 Production Config에 승인된 Normal/Clash copies/seed를 작성한다. 실제 CharacterStart·field HP 및 UI/Input/Director/Reward 연결도 확인해야 한다. 테스트 후보의 숫자를 자동으로 Production에 복사하지 않는다.

D4D 천사 전투 연결에는 이미 승인된 `enemy.chapter1.angel` source를 실제 적 root에 연결하는 별도 자산 승인, 적 HP/스킬/MP/AI 및 Film mapping 결정, 기존 enemy loadout·Registry 연결, 적 손패 적용 여부 및 Inspector/Play Mode 검증이 필요하다. Enemy source SO는 HP/loadout 자체가 아니다.

## Development tracking

- System: Combat Hand / initial Clash; 기존 CombatEntryPoint, Session, StateMachine, Orchestrator, SkillRunner 및 CharacterSkillRuntime 소유.
- Implementation: 실제 스킬의 테스트 전용 Hand 후보 및 격리 Play Mode 검증. 실제 Production 획득/장착·Registry/Config 연결 Pending.
- Compile/automatic: 아래 실제 실행 기록. Inspector/Production Play Mode/실제 통합 Unverified.
- Recommended: **Initial Hand Isolated Play Mode Verified — Card Counts / Content / Production Wiring Pending**. CBT-R2-05/06A 수동 보류와 LegacyPlanning 유지.
