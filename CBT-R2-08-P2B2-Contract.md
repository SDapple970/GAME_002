# CBT-R2-08-P2B2 — Hand/BP 설정 계약 및 데이터 승인 매트릭스

2026-10-29 KST. 범위: 정식 설정 계약 조사·안전성 재검증·P2-B3 준비. **Production 활성화 및 보호 Scene/Prefab/SO 편집은 수행하지 않았다.**

## 근거와 현재 상태

- 활성 지침: 루트 `AGENTS.md`, 중첩 지침 없음. Branch `main`, HEAD `6fa26d294472574097d33b884858ba0ee8bdc8b7`. Unity ProjectVersion 6000.3.23f1.
- 설계 기준: [GAME_002_CURRENT_HANDOFF](https://docs.google.com/document/d/1I8QXiwpWDuHX8VTqGqJjAGA-HhWB1lTDKD_62LgjPhk/edit) D04/D08/D12~D15 및 4절, [GAME_002_기능구현](https://docs.google.com/document/d/1JF92rWAxIJ-RNvqJL5lw8CAa_OsVQu61ohmvH9YfVkA/edit) 「새로운 전투」 §12 등. 인계 문서는 2026-10-21 수정, 기능구현은 2026-10-24 수정된 현재 문서를 읽었다. 과거 코드 구현 상태를 말하는 인계 문구는 현재 코드보다 오래된 이력으로 취급한다.
- 현행 구현: `CBT-R2-08-P2B1-Verification.md` 및 `FinalCombatRuntimeConfigSO`, `CombatEntryPoint.InitializeProductionHandBp`, `CombatSession`, `CombatHandRuntime`, `CombatBpRuntime`, `CombatUltimateRuntime`을 직접 확인했다. Config에 기존 필요한 필드가 있어 이번에 추가 필드/Runtime 수치/새 Owner를 만들지 않았다.
- `FinalCombatRuntimeConfig_V1.asset`에는 새 opt-in 작성 데이터가 없고 기본 false다. `Dungeon_1_Production`의 EntryPoint가 기존 Config를 참조한다. 실제 Hand/Clash/BP Debug opt-in은 여전히 Scene/prefab에 있다. P2B1 선택 655 PASS/6 FAIL/1 IGNORED는 이전 결과이며 이번 실행은 아래에 별도 기재한다.
- 현재 요청은 3/2 손패를 **기능 테스트 제안**으로 명시한다. 값이나 일반 기획 문서의 구상을 실제 Production 승인으로 추정하지 않는다.

## 설정 필드 및 데이터 승인 매트릭스

분류: Approved(현재 지시로 확정된 정책), Existing Runtime Rule(현 코드), Proposed(기획/인계 제안), Test Fixture Only, Content Pending, Contract Pending. 런타임 수치와 설계상 제안은 각각 표시한다.

| 데이터 / Config·SO 필드 | 현재 코드·기획 근거 | 상태 / Production 적용 가능 여부 / 필요한 결정 |
|---|---|---|
| 초기 합 `clashHandCapacity`, 일반 `normalHandCapacity` | Config 기본 각 0, 활성화 시 둘 다 0은 거부; 인계 4절은 합3/일반2 기능 테스트 프로필 | **Proposed / Test Fixture Only**. 공식 슬롯 수 승인 전 활성화 금지. 0 값 자체는 한 종류에 허용되나 해당 참가자에게 draw 가능한 카드가 최소 하나 필요 |
| 시작 사용 여부 `enableProductionHandBp` | 기본 false; 기존 Asset에는 필드 작성 없음 | **Existing Runtime Rule / Approved 안전 제한**. false 유지; 값/연결 승인 뒤에만 P2-B3 활성화 |
| 장착 snapshot | CharacterSkillRuntime → PersistentSkillCombatLoadoutBridge → CombatStartRequest → FieldCombatantFactory. unique Character ID와 비어 있지 않은 ally snapshot이 활성 조건 | **Existing Runtime Rule / Content Pending**. 공식 캐릭터 ID·Film/Unique/스킬 매핑 필요. 기존 hero.test를 공식 데이터로 치환하지 않음 |
| 카드 분류·중복 `handCardRules[].skill/kind/copies` | 등록 Skill SO reference 당 한 rule, Normal 또는 Clash, copies 양수. 동일 Skill의 복수 instance는 같은 rule copies로 생성 | **Existing Runtime Rule / Contract Pending**. 인계 4절은 허용된 instance의 중복만 제안. 동일 스킬을 Normal과 Clash 양쪽에 배치할 필요가 있다면 현재 단일 rule 계약과의 차이 결정 필요 |
| 초기 Draw·seed `handSeed` | HandRuntime 생성 시 종류별 seeded shuffle 후 capacity까지 draw; seed + roster index. 구체 seed 미승인 | **Existing Runtime Rule / Contract Pending**. 재현성·난수 정책 및 제작 seed 승인 필요. 코드 기본 0을 정식 값으로 간주하지 않음 |
| discard/refill | 승인 시 해당 instance discard; 양 진영의 성공한 공격권 cycle 완료 후 refill; draw 소진 시 해당 종류 discard만 shuffle | **Existing Runtime Rule / Proposed**. 인계 4절·D12와 부합하는 기술 경로이나 테스트 제안의 기획 승인과 실제 Production 검증은 별개 |
| 부족 덱/빈 손패 | HandRuntime은 있는 카드까지만 draw. 활성 시작 검증은 각 참가자에 drawable 카드 하나 이상 요구; 전투 중 빈 손패 가능 | **Existing Runtime Rule / Contract Pending**. 인계 D08은 무료 상시 Clash 카드 비지급과 무대응을 제안. 활성 시작 시 완전히 빈 deck을 허용할지 별도 결정 필요 |
| 재전투/인계 | 새 Session과 재생성된 snapshot, 세션 내 actor별 Hand 유지. 인계는 refill/재생성을 자체 수행하지 않음 | **Existing Runtime Rule / Proposed**, 인계 D14. 자동 fixture 검증과 실제 다인 Production 검증을 구분 |
| 적 Hand | P2B1 활성 시 allies **및 enemies 전원**에 rule·Hand 필수 | **Contract Pending**. 인계 D15는 적 Hand 별도 opt-in 제안. 현 Config에는 적만 끄는 필드가 없고 현재 코드로 그 정책을 표현할 수 없음. 승인된 정책과 최소 변경 범위 결정 전 활성화 보류 |
| 카드 인스턴스 ID | CombatSession 내 증가하는 RuntimeId, persistentKey는 Skill/저장 정체성 | **Existing Runtime Rule**. 재전투 시 별도 Session이므로 ID만으로 이전 명령을 식별하지 않고 세션·version과 함께 확인 |
| 초기 BP | CombatBpRuntime 생성 시 actor별 0; Debug 전용 initialPlayerBp는 별개 | **Existing Runtime Rule / Proposed**, 인계 D13 초기0 제안. 공식 승인·재전투 검증 필요; Debug 초기3을 정식 값으로 이전하지 않음 |
| BP 최대 `bpMaximum`, 주기 `bpRoundCyclesPerGain`, 행동 상한 `bpMaximumInvestment` | 활성화 시 양수 필수. 인계 D13은 상한3, 양측 유효 cycle 기준2회, 0~3 투자 기능안. 기능구현 §12는 2턴당 획득 구상 및 행동당 최대3 기술 | **Proposed / Contract Pending**. ‘턴’과 유효 RoundCycle의 의미 및 실제 수치 승인 후 작성. 코드에서 2턴을 기존 Turn과 동일시하지 않음 |
| BP 획득 대상 | 양 진영 유효 AttackCycle 쌍이 주기 완료 시 양쪽 생존 actor에 +1, HP≤0/기절 제외, cap 적용 | **Existing Runtime Rule / Contract Pending**. D13은 활동 중 생존 편성원·KO/기절 제외 권장, 적·Panic 획득 여부 확정 필요. 현재 GrantIfActive는 Panic을 별도로 제외하지 않음 |
| BP 승인 소비 | 명령 승인 단계에서 MP/카드/BP 검사 및 commit, 발행된 receipt만 Ultimate에 1회 반영; stale/중복 거부 | **Existing Runtime Rule / Proposed**, 인계 D04/D13. 실제 실행과 비용·중복 검증 필요 |
| 궁극기 단계/리셋 | Session당 stage 0 시작, 승인 BP 소비 누적 최대3, stage3 실행 후 reset. 기절/Panic/사망/Terminal 및 권한 제한은 기존 StateMachine | **Existing Runtime Rule / Proposed**, 인계 D13과 기능구현 §12의 3단계. 정식 비용·게이지 비율·대상·취소 정책 승인 필요 |
| `ultimateSkills[]` 및 Skill `BpEnhancementKind/BpEnhancementPerPoint` | Ultimate는 optional registry SO 목록, 참가자 보유 스킬 중 최대1개. enhancement는 기존 Skill SO 값 참조 | **Content Pending**. 공식 Ultimate Skill/Effect/MP·강화 종류/계수·적 사용 정책 없음. 빈 목록은 오류가 아니며 발동 가능 데이터도 없음 |
| 반복형 합 config / timeout | P2B1 Hand/BP 활성화 시 Clash Debug opt-in도 건너뜀. 별도의 정식 반복 합 config/callback 미구현 | **Contract Pending / Blocked**. 정식 반복 합 설정·승자/timeout selector를 마련하지 않은 상태에서 Hand/BP만 활성화하면 기존 Debug 합 경로가 사라진다 |

설정값 승인·콘텐츠 제작·실제 연결은 별도 결정이다. 이 매트릭스는 Skill 이름, `skill.production` key, Debug 수치를 정식 승인 증거로 취급하지 않는다.

## 필수 데이터 및 유효성

활성화 시 최소한 공식 Character ID와 장착 snapshot(각 아군), Enemy의 유효한 snapshot, 해당 스킬 전부에 대한 등록된 SO 규칙·정체성·kind/copies, 비음수 capacity와 drawable 카드, 양수 BP 상한/주기/투자 상한이 필요하다. `ultimateSkills`는 선택 사항이다. `handSeed`는 integer 값이며 별도의 승인/명시 여부 표식은 없다. MP·posture 등 기존 Config는 별도 기존 검증을 통과해야 한다.

EntryPoint는 중복/누락 Character ID, 빈 snapshot, 미등록/중복 key/rule, 잘못된 kind·copies·BP 값, 선택 불가능한 전체 손패, 중복 세션을 시작 전에 거부한다. 전체 actor 검사 후 Hand→BP→Ultimate 순서로 적용하며 실패 시 부분 상태를 폐기한다. Production 성공 표식이 있으면 Debug Hand/BP/Clash 자동 주입은 건너뛴다. 비활성 시에는 기존 Debug 경로가 유지된다.

주의: Config에는 **정책 승인 상태를 직렬화하는 기능과 에디터 사전 승인 판정기**가 없다. 자동 시작 검증은 구조적 유효성만 확인하며 승인 여부를 증명하지 않는다. 별도의 콘텐츠 승인 절차가 필요하다. 현재 한 Skill의 Normal/Clash 이중 분류, 적 Hand 선택, 정식 Clash 설정은 기존 필드만으로 표현하기 어려우므로 정책 확정 후 최소 구현 범위를 별도로 결정한다. 이번에 필드나 validator를 추측해 추가하지 않았다.

## P2-B3 Inspector 및 자산 전환 가이드 — 미실행

1. 승인된 정책·공식 Character/Skill/Enemy 데이터를 준비하고, 기획 D15의 적 opt-in 및 반복 합 설정에 대한 구현 차이를 해소한다. 필드 작성 자체가 정책 승인 증거는 아니다.
2. 편집 직전 사용자 미커밋 상태를 포함한 Scene/Prefab/SO/meta 사본·해시·GUID·Inspector/Prefab override·UnityEvent 및 C# 구독을 기록한다. 원본을 HEAD로 복원하거나 사용자 변경을 버리지 않는다.
3. 별도 승인된 작업에서 `Assets/GAME/Data/Combat/Runtime/FinalCombatRuntimeConfig_V1.asset`의 위 필드를 승인값으로 작성한다. `Dungeon_1_Production/Runtime/Combat/CombatRuntime`의 CombatEntryPoint `finalExchangeRuntimeConfig`가 그 SO를 참조하는지 확인한다. 현재 prefab과 Scene registry/override 차이도 확인한다.
4. 변경 대상은 `Assets/GAME/Prefabs/CombatRuntime.prefab`의 CombatRuntime/BP Debug 원본, `Assets/GAME/Scenes/Dungeon_1_Production.unity`의 Runtime/Combat/CombatRuntime에 추가된 Hand/Clash/BP Debug 컴포넌트, `Assets/GAME/Scenes/Dungeon_Template.unity`의 prefab instance/override다. Config 및 정식 초기화 경로와 반복 합 경로가 준비된 뒤 원본/instance를 구분해 전환한다. 기존 prefab GUID 1bfc52cd235e809449d17d48571f5a38와 각 기존 meta를 유지하고 전체 Apply/Revert 금지.
5. 활성화 전후 단일 EntryPoint/Orchestrator/Director/Reward/World 참조, Character ID, 모든 스킬 rule, 가능한 카드와 BP/Ultimate mapping, Debug fixture 비혼입, Missing Script/Reference 및 YAML diff를 확인한다. 이전 6개 gate 재실행: 선행 Debug 오류를 넘어 후속 검증까지 실제 통과해야 한다.
6. 실제 Play Mode는 승인·보류 해제 후 Contact/FieldAttack, 1~3인 Hand 및 인계·MP/BP 승인, 일반/합 빈 손패, 유효 cycle/refill·Ultimate, 스턴/Panic/KO, 재전투, Pause/Abort/Reward·입력 복귀를 검사한다. 정식 SO 값·기능 검증은 fixture 통과와 별도로 기록한다.
7. 문제 발생 시 **편집 직전 현재 작업본**에서 승인된 변경만 되돌린다. 원본 prefab과 Scene override를 한 쌍으로 검토하고, 도중 사용자 변경이 있으면 자동 덮어쓰기 대신 변경 구간을 대조한다. meta/GUID와 다른 미커밋 변경을 보존한다.

## 검증 및 보존

실제 Unity 6000.3.23f1 선택 실행 **655 PASS / 6 FAIL / 1 IGNORED**, 총662 (`Logs/CBT-R2-08-P2B2-Regression.xml`, `.log`; 종료2는 기존 실패). P2B1 XML과 실패 이름을 직접 비교하여 차이가 없음을 확인했다. Debug 없는 명시적 초기화, 활성/비활성·오류/rollback·중복/재전투, Character ID별 손패, Hand/BP/Ultimate·인계, Item/Film 및 LegacyPlanning fixture를 포함한다. 18개 초기화 검사를 포함한 선택 실행은 정식 콘텐츠 Production 검증이 아니다.

기존 실패 6건은 `CBT-R2-08-P2B1-Verification.md`에 전체 이름을 기록했다. 5건은 Production Scene의 Hand Debug component 선행 gate, 1건은 Dungeon_Template의 BP Debug prefab gate이며 **Asset Change Required**를 유지한다. ignored는 별도 프로세스 재시작이 필요한 `PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners`다.

이번에는 C#을 변경하지 않았으며 Unity 테스트 실행 시 compiler error는 관찰되지 않았다. 별도의 강제 전체 재컴파일 여부는 확인하지 않았다. P2B1 당시 실제 컴파일 및 기존 경고 4건과 구분한다. 전체 회귀는 보호 Scene 설치·저장 함수를 호출하는 검사가 포함되어 실행하지 않는다. 별도 Editor 재시작 Save, Player Build, 실제 Production Inspector 및 수동 Play Mode도 미실행이다. 05/06A 보류를 유지한다.

시작 시 기록한 기존 코드·보호 자산·미커밋 파일 1,798개는 모두 SHA-256 동일하다. 수정·삭제·이동 없음. 추가는 이 보고서와 무시된 Logs의 실행/해시 기록이다. 기존 API·SerializeField·GUID・meta・package 영향 없음. 보고서 공백 검사 통과; 기존 폰트/Scene의 이전 공백은 수정하지 않았다.

## Development tracking

- System: Combat / Hand / BP / Ultimate. Production Owner: CombatEntryPoint → Bootstrapper → CombatSession 및 기존 Runtime.
- Feature: CBT-R2-08-P2B2 설정 계약·데이터 승인 매트릭스.
- 구현: 설정 계약 조사·전환 가이드 완료; 코드 변경 없음. 실제 정식 콘텐츠 및 적 Hand/Clash 정책은 Content/Contract Pending.
- 검증: 실행 결과 아래 기록. 정적 참조 조사와 격리 fixture는 Production 활성화·Inspector·실제 Scene 통합 검증을 의미하지 않는다.
- 권장: **Contract Analysis Complete — Content/Contract Approval and Production Activation Pending**. Cutover 완료 아님.
