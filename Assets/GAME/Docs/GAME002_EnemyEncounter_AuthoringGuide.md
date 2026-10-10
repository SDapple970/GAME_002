# GAME_002 Enemy / Encounter 제작 가이드

## 1. 콘텐츠 개요

Production field enemy는 `FieldEnemyMotor2D`/`FieldEnemyPatrolAI2D`, combat HP/loadout, `CombatEncounterGroup` 또는 `CombatEncounterTrigger2D`로 구성된다. 전투 시작은 오직 `CombatEntryPoint`로 수렴한다. `EnemyDefinitionSO`/`EnemySourceComponent`는 기본 공격과 별개로 Film 획득 source를 authoring한다.

## 2. 현재 구현 상태

접촉과 Player Field Attack 전투 진입, HP/loadout, patrol/chase, encounter 저장/재진입, victory의 defeated enemy provenance와 명시적 Film 획득은 있다. Production `EnemyDefinitionSO` asset과 승인된 Film mapping은 현재 0개다. Legacy `Game.Battle.FieldEnemy`, Demo/Test 적은 새 표준이 아니다.

## 3. Production Owner

`CombatEncounterGroup`은 encounter lifecycle/저장을, `CombatEncounterTrigger2D`와 `PlayerFieldAttackController`는 start request를, `CombatEntryPoint`는 전투를, `EnemySkillAcquisitionIntegration`은 victory Film 획득을 맡는다. field enemy가 보상이나 전투 계산을 직접 소유하지 않는다.

## 4. 사용 코드/Asset 목록

| 역할 | 실제 타입 |
| --- | --- |
| 적 이동 | `Game.Enemies.FieldEnemyMotor2D` (`rb`, `visualRoot`, `moveSpeed`, `faceMoveDirection`) |
| 적 AI | `FieldEnemyPatrolAI2D` (`motor`, player, left/right point, detection/chase/patrol 값) |
| 전투 데이터 | `CombatHpComponent`, `CombatSkillLoadoutComponent.skillIds` |
| encounter | `CombatEncounterGroup`, `CombatEncounterTrigger2D` |
| source | `EnemyDefinitionSO` — Create `Game/Enemies/Enemy Definition`; `EnemySourceComponent.definition` |
| validator | `GAME > Validation > Enemy Skill Acquisition` |

## 5. 관련 시스템과 데이터 흐름

```text
Contact trigger / Field Attack → CombatStartRequest → CombatEntryPoint
→ Combat result defeated source → EnemySkillAcquisitionIntegration
→ EnemyDefinitionSO.acquirableSkillPersistentKeys → shared Film runtime
```

## 6. 콘텐츠 제작 준비물

적 임시 ID/이름, stage 위치, HP·이동·patrol·기본 skill ID, encounter 정책, reward/Quest/Film 기획, sprite/animation/SFX를 승인받는다. stable enemy identity와 Film persistent key는 담당자가 asset/Registry를 검사한 뒤 배정한다.

## 7. 신규 콘텐츠 생성 방법

1. `Dungeon_Template`의 valid enemy/encounter 구성 또는 기존 Production enemy를 참조한다. Debug/Legacy object를 복제하지 않는다.
2. enemy root에 Rigidbody2D, collider, HP source, motor, patrol AI, `CombatSkillLoadoutComponent`를 기존 project 구성을 따라 연결한다.
3. `CombatEncounterGroup` 아래에 combatant enemy root를 넣고, Contact trigger는 helper child로 둔다. group auto collect 시 helper에는 HP source를 넣지 않는다.
4. Film을 주는 적만 **Create > Game > Enemies > Enemy Definition** SO를 만들고 `persistentKey`, `acquirableSkillPersistentKeys`를 입력한다.
5. 실제 처치될 enemy root에 `EnemySourceComponent`를 하나 붙여 definition SO를 연결한다. `CombatEntryPoint.skillDefinitions`에 mapped Film SO가 등록돼 있어야 한다.
6. Contact와 Field Attack에서 Victory, defeat/abort, Save/Load를 검증한다.

## 8. Inspector 필드 설명

| 대상 | 필드 | 규칙 |
| --- | --- | --- |
| EnemyDefinitionSO | `persistentKey` | enemy stable identity. 비어 있거나 중복 불가 |
| EnemyDefinitionSO | `acquirableSkillPersistentKeys` | resolved Film key 목록. Unique/빈/중복/모호 key 금지 |
| EnemySourceComponent | `definition` | 실제 combat enemy에 연결하는 SO 참조 |
| CombatSkillLoadoutComponent | `skillIds` | 적이 전투에서 쓸 runtime skill IDs; Film 획득 목록과 별개 |
| FieldEnemyMotor2D | `rb`, `visualRoot`, `moveSpeed`, `faceMoveDirection` | 이동 및 sprite facing |
| FieldEnemyPatrolAI2D | patrol points, detection/chase, tag | Exploration에서만 이동; points/motor 필수 |
| CombatEncounterGroup | `autoCollectChildren`, `enemies`, `encounterId`, flow mode | encounter 소유와 save identity |

## 9. Scene/Prefab 연결 방법

scene의 유일한 `CombatEntryPoint`/runtime을 사용한다. contact collider는 trigger, 적의 물리 collider/HP root는 전투 대상이 되도록 분리한다. `PlayerFieldAttackController`는 player의 기존 input route를 사용하므로 enemy마다 keyboard code를 추가하지 않는다.

## 10. 기존 시스템 등록 방법

기본 전투 스킬은 existing Registry의 SO `skillId`와 enemy loadout으로 등록한다. Film은 SO Registry + EnemyDefinition mapping + EnemySourceComponent 3개가 모두 필요하다. Quest kill은 Combat publisher 기존 설정을 사용한다.

## 11. Play Mode 검증 방법

patrol/ground collision, contact start, Field Attack start를 각각 확인한다. Victory 시 mapped defeated source만 Film을 주고, duplicate/defeat/abort는 주지 않는지 확인한다. Reward/Quest/Exploration 복귀, cleared encounter 재진입, Save/Load restore를 확인한다.

## 12. 통과 기준

적은 Exploration에서만 이동하고 정확히 한 encounter가 한 combat을 시작하며, victory/Film/Quest/Reward가 중복되지 않고 save 후 lifecycle이 유지돼야 한다.

## 13. 자주 발생하는 오류

| 증상 | 확인 |
| --- | --- |
| patrol 없음 | Rigidbody2D/motor/left-right point/GameState/Player tag |
| contact/attack 시작 실패 | collider layer/tag, group membership, HP source, existing CombatEntryPoint |
| Film 없음 | Victory인지, EnemySourceComponent가 root에 있는지, mapping key/Registry/category |
| validator 오류 | empty/duplicate enemy key, unresolved/Unique Film key |

## 14. 구현되지 않은 기능

spawn table, loot probability, random Film drop, respawn timer, boss phase AI, multiple authored reward tables는 현재 Inspector-only Production 표준이 아니다.

## 15. 수정 시 주의사항

기존 enemy loadout/encounter ID/persistent key를 바꾸지 않는다. helper child를 auto-collected combatant로 만들지 말고, `FieldEnemy` legacy path를 추가하지 않는다.

## 16. 관련 문서 링크

[스킬 가이드](GAME002_SkillContent_AuthoringGuide.md) · [Enemy 템플릿](GAME002_EnemyEncounter_PlanningTemplate.md) · [Enemy 체크리스트](GAME002_EnemyEncounter_ValidationChecklist.md) · [Dungeon 가이드](GAME002_DungeonStage_AuthoringGuide.md)
