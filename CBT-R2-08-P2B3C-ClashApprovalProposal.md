# CBT-R2-08-P2B3C — Clash Playtest V1 수치 분석 및 승인안

2026-10-30 KST. **수치 제안 및 격리 검증이며 승인·Production 적용이 아니다.** Runtime과 기존 Config/SO/Scene/Prefab은 변경하지 않았다. P2B3D 자산 전환도 수행하지 않았다.

## 판단과 권장안

1차 비교 기준으로 **B 균형형: 공격자/대응자 강인도3/3, 최대 비교3, 선택시간5초**를 제안한다. B3B 검증과 이어지며 합 손패3장 안에서 대응자가 세 번 대응할 수 있다. 다만 MP 비용1인 스킬로 3회 비교하려면 진입 시 MP3이 필요하다. 현재 저장 프로필의 초기 MP2로 즉시 선언하면 두 번 지출 후 MP가 부족하다. Clash 중에는 기다려도 MP가 회복되지 않는다.

따라서 **초기 MP2에서 즉시 공격하는 경험이 우선이면 A 짧은형(2/2·최대2·5초)이 더 일관적**이다. C 긴 합형(4/4·최대4·5초)은 실험용 비교군으로 권장한다. 현재 MP 상한3과 적 합 손패3장 때문에 Clash 수치만 높여서는 일반적인 비용1/Hand 적 교전에서 4회 비교가 보장되지 않는다.

어느 안도 정식 밸런스가 아니다. 아래 숫자 및 selector의 사용자 승인이 있어야 후속 작성에 사용할 수 있으며, 그 승인 자체가 Production 활성화·보호 자산 전환 승인은 아니다.

## 지침·Git·근거

- 활성 지침: 루트 `AGENTS.md`; 중첩 지침 없음. Branch `main`, HEAD `6fa26d294472574097d33b884858ba0ee8bdc8b7`, Unity 6000.3.23f1 유지.
- P2B3A/B 보고서·실제 두 Playtest Config, `FinalCombatRuntimeConfigSO`, `CombatClashSeriesConfig/State`, EntryPoint 초기화, StateMachine, Orchestrator, Hand/MP/BP 및 기존 판정/Timeout/실행 검사를 조사했다.
- 승인 근거는 현재 지시와 P2B2D의 Enemy opt-in·분류·Clash 필수 계약, P2B3A의 Hand/BP/Ultimate Playtest 승인이다. 이전 기획의 제안 숫자를 승인으로 격상하지 않았다. 원격 기획의 새 승인·변경을 확인했다고 주장하지 않는다.
- 구현 기준은 현재 코드·직렬화 데이터·실행 결과다. 시작 Git 상태/해시는 `Logs/CBT-R2-08-P2B3C-BeforeStatus.txt`, `...-BeforeHashes.json`에 기록했다.
- 이전 선택 회귀는 B3B **757 PASS / 6 FAIL / 1 IGNORED**. 이전 전체 **1,562/8/1**은 과거 기준선이다. 이번 실행과 구분한다.

## 1. 실제 필드·현재 값·규칙

| 필드/설정 | Production 기존 SO | B3A SO | B3B Clash SO | 상태 |
|---|---|---|---|---|
| `enableProductionHandBp` | 미작성=false | false | false | 실제 활성화 안 됨 |
| `clashAttackerDurability` | 미작성=0 | 0 | 3 | B3B 숫자는 Test Fixture Only |
| `clashResponderDurability` | 미작성=0 | 0 | 3 | 정식 숫자 미승인 |
| `clashMaxComparisons` | 미작성=0 | 0 | 3 | 첫 비교 포함 상한 |
| `clashDecisionDurationSeconds` | 미작성=0 | 0 | 5 | 후속 Clash 선택별 제한 |
| `clashFinalSkillPolicy` | 미작성=Unspecified | Unspecified | LastCommittedWinningSkill(1) | 제작 프로필 승인 필요 |
| `clashTimeoutAttackPolicy` | 미작성=Unspecified | Unspecified | FirstAvailableSkill(1) | 제작 프로필 승인 필요 |

새 Production Hand/BP 모드는 위 양수/유한 시간/명시적 selector가 없으면 Opening Effect와 첫 명령 전에 거부된다. 비활성 기존 Config 및 LegacyPlanning은 기존 동작을 유지한다.

**강인도는 합 Series의 역할별 런타임 값**이다. 공격자/대응자가 아군인지 적인지에 따라 초기값이 바뀌는 별도 필드는 없다. 적이 먼저 공격하면 공격자 설정을 적이 받는다. 후보는 역할 편향을 피하도록 대칭값을 제안한다. 독립적인 `maxDurability` 필드·캐릭터별 최대 강인도·회복 필드는 현재 없다. Series 생성 시 시작값을 받고 이후 감소만 하며, 새 Series는 다시 시작값을 받는다.

강인도는 HP, `CombatantCombatState.CurrentPosture/MaxPosture`, 과거 `ICombatant.Stagger/StaggerMax`와 별개다. 기존 Posture3 또는 Legacy Stagger6/8을 강인도 승인값으로 사용하지 않는다.

현재 비교 계산:

`공격 최종 위력 A = clamp(Skill.ClashPower + 상태 ClashModifier + 해당 승인 BP ClashPower 보정, 0, int.MaxValue)`; 대응 위력 R도 같은 방식이다.

- A > R: 대응 강인도에서 A−R을 감소, 최저0.
- A < R: 공격 강인도에서 R−A를 감소, 최저0.
- A = R: 양쪽 강인도 각각1 감소.
- 양측0이면 높은 Skill Speed, Speed도 같으면 현재 공격권 소유자를 승자로 한다.
- 한쪽0이면 반대쪽 승자다. **소진 결과가 비교 상한보다 먼저 평가**된다.
- 아직 양측이 살아 있고 비교 상한에 도달하면 Tie다. 최종 피해 없이 Standoff로 돌아간다.

이 Series 비교식에는 랜덤 굴림이 없다. 현재 위력/비용은 선택 Skill의 데이터와 상태/BP에서 오므로 Config 강인도만으로 실제 승률·DPS·난이도를 산출할 수 없다.

## 2. Proposed 후보와 정확한 승인 대상

전부 **Playtest Candidate / 미승인**이다. 비교 변수를 제한하기 위해 시간과 selector는 공통으로 둔다.

| Config 필드 | A 짧은 합 | B 균형형(조건부 권장) | C 긴 합 |
|---|---:|---:|---:|
| `clashAttackerDurability` | 2 | 3 | 4 |
| `clashResponderDurability` | 2 | 3 | 4 |
| `clashMaxComparisons` | 2 | 3 | 4 |
| `clashDecisionDurationSeconds` | 5초 | 5초 | 5초 |
| `clashFinalSkillPolicy` | LastCommittedWinningSkill(1) | 동일 | 동일 |
| `clashTimeoutAttackPolicy` | FirstAvailableSkill(1) | 동일 | 동일 |

유지할 승인 Playtest 계약: 일반2/합3 슬롯, BP 초기0·상한3·유효 RoundCycle2회마다+1·행동 투자0~3, 승인 누적 BP 소비에 따른 Ultimate Stage1/2/3. Enemy Hand Source Key opt-in, Film/Unique 소유 분류와 Normal/Clash 사용 분류, 동일 Skill 양쪽 배치 금지, 기존 Owner/Save 계약도 유지한다.

### 선택 근거와 1대1 진행

| 항목 | A | B | C |
|---|---|---|---|
| 목적 | 짧은 자원 교환, 초기 MP2와 비용1 가정에 적합 | 3회 비교·카드 선택·BP 분산 관찰 | 낮은 비용/일반 적과 장기 비교의 한계 관찰 |
| 일정 우세 +1의 결착 | 2회 | 3회 | 4회(자원이 충분할 때) |
| 한 번에 이길 최소 위력 차이 | ≥2 | ≥3 | ≥4 |
| 동등 위력·동일 Speed | 2회 양측0 → 공격권 승리 | 3회 양측0 → 공격권 승리 | 4회 양측0 → 공격권 승리 |
| 우세가 교대로 바뀌는 경우 | 2회 상한 Tie | 3회 상한 Tie | 4회 상한 Tie |
| 비용1·초기 MP2 즉시 교전 | 2회 결착 가능 | 2회 뒤 공격 MP 부족 → Timeout Tie | 2회 뒤 동일 |
| 비용1·MP3에서 진입 | 자원 여유1 | 3회 결착 가능 | 3회 뒤 MP 부족 |
| Enemy Hand on / 합3장 | 최대2회 대응, 한 장 여유 | 세 장 전부 대응에 사용 가능 | 네 번째 대응 카드 없음 |

‘가능’은 제시된 결정적 위력/비용 가정에서의 결과다. 실제 스킬 분포를 알지 못하므로 후보별 승률이나 상한 도달 확률을 임의 수치로 만들지 않았다.

### 3라운드 예제와 자원량

공격 위력3, 대응2의 일정 우세 +1을 사용한다. 아래 상태는 **공격 강인도/대응 강인도**다.

| 순서 | A 2/2·상한2 | B 3/3·상한3 | C 4/4·상한4 |
|---|---|---|---|
| 시작 | 2/2 | 3/3 | 4/4 |
| 1회 비교 | 2/1 | 3/2 | 4/3 |
| 2회 비교 | 2/0 → 공격 승리 | 3/1 | 4/2 |
| 3회 비교 | 실행 불가: 이미 종료 | 3/0 → 공격 승리 | 4/1 → 계속 필요 |
| 이후 | 없음 | 없음 | 4회 비교 가능하면4/0 승리 |

세 번째 행까지의 강인도 차이 자체는 **비용0·일반 적(no-Hand) 격리 조건으로 세 후보 모두 실제 실행**했다. 비교 비용을 제거해 공식만 격리한 것이며 MP0을 제작 비용으로 제안한 것은 아니다. 비용1 조건은 별도 검사했다.

역전 예제는 위력2/2에 라운드별 승인 BP ClashPower 보정1을 번갈아 투자해 재현했다. 양측 각 BP3 예산은 테스트에서 기존 ledger의 6회 유효 RoundCycle로 사전 준비했으며, 첫 전투의 초기 BP0을 변경하지 않았다.

- 1회 공격자 BP1: A3−R2 → 대응 강인도−1.
- 2회 대응자 BP1: A2−R3 → 공격 강인도−1.
- 3회 공격자 BP1: A3−R2 → 대응 강인도−1.
- A는2회 뒤1/1 Tie, B는3회 뒤2/1 Tie. C는4회까지 대응자 BP1을 추가해2/2 Tie였다. 최종 HP/정신력 변화가 없음을 검사했다.

이 예제는 승인된 투자 한도 안의 기존 BP 기능을 사용한다. 실제 제작 Skill에 ClashPower 보정1을 승인한 것은 아니다.

## 3. 카드·MP·BP·Ultimate 해석

n회 모두 대응한 Series의 지출(각 Skill MP1 가정): 공격자 Normal1장 + Clash(n−1)장, 대응자 Clash n장, 양측 MP 각각n. 마지막 최종 공격에서 이미 승인된 일반 피해 비용/카드를 다시 지출하지 않는다. 정상 최종 피해는 선택한 승자 Skill 효과 1회이며 **라운드 수·강인도 차이가 자동 피해 배율이 되지 않는다**.

| 비교 횟수 | 공격자 카드 | 대응자 카드 | 양측 합계 MP(비용1) | fixture 최종 HP 피해 |
|---:|---|---|---:|---:|
| 1 | Normal1 | Clash1 | 2 | 1 |
| 2 | Normal1+Clash1 | Clash2 | 4 | 1 |
| 3 | Normal1+Clash2 | Clash3 | 6 | 1 |
| 4 | Normal1+Clash3 | Clash4 필요 | 8 | 1, 단 실제 자원 충족 필요 |

실제 Skill의 비용/효과가 다르면 이 표의 MP·HP 값도 달라진다. 카드 kind 소비 구조는 동일하다. 일반 손패2장은 한 합에서 Normal을 두 번 쓰는 슬롯이 아니라 다음 선언/Chain용 여유와 관계된다.

중요한 현재 제약:

1. 저장 프로필은 MP 상한3·초기2·Standoff 회복1/초라는 기존 값이다. 그 값의 정식 승인과 이번 Clash 승인은 구분한다. 비용1의 3회 비교를 하려면 **진입 전 MP3**이거나 더 낮은 비용 구성이 필요하다. 초기2에서 회복1을 기다리는 약1초 예시는 현재 회복식에 따른 예상이며 수동 플레이 감각을 검증한 값이 아니다.
2. Orchestrator는 Clash에서 선택 timer만 진행하며 MP 회복은 Standoff에서만 한다. 실제 검사에서도 `Tick(100,4)` 동안 Clash MP0은 그대로였다. 긴 제한시간이 부족 MP를 해결하지 않는다.
3. 합 손패3장 유지 시 대응자는 첫 대응을 포함해3장까지만 낼 수 있다. 공격자는 Normal1+Clash3으로 네 번째 공격을 낼 수 있다. C에서 MP 비용을0으로 격리해도 Hand 적은 네 번째 공격에 무대응했고, **3회 비교 뒤 강인도4/1인 채 Unopposed 최종 공격**이 발생했다. 일반 no-Hand 적은 이 카드 제한을 적용하지 않는다.
4. MP 최대3·모든 비용1이면 C는 진입 MP3에서도 네 번째 공격 비용을 낼 수 없어 3회 후 Tie로 끝났다. C의 완전한4회 비교는 낮은/혼합 MP 비용과 Enemy no-Hand 같은 별도 제작 조건이 필요하다. Hand 슬롯/MP 상한을 임의로 늘려 해결하지 않았다.
5. Draw는 비용 환불·중간 refill·BP 획득을 만들지 않는다. 강인도/상한을 높이면 ‘더 큰 피해’보다 비용 소진·무대응·draw 비율이 달라질 수 있다.
6. BP는 ClashRound마다 새로 얻지 않는다. 세 라운드에서 BP1씩 쓰려면 기존 예산3이 먼저 있어야 하며 승인 소비량3이 같은 캐릭터의 Ultimate Stage3으로 연결된다. C 네 라운드에 BP1씩 필요한 예산4는 현재 상한3으로 즉시 확보할 수 없다. 이를 이유로 BP/Ultimate 계약을 바꾸지 않는다.

## 4. 결정 시간·안전 종료·연출·장기전

`clashDecisionDurationSeconds=5`는 **후속 ClashAttack/ClashResponse 각각의 선택 제한**이다. 최초 일반 공격 선언/첫 대응과 연출 전체의 제한시간이 아니다. unscaled time으로 감소하며 Pause/GameState.Paused 또는 timeScale≤0에서는 진행/제출을 막는다.

- 공격 Timeout: 첫 가용 스킬을 선택·승인 시도. 가용 공격이 없으면 다음 공격 없이 Tie로 Standoff 복귀.
- 대응 Timeout: 무대응 승인. 이미 선택한 공격을 기존 Unopposed Outcome으로 한 번 실행.
- 선택 대기 중 Abort: pending/timer/binding 정리, stale command와 지연 연출 완료 거부.
- 일반 모션 request에는 이 선택 timer가 적용되지 않는다. 실제 Director 연출은 B3B 회귀로 검증하되, 실제 Dungeon의 Animation/Event/카메라 연결은 아직 미검증이다.

n회 비교에 도달하기 위한 후속 선택은 최대2(n−1)개다. 양측이 전부 제한시간을 쓰는 **타이머 합의 예상 상한**은 A10초/B20초/C30초이고, 적 AI가 즉시 응답하면 플레이어 대기는 대략 A5/B10/C15초다. 최초 선언/연출/Pause는 제외한 산술값이며 실제 전투 소요 시간 측정값이 아니다. 이번에5초 타이머 동작 자체는 후보별로 검사했지만4초/6초/8초 등의 사용성 비교는 하지 않았다.

Series는 최대 비교 수와 유효한 선택 Timeout으로 계산상 무한 비교를 막는다. 그러나 전투 전체의 횟수 상한은 아니다. 다음 사항은 관찰이 필요하다.

- 연속 draw → Standoff 재진입으로 무피해 교전이 반복될 수 있다.
- draw는 유효 공격 cycle/refill/BP를 만들지 않으므로 카드와 MP만 소진하는 패턴의 빈도, 행동 불가·양보 반복을 확인해야 한다. 전체 전투가 무한히 진행하지 않는다는 수동 통합 보장은 이번 분석 범위 밖이다.
- 가용 스킬0인데도 공격 Timeout5초까지 기다리는 경로는 안전하지만 체감 대기일 수 있다.
- 적 Hand on/off가 C의 결착 성격을 크게 바꾸므로 적 타입별 결과를 섞어 하나의 평균으로 평가하면 안 된다.

## 5. 최종 효과·복귀 규칙과 승인 구분

기존 Runtime 규칙은 그대로다. 각 비교는 강인도만 줄이고 HP/Status/Mental/Posture를 실행하지 않는다. 마지막 라운드 연출 완료 후 명시적 selector가 이미 승인된 승자 Skill을 선택하고 기존 Outcome/SkillRunner를 한 번 실행한다. 상태/정신력/체간/Terminal 처리는 기존 StateMachine 경계를 따른다. 생존하면 Outcome 연출 완료 뒤 Chain, draw면 최종 공격 없이 Standoff, Terminal이면 기존 EntryPoint의 CompletionId 경로로 종료한다.

상태·정신력의 실제 수치는 제작 데이터와 기존 Config를 따른다. B3B의 피해1·Mental100→85·상태1회 fixture 검증을 현재 승인된 공식 콘텐츠로 치환하지 않았다. Reward/참가자 EXP의 중복 방지도 기존 Completion 계약을 유지한다.

확정/미확정 구분:

| 항목 | 상태 |
|---|---|
| Hand3/2, BP0/3/2주기/0~3, Ultimate1/2/3 | 기존 **Approved Playtest V1**, 변경 없음 |
| Enemy Source Key opt-in, 소유/카드 분류 분리, 동시 양쪽 배치 금지, 새 모드 Clash 필수 | 기존 승인 계약 유지 |
| 현재 감소식·tie/동시0 우선순위·상한/Timeout/효과 경계 | **Existing Runtime Rule**, 이번 변경 없음 |
| B3B 3/3/3/5, B2D 7/8/2 등 | **Test Fixture Only** |
| A2/2/2/5, B3/3/3/5, C4/4/4/5 | **Proposed / Playtest Candidate**, 승인 전 작성·활성화 안 함 |
| 공식 Skill/비용/위력/상태/BP계수/Enemy/캐릭터/Ultimate/AllOut | **Content/Contract Pending** |

## 6. 관찰 지표와 선택 기준

권장 측정 항목은 아래와 같으며 목표치 자체도 Proposed다. 아직 실플레이 표본이나 telemetry를 수집하지 않았다.

- 비교 횟수 분포:1/2/3/4회, 평균·중앙값, 위력 차이 구간별 결착.
- 종료 사유별 비율: 강인도 소진/동시0/상한 Tie/공격 MP 부족 Tie/카드 부족 무대응/대응 Timeout/Terminal.
- 교전 시작 MP, 실제 MP 지출, 유효 카드 수, BP 투자액과 캐릭터별 Ultimate 도달 주기.
- 선택 시간·Timeout 빈도와 가용 공격0 상태의 대기 시간.
- 실제 Outcome1회당 MP/카드 지출, 연속 draw 수, 마지막 HP 변화 이후 경과 시간, 양보/행동 불가 빈도.
- Enemy Hand on/off·1인/3인·Skill MP 비용별로 분리한 결과 및 인계 후 결착.

B를 우선 관찰하되 ‘3회 비교를 위한 준비/비용 계획’이 플레이에서 자연스러운지 확인한다. 즉시 공격이 대부분이고 MP 부족 Tie가 주된 종료라면 A와 비교한다. C의4회 분기 체험이 목적이라면 먼저 현재 Hand/비용 안에서 재현할 콘텐츠 조건을 승인해야 하며 손패/MP/BP 정책 변경을 이 승인에 끼워 넣지 않는다.

## 7. 검증·파일·보존 및 후속 단계

추가: `Assets/GAME/Tests/Editor/CombatClashCandidateAnalysisTests.cs` 및 Unity 생성 meta, 이 보고서. 무시되는 Logs에는 실행 XML/log/filter 및 실제 Runtime 출력에서 추출한 `CBT-R2-08-P2B3C-Candidates.csv`를 보존한다. Runtime·기존 fixture·Config/SO·Scene/Prefab 수정/삭제/이동 없음. 테스트는 B3B의 기존 격리 EntryPoint fixture를 reflection으로 재사용하며 새 Production Owner/공개 Runtime API/SerializeField를 만들지 않는다. 새 script GUID `2f4fea6480cbc9c45a5679c61c67d228`; 기존 GUID/meta는 보존한다.

새23개는 후보별 위력 우세·한 번 결착·동점/동시0·BP로 재현한 우세 교대/상한, MP2/최대3 한계, 적 카드3 한계, Pause/Timeout/Abort를 실제 기존 Runtime으로 실행한다. 비용0 조건과 비용1 조건을 분리했다. Timeout5초만 실제 비교했으며 다른 시간값/공식 콘텐츠/실제 Dungeon의 후보별 사용자 경험은 **예상 또는 미검증**이다. 기존 B3B 실제 Director·Chain/인계/Terminal/Reward·EXP 및 B3A22/B2D40도 회귀로 재실행한다.

기존 실패6개(공통 namespace `Game.Tests.Integration`)는 **Existing Confirmed / Asset Change Required**다:

1. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasCanonicalCombatRewardUiCameraAndBootstrapOwners`
2. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasExplicitInteractionOwnersWithoutLegacyInteractionPaths`
3. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasOneCanonicalRepeatableNpcInteraction`
4. `DungeonOneProductionSceneTests.ProductionDungeonOne_HasQuestDerivedCompletionContractWithoutLegacyExitOrFallbackDestination`
5. `DungeonOneProductionSceneTests.ProductionDungeonOne_IsCleanEnvironmentMigration`
6. `ProductionSceneWiringTests.DungeonTemplate_IsCanonicalSingleOwnerProductionSource`

앞5개는 기존 Hand Debug gate, 마지막은 Template BP Debug gate다. 참조 삭제/validator 완화로 숨기지 않는다. 전체 회귀는 보호 자산 설치·저장 검사가 포함되어 미실행한다. 실제 Production Inspector/수동 Play Mode, Player Build, 별도 Editor 재시작 Save도 미실행이다.

사용자에게 필요한 정확한 결정: 후보 A/B/C 또는 별도 **공격자 시작값, 대응자 시작값, 최대 비교 수, 선택시간(초)**, 그리고 **최종 스킬 selector / Timeout 공격 selector**. 강인도 최대치라는 별도 숫자를 승인할 필드는 현재 없다. 권장 B 승인 예시는3,3,3,5초,LastCommittedWinningSkill,FirstAvailableSkill이다. 이 보고서만으로 승인됐다고 처리하지 않는다.

P2B3D 전에는 위 선택 승인 외에도 유효한 캐릭터/적 snapshot·registry·카드 rule·Source Key·Skill 비용/위력/효과 및 Ultimate/AllOut 제작 계약이 필요하다. 보호 Asset 변경은 별도로 승인받아 기존 Config/CombatRuntime prefab/Dungeon scene/Template instance override를 구분해 최소 전환하고, Debug 분리·Missing Reference/GUID·기존6개 gate 및 실제 Contact/FieldAttack/인계/Timeout/Abort/Reward/Save를 검증해야 한다. 작업 직전 사용자 변경 포함 사본/해시로 복구하며 HEAD reset은 하지 않는다. 05/06A 수동 보류 유지.

## Development tracking

- System: Combat / Clash Playtest V1 balancing proposal.
- Owner: 기존 CombatEntryPoint/Session/StateMachine/Orchestrator/SkillRunner 유지.
- Implementation: 후보 분석·재현 검사·승인안 작성 완료. 밸런스 적용 없음.
- Compile/자동 검사: 아래 최종 기록. 실제 Production Inspector/수동 Play Mode/통합은 Pending.
- Recommended: **Clash Candidates Analyzed — User Numeric Approval / Content / Production Wiring Pending**.
- Runtime 결함 수정이나 시스템 전체/Cutover 완료로 기록하지 않는다.

## 최종 실행 및 보존 기록

Unity 6000.3.23f1 후보 집중 **23 PASS / 0 FAIL**, 선택 회귀 **780 PASS / 6 FAIL / 1 IGNORED**, 총787. 기록은 `Logs/CBT-R2-08-P2B3C-Candidates.xml/.log`, `...-Regression.xml/.log`, 정확한 실행 범위는 `...-TestFilter.txt`다. 후보별 실제 출력은 `...-Candidates.csv`이며 열은 시나리오/시작강인도/상한/비교횟수/남은양측강인도/결과/양측지출MP다. 출력은 Runtime 결과이며 별도 계산 엔진으로 재계산한 데이터가 아니다.

선택 회귀에서 B3B **15/15**, B3A **22/22**, B2D **40/40**, Legacy 관련 **220/220** PASS. 실제 Director Play Mode, 최종 피해/상태/정신력1회, Chain/인계, Terminal/Completion 및 Reward/EXP, Item/Film 검사를 포함했다. 후보별 새23개는 명시적 명령과 deltaSeconds를 사용하는 결정적 EditMode 검사이며, 새로운 후보별 수동 플레이/실측 사용성 검증을 수행한 것은 아니다.

실패6개는 B3B XML과 정확한 이름을 비교해 동일함을 확인했다. **신규 실패 없음(선택 범위)**. 원문 `...-SixEvidence.txt` 보존. Ignored는 기존 `Game.Tests.Integration.PartyRewardDiskSaveTests.EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners`이며 별도 쓰기/읽기 프로세스 재시작 조건 때문에 미실행이다. 새 Skip 없음. 전체1,562/8/1은 이전 기준선이며 전체 회귀 재실행은 아니다.

Unity에서 새 테스트 컴파일 오류 및 `warning CS` 없음. 기존 Runtime 전체 강제 재컴파일를 수행한 것으로 기록하지 않는다. 이전에 기록한 OfficeFlowController meta/빈 Tests.asmdef 환경 안내는 기존 파일 무변경으로 보존했다.

최종 SHA-256 비교: 기존 **1,812개 전부 동일**, 기존 파일 수정/삭제/이동 없음. Production 및 B3A/B3B SO/Scene/Prefab, 기존 meta/GUID, Packages/ProjectSettings, 기존 dirty/untracked 코드·보고서 모두 보존했다. 추가는 새 테스트/meta와 이 승인안 **3개 파일**뿐이다. 새 GUID 중복 없음. 신규 C#/보고서 공백 검사 통과, 기존 폰트/Scene 공백은 수정하지 않았다. 기존 fixture의 private helper를 reflection으로 재사용하므로 향후 helper 이름/서명 변경 시 테스트 보수가 필요하며 Production API 영향은 없다.

재현 절차: Unity Test Runner EditMode에서 `CombatClashCandidateAnalysisTests`를 실행하고23/23 및 출력의 `CANDIDATE` 행을 확인한다. 이어서 B3B/B3A/B2D fixture와 선택 filter를 실행한다. 후보 A/B/C는 테스트 clone에서만 설정되므로 저장 SO 값을 바꾸지 않는다. 이후 사용자 수치 승인과 별도 자산 전환 승인·콘텐츠 준비가 이루어진 뒤에만 실제 Inspector/Play Mode 절차를 진행한다.

권장 상태: **Clash Candidates Analyzed — User Numeric Approval Pending**. Production 자동 초기화는 비활성, P2B3D 미진행. commit/push/PR 없음.
