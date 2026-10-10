# GAME_002 Film / Unique / Combat Skill 콘텐츠 제작 가이드

> 대상: 기획자와 Unity 콘텐츠 적용 담당자  
> 기준: 현재 `main`의 Production 코드와 `Dungeon_1_Production` 연결 상태  
> 범위: 일반 전투 스킬, Film 스킬, Character Unique 스킬, 적 처치 기반 Film 획득  
> 이 문서는 콘텐츠를 작성하는 방법을 설명한다. Production 코드, 기존 Scene/Prefab, 기존 persistent key 또는 GUID를 바꾸는 방법을 제안하지 않는다.

## 1. 먼저 알아둘 현재 상태

현재 시스템은 아래의 **콘텐츠를 받을 수 있는 구조**까지 구현되어 있다. 그러나 승인되어 실제 Production에 연결된 Film, Unique, `EnemyDefinitionSO`는 아직 없다. `Dungeon_1_Production`의 적이 기존 공격 스킬을 사용한다는 사실은 Film 획득 매핑이 작성되었다는 뜻이 아니다.

| 구분 | 현재 확인된 Production 상태 | 콘텐츠 작업 시 의미 |
| --- | --- | --- |
| `SkillDefinitionSO` | 7개 에셋 존재. `Assets/GAME/Data/Skill/`의 4개가 Production Combat Registry에 등록됨 | 새 스킬은 같은 SO 형식을 사용하고 Registry에 추가해야 함 |
| Film | `ownershipCategory = Film` 에셋 0개 | 시스템은 준비됨. 기획 승인 후 첫 Film을 새로 authoring 가능 |
| Unique | `ownershipCategory = Unique` 에셋 0개 | 시스템은 준비됨. 캐릭터/레벨/스킬 매핑이 승인되어야 authoring 가능 |
| `EnemyDefinitionSO` | 에셋 0개 | 적의 기존 공격 loadout과 Film 획득 정보는 별도 데이터임 |
| 캐릭터 진행도 | `Assets/GAME/Data/Test/CP_TestHero.asset`만 존재 (`hero.test`, Test) | Production 캐릭터 진행도 정의와 Unique 목록은 아직 authoring pending |

따라서 이 문서의 예시는 `Example/Test`이며, 이름·수치·적→Film·캐릭터→Unique 관계를 Production에 승인하는 근거가 아니다.

## 2. 기획자가 이해할 세 종류의 스킬

| 종류 | 플레이어가 얻는 방식 | 누가 장착/사용하는가 | 전투에 들어오는 조건 | 기획 시 핵심 |
| --- | --- | --- | --- | --- |
| 일반 전투 스킬 (Compatibility) | 기존 전투 loadout/호환 경로 | 현재 field combatant의 loadout | 해당 캐릭터의 기본 loadout에 있을 때 | 기존 전투 규칙과 숫자 |
| Film | 전투에서 **Victory**로 끝났고 실제로 처치 기록이 난 적의 명시적 Film 매핑 | 파티가 공유 보유, 파티원별로 장착 | 공유 보유 + 그 캐릭터가 장착 | 어느 적이 어떤 Film을 주는지 |
| Unique | 캐릭터 진행도 정의에 있는 전용 목록 | 지정된 한 캐릭터만 | 소유자 일치 + 지원되는 레벨 조건을 만족 | 소유 캐릭터와 해금 레벨 |

Film은 파티 공용 보유 상태다. 같은 Film을 여러 파티원에게 각각 장착할 수 있지만, 한 캐릭터에 같은 Film을 두 번 장착할 수는 없다. Unique는 Film처럼 장착/해제하는 목록이 아니라, 소유자와 레벨로 자동 판단되어 전투 snapshot에 포함된다.

### 지원되는 대상·비용·수치

`SkillDefinitionSO` Inspector는 현재 `Self`, `SingleEnemy`, `SingleAlly`, `AnySingle`, `AllEnemies`, `AllAllies`, `Environment`, `None` 대상 규칙과 `Attack`, `Defense`, `Dodge`, `Utility`, `Inspect`, `ScanEnv` 태그를 데이터로 제공한다. 키워드는 Ice/Fire/Dark/Elec/Wind/Earth/Water이다. 새 대상 규칙이나 태그의 전투 의미를 Inspector 값만으로 추가할 수 있는 것은 아니다.

- 영감 비용은 `inspirationCost`를 사용한다.
- MP 비용(`mpCost`)과 Final Combat 전용 MP 비용(`finalMpCost`), clash power는 별도 필드다.
- 일반 피해는 `baseDamage`, 그로기 수치는 `baseStagger`, 약점 적중 보너스는 `weaknessStaggerBonus`, 행동 순서는 `speed`를 사용한다.
- `CombatStatusDefinitionSO`를 연결하면 `SkillRunner.TryExecute` 실행 경로는 대상에게 상태를 적용한다. 상태의 정체성, Buff/Debuff, 중첩(Refresh/Replace/Stack), 지속 메타데이터를 별도 Status SO에서 작성한다. 다른 전투 흐름에서의 상태 효과 요구는 Play Mode로 별도 확인한다.
- `Inspect`는 약점을 공개하고, `Utility`/`ScanEnv`는 현재 피해를 적용하지 않는다. 회복량, 보호막, 도발, 지속 피해, 확률 발동, 연계/콤보, 조건부 수치 계산은 이 가이드의 Inspector-only 범위로 확정하지 않는다. 필요하면 기능 요청으로 분리한다.

### 애니메이션·VFX·SFX 기획서에 적을 것

스킬에는 전투/필드 Animator trigger 문자열, Cast VFX, Impact VFX, Cast SFX, Impact SFX를 연결할 수 있다. VFX는 있으면 전투 presentation에서 생성되고, SFX는 해당 위치에서 재생된다. 에셋이 아직 없으면 `미정`으로 기록하고 빈 참조를 허용하는지 담당자와 결정한다. 임의 trigger 이름이나 파티클 수명 규칙을 새로 만들지 않는다.

## 3. 콘텐츠가 연결되는 구조

```text
기획표
  └─ SkillDefinitionSO (정체성·수치·표현)
       ├─ CombatEntryPoint.skillDefinitions (Production Registry)
       │    └─ 전투 시작 시 immutable combat loadout snapshot
       ├─ Film: EnemyDefinitionSO.acquirableSkillPersistentKeys
       │    └─ EnemySourceComponent.definition → 실제 처치 Victory → 파티 공유 Film 보유
       │         └─ Character Skill UI → 파티원별 장착/해제
       └─ Unique: uniqueOwnerCharacterId
            └─ CharacterProgressionDefinitionSO.uniqueSkillUnlocks
                 └─ 소유자 레벨 5/10/20/40 판정 → 해당 캐릭터의 전투 snapshot
```

`CombatEntryPoint`가 유일한 Production 전투 진입점이고, 기존 `skillDefinitions` 배열이 Combat Registry다. Film/Unique를 위해 별도 Skill Manager나 별도 Combat EntryPoint를 만들지 않는다.

## 4. 기획자 전달 규칙

1. 먼저 [기획 템플릿](GAME002_SkillContent_PlanningTemplate.md)을 채운다. 임시 ID는 사람이 읽는 추적용이며 Unity persistent key나 GUID가 아니다.
2. 기획 승인 시, Film이면 적의 stable identity와 Victory 획득 근거를 함께 승인한다. 확률 드롭, 패배/도주 보상, 적이 살아 있는 상태의 획득은 현재 구현 범위 밖이다.
3. Unique면 캐릭터 ID, 표시 순서, 해금 조건을 승인한다. 현재 자동 해금으로 지원하는 레벨은 **5 / 10 / 20 / 40**뿐이다. 한 캐릭터에 최대 6개 목록을 둘 수 있지만, 그중 조건을 아직 정하지 못한 항목은 `Unconfigured`으로 남아 잠긴 상태로 표시한다.
4. Unity 담당자가 Registry 중복 여부를 확인한 뒤 persistent key와 `skillId`를 배정한다. 기획자는 persistent key, Unity GUID, `.meta` GUID를 만들거나 기존 값을 바꾸지 않는다.
5. 기획 수치가 확정되지 않았다면 `기획 상태`를 미확정으로 남긴다. 임의 수치나 해금 규칙을 Production 데이터에 넣어 확정하지 않는다.

## 5. Unity 콘텐츠 적용 담당자 가이드

### 공통 사전 점검

- 현재 Production 스킬 위치는 `Assets/GAME/Data/Skill/`이다. 기존 `Skill_BasicAttack`, `Skill_skill2`, `Angel_Skill`, `Angel_Skill2`는 수정/재활용하지 말고 새 콘텐츠는 별도 SO로 만든다.
- `EnemyDefinitionSO`와 Production `CharacterProgressionDefinitionSO`의 실제 에셋 디렉터리는 아직 정해져 있지 않다. 첫 Production 에셋을 만들기 전에 팀의 데이터 경로 결정을 받는다. 아래 경로 표기는 권장 예시이지 기존 에셋이 이미 있다는 뜻이 아니다.
- `Assets/GAME/Combat/Data/Skills/`의 `skill.demo.*` 3개와 `Assets/GAME/Data/Test/CP_TestHero.asset`는 Demo/Test 예시다. Production 콘텐츠로 설명하거나 연결하지 않는다.
- `persistentKey`와 Registry 안의 `skillId`는 모두 고유해야 한다. 기존 키/ID의 이름 변경은 Save/Load 호환을 깨므로 금지한다.

### A. 일반 전투 스킬 만들기

1. Unity Project 창에서 **Create > Game > Combat > Skill Definition**을 선택한다. 실제 Create menu 경로는 `Game/Combat/Skill Definition`이다.
2. 새 SO를 승인된 Production 데이터 위치에 저장한다. 현재 확정된 스킬 저장 위치는 `Assets/GAME/Data/Skill/`이다.
3. Inspector에서 아래 필수 필드를 채운다.
4. `ownershipCategory`는 일반 호환 스킬이면 `Compatibility`로 둔다. `uniqueOwnerCharacterId`는 Unique가 아닐 때 비운다.
5. Production 씬의 기존 `CombatEntryPoint`를 찾아 **Skill Book Sources (MVP) > Skill Definitions** 배열에 새 SO를 추가한다. 기존 항목을 교체하지 않는다.
6. 스킬을 실제 field combatant가 쓰게 하려면 기존 `CombatSkillLoadoutComponent`의 `skillIds` 방식 또는 Film/Unique 경로 중 해당하는 **기존** 경로를 사용한다. 일반 스킬을 위해 새 runtime owner를 추가하지 않는다.
7. 8절의 자동 검증과 Play Mode 검증을 수행한다.

### B. Film Skill 만들기

1. 위와 동일하게 `SkillDefinitionSO`를 생성하고, `ownershipCategory`를 `Film`으로 설정한다.
2. 고유 `persistentKey`와 Registry 안에서 고유한 양수 `skillId`를 배정하고 Production `CombatEntryPoint.skillDefinitions`에 추가한다.
3. **Create > Game > Enemies > Enemy Definition**으로 `EnemyDefinitionSO`를 만든다. 실제 Create menu 경로는 `Game/Enemies/Enemy Definition`이다.
4. 적 정의의 `persistentKey`(stable enemy identity)를 정하고, `acquirableSkillPersistentKeys`에 위 Film의 **persistent key 문자열**을 정확히 추가한다. Unique key를 넣으면 validator가 오류로 처리한다.
5. 실제 전투에 참여하는 적 root에 `EnemySourceComponent`를 하나 추가하고 `definition`에 그 적 정의 SO를 연결한다. `CombatEncounterGroup` 또는 `CombatEncounterTrigger2D`의 helper child가 아니라, 처치 provenance를 가져야 하는 적 GameObject를 대상으로 한다.
6. Contact와 Field Attack 각각에서 Victory를 만들고, 보상 이후 Character Skill UI에서 Film이 공유 보유로 나타나는지 확인한다. 각 파티원을 선택해 독립적으로 장착/해제하고, 장착한 캐릭터의 다음 전투에서만 snapshot에 들어오는지 확인한다.

### C. Unique Skill 만들기

1. 실제 캐릭터 identity를 먼저 확정한다. `CharacterProgressionDefinitionSO.characterId`와 `SkillDefinitionSO.uniqueOwnerCharacterId`는 같은 정규화된 문자열이어야 한다.
2. Skill SO의 `ownershipCategory`를 `Unique`로 설정하고 `uniqueOwnerCharacterId`를 채운다. Unique owner가 비어 있으면 validation 오류다.
3. **Create > GAME > NonCombat > Character Progression Definition**으로 캐릭터 진행도 SO를 만들거나, 그 캐릭터의 기존 Production 정의를 연다. 실제 Create menu 경로는 `GAME/NonCombat/Character Progression Definition`이다.
4. `Unique Skill Unlocks` 목록의 순서가 UI 표시 순서다. 각 항목에 `persistentSkillKey`, `enabled`, `condition`, `unlockLevel`을 입력한다.
5. `condition = Level`일 때 현재 지원되는 `unlockLevel`은 5, 10, 20, 40뿐이다. 아직 두 개의 후속 Unique 해금 조건은 정해지지 않았으므로 `condition = Unconfigured`으로 남기며, UI에는 “조건 미정”으로 표시된다. 임의의 퀘스트/친밀도/확률 조건을 넣지 않는다.
6. Skill SO를 Production Registry에 추가한다. Unique는 Film 장착 목록에 넣지 않는다.
7. 해당 캐릭터의 레벨 전후에서 Character Skill UI의 잠금/해금 상태와 실제 전투 snapshot을 확인한다. 다른 캐릭터에게는 나타나거나 사용되면 안 된다.

### D. 적 source 연결

`EnemyDefinitionSO`에는 `persistentKey`와 `acquirableSkillPersistentKeys`만 있다. 적의 HP, 기본 공격 스킬, 콜라이더, patrol, encounter trigger를 복제하거나 대체하지 않는다. `EnemySourceComponent`는 definition 참조 하나만 가진다.

```text
Dungeon_1_Production (예시 구조; 실제 이름은 콘텐츠에 맞춤)
└─ Encounters
   └─ CombatEncounterGroup
      └─ Enemy_Example              ← Combat HP/loadout + EnemySourceComponent
         └─ Contact trigger child   ← 접촉 감지용 helper
```

연결 후 Scene/Prefab에서 missing reference가 없는지 확인하고, Contact와 `PlayerFieldAttackController`의 Field Attack 모두 실제 `CombatEntryPoint`로 들어가는지 확인한다.

## 6. SkillDefinitionSO Inspector 필드 표

| Inspector 구역 / 필드 | 필수 | 의미와 작성 규칙 |
| --- | --- | --- |
| Identity / `skillId` | 예 | Registry 내부에서 양수·고유. persistent key와 별개인 전투 runtime ID |
| Identity / `displayName` | 예 | UI와 전투에 보이는 이름 |
| Persistent Save Identity / `persistentKey` | 예 | Save/Load와 Film/Unique/적 매핑에 쓰는 불변 문자열. 공백 금지, 전체 SO에서 고유 |
| Persistent Ownership Role / `ownershipCategory` | 예 | `Compatibility`, `Film`, `Unique` 중 하나 |
| `uniqueOwnerCharacterId` | Unique만 | Unique의 소유 캐릭터 ID. Film/Compatibility에는 비움 |
| Costs / `inspirationCost`, `mpCost` | 기획에 따라 | 일반 전투 비용 |
| `tag`, `targeting`, `consumesTurn` | 예 | 기존 enum 범위에서만 선택. UI/전투 규칙과 함께 시험 |
| `keywords` | 선택 | 약점·태그 판정에 쓰는 속성 mask |
| Movement Presentation | 선택 | `movementMode`, 목표 거리, 이동 속도, 이동 후 지연. 표현용 접근 이동 |
| Final Combat / `finalMpCost`, `clashPower` | 해당 모드 | 0 이상. Final Combat 설계가 있을 때만 승인 수치 사용 |
| Combat Status Effects / `appliedStatusEffects` | 선택 | Status SO 배열. 상태 종류와 중첩/지속 정의도 검토 |
| Animation / combat·field trigger | 선택 | 기존 Animator controller에 존재하는 trigger 문자열 사용 |
| Field Use | 선택 | `fieldUsable`, cooldown, hit delay, hit box size/offset. 새 field 기능을 보장하지 않음 |
| Presentation Assets | 선택 | Cast/Impact VFX 및 SFX 참조 |
| MVP Numbers | 예 | 피해·그로기·약점 보너스·speed. 회복 등 다른 의미로 전용하지 않음 |

## 7. 올바른 예와 금지 예

| 상황 | 올바른 설정 | 잘못된 설정 |
| --- | --- | --- |
| Film 획득 | Film SO key를 EnemyDefinition의 목록에 한 번만 넣고 실제 적에 EnemySourceComponent 연결 | 적의 `CombatSkillLoadoutComponent.skillIds`에만 넣고 Film 획득을 기대 |
| Unique 소유 | Skill의 Unique owner와 progression `characterId`를 동일하게 설정 | Unique owner를 비우거나 다른 캐릭터 key를 목록에 기입 |
| 레벨 해금 | 5/10/20/40 또는 `Unconfigured` | 레벨 7, 퀘스트 조건, 확률 조건을 Inspector만으로 확정 |
| Registry | 새 SO를 기존 `CombatEntryPoint.skillDefinitions` 배열에 추가 | 새 CombatEntryPoint 또는 새 Registry/Manager 생성 |
| IDs | 담당자가 고유 key/skillId를 확인해 배정 | 기존 persistent key/GUID를 재사용·변경 |

## 8. 검증 방법

### 자동 Validator 메뉴

저장된 Scene, Play Mode 밖에서 아래 실제 메뉴를 실행한다.

1. **GAME > Validation > Combat Skill Persistent Identities**  
   모든 `SkillDefinitionSO`의 빈/중복 persistent key를 검사한다.
2. **GAME > Validation > Enemy Skill Acquisition**  
   적 stable identity의 중복, Film key의 빈/중복/미해결, Unique를 적 획득으로 매핑한 오류를 검사한다.
3. **GAME > Validation > Character Unique Skills**  
   Unique owner, 캐릭터 ID, 중복 Unique, 최대 6개, owner 불일치, 지원하지 않는 조건/레벨을 검사한다.
4. **GAME > Verification > Audit Film Unique Production Content**  
   위 데이터 검사와 Production UI/Scene/Registry 검사를 묶은 읽기 전용 audit이다. 빈 Film/Unique 매핑도 구조상 통과할 수 있으며, audit 통과는 콘텐츠 승인과 동의어가 아니다.
5. 필요 시 **Tools > GAME > Validate Production UI Routing** 및 **GAME > Production Migration > Validate Dungeon 1 Production Scene**도 실행한다.

### 수동 Play Mode 확인 순서

1. Content가 등록된 Production Scene을 저장하고 Play Mode에 들어간다.
2. Contact 전투를 시작해 Registry 스킬 선택과 실행, 피해/상태/표현을 확인한다.
3. Field Attack으로도 같은 적과 전투에 들어가 `StartReason.PlayerFirstHit` 경로가 동작하는지 확인한다.
4. Film 적을 Victory로 끝낸 뒤 Film 보유, 공유, 장착/해제, 다음 전투 snapshot을 확인한다. 패배·중단은 Film을 주지 않아야 한다.
5. Unique 캐릭터를 해금 레벨 전/후로 준비해 UI와 실제 전투 사용 가능 여부를 비교한다.
6. Save 후 Title/다른 Scene을 거친 Cold Load로 돌아와 Film 보유·장착, Unique 해금, Reward/Quest/Exploration 복귀가 유지되는지 확인한다.

## 9. 한 개의 신규 Film을 넣는 전체 순서

```text
기획표 작성 → 승인(스킬/적/수치/연출) → 담당자가 ID 배정
→ SkillDefinitionSO 생성 → Film 설정 → Registry 추가
→ EnemyDefinitionSO 생성·Film key 매핑 → 실제 적에 EnemySourceComponent 연결
→ Validator 4종 → Contact/Field Attack Victory 확인
→ Character Skill UI 장착/해제 → 전투 사용 → Save/Load 회귀 확인
→ 검수 상태 업데이트
```

## 10. 현재 Inspector만으로 할 수 없는 요청

- 확률 드롭, 드롭 테이블, 중복 보상 규칙 변경
- Victory 이외의 획득 조건(패배, 도주, 대화, 퀘스트, 친밀도 등)
- 5/10/20/40 이외 Unique 레벨, 또는 레벨 외 해금 조건
- 회복/보호막/도발/지속 피해/조건부 계산/연계 효과의 새 전투 규칙
- 새 targeting enum, 새 keyword, 새 UI 열, 새 저장 스키마

이 항목은 “기획 미정”이 아니라 **추가 코드 구현 요청**이다. 요청서에는 원하는 규칙, UI 표시, Save/Load 영향, 전투/보상/퀘스트 상호작용, 테스트 기준을 포함한다.

## 11. 안전 규칙

- Production 코드, `CombatEntryPoint`, Registry 구조, Scene/Prefab을 콘텐츠 작업 명목으로 교체하지 않는다.
- 기존 persistent key, `skillId`, `.meta`/GUID를 바꾸지 않는다.
- Demo/Test 에셋을 Production 매핑의 근거로 쓰지 않는다.
- 신규 Skill Manager, 별도 Combat EntryPoint, 별도 global 상태 writer를 만들지 않는다.
- 기존 적의 기본 loadout은 획득 정보와 별개다. `EnemySourceComponent` 없는 적은 Film source로 취급하지 않는다.
- Production 콘텐츠 작성 전에는 승인된 매핑 표를 확보한다.

관련 양식: [기획 템플릿](GAME002_SkillContent_PlanningTemplate.md) · [검증 체크리스트](GAME002_SkillContent_ValidationChecklist.md)
