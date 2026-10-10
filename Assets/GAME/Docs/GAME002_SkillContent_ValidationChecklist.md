# GAME_002 Skill Content Validation Checklist

> 콘텐츠 단위 또는 콘텐츠 묶음마다 복사하여 기록한다.  
> 구현 상태와 검증 상태는 다르다. Inspector 입력이 끝났어도 Play Mode / 통합 검증 전에는 완료로 표시하지 않는다.

## 0. 검수 대상

| 항목 | 내용 |
| --- | --- |
| 콘텐츠 묶음 | |
| Skill SO 경로 | |
| Enemy Definition SO 경로 | |
| Character Progression SO 경로 | |
| 대상 Scene/Prefab | |
| Unity 버전 | 6000.3.23f1 |
| 담당자 / 날짜 | |

## 1. 제작 상태

| 항목 | 상태 | 근거 / 경로 | 확인자 |
| --- | --- | --- | --- |
| 기획 작성 | 미시작 / 진행 / 완료 | | |
| 기획 승인 | 미승인 / 승인 | | |
| 필요 에셋 준비 | 미준비 / 진행 / 완료 | | |
| Unity SO 생성 | 미시작 / 완료 | | |
| Inspector 설정 | 미시작 / 완료 | | |
| Combat Registry 등록 | 미시작 / 완료 | `CombatEntryPoint.skillDefinitions` | |
| Scene/Prefab 연결 | 미시작 / 완료 / 해당 없음 | `EnemySourceComponent.definition` 등 | |
| 구현 상태 | Not Implemented / In Progress / Implementation Complete / Needs Fix | | |

## 2. 데이터·Inspector 확인

### 모든 SkillDefinitionSO

- [ ] `skillId`가 양수이고 대상 Combat Registry에서 중복되지 않는다.
- [ ] `persistentKey`가 비어 있지 않고 모든 Skill SO에서 중복되지 않는다.
- [ ] `displayName`, `ownershipCategory`, 비용, 대상, 수치가 승인 기획과 일치한다.
- [ ] `ownershipCategory = Compatibility`이면 Unique owner가 비어 있다.
- [ ] `ownershipCategory = Film`이면 Film 획득 매핑과 Registry 등록이 모두 있다.
- [ ] `ownershipCategory = Unique`이면 `uniqueOwnerCharacterId`가 비어 있지 않다.
- [ ] Animation trigger, VFX/SFX, Status SO 참조가 있으면 실제 에셋이며 Missing이 아니다.
- [ ] 기존 persistent key, `skillId`, asset path, `.meta` GUID를 변경하지 않았다.

### Film / Enemy

- [ ] `EnemyDefinitionSO.persistentKey`가 비어 있지 않고 EnemyDefinition 전체에서 고유하다.
- [ ] `acquirableSkillPersistentKeys`의 모든 값이 비어 있지 않고 한 적 안에서 중복되지 않는다.
- [ ] 각 mapped key는 정확히 하나의 Registry/asset Skill SO로 해석된다.
- [ ] mapped Skill은 `Film`이고 `Unique`가 아니다.
- [ ] 실제 전투 적 GameObject에 `EnemySourceComponent`가 하나 있으며 올바른 `definition`을 참조한다.
- [ ] 적의 기본 전투 loadout과 획득 Film 정보를 혼동하지 않았다.

### Unique / Character progression

- [ ] `CharacterProgressionDefinitionSO.characterId`가 비어 있지 않고 캐릭터 정의 전체에서 고유하다.
- [ ] `Unique Skill Unlocks`는 최대 6개이며 동일 key가 중복되지 않는다.
- [ ] 각 Unique key는 `ownershipCategory = Unique`이고 `uniqueOwnerCharacterId == characterId`다.
- [ ] `enabled`, 표시 순서, 조건, 레벨이 승인 기획과 일치한다.
- [ ] Level 조건은 5/10/20/40만 사용한다.
- [ ] 미정 항목은 `Unconfigured`으로 남겼으며 임의 조건을 넣지 않았다.

## 3. 자동 검증 기록

모든 메뉴는 Play Mode 밖, 저장된 Scene에서 실행한다.

| 메뉴 | 결과 | Console/로그 근거 | 실행자/날짜 |
| --- | --- | --- | --- |
| `GAME > Validation > Combat Skill Persistent Identities` | PASS / FAIL / 미실행 | | |
| `GAME > Validation > Enemy Skill Acquisition` | PASS / FAIL / 미실행 | | |
| `GAME > Validation > Character Unique Skills` | PASS / FAIL / 미실행 | | |
| `GAME > Verification > Audit Film Unique Production Content` | PASS / FAIL / 미실행 | `Logs/Combat17C6_ContentAudit.md` 참고 가능 | |
| `Tools > GAME > Validate Production UI Routing` | PASS / FAIL / 해당 없음 | | |
| `GAME > Production Migration > Validate Dungeon 1 Production Scene` | PASS / FAIL / 해당 없음 | | |

자동 검증 PASS는 데이터 형식/연결의 근거다. 밸런스, 연출 품질, Contact/Field Attack 실제 동작, Save/Load 회귀를 대신하지 않는다.

## 4. Play Mode 검증 기록

| 시나리오 | 기대 결과 | 결과 | 증거 / 재현 절차 |
| --- | --- | --- | --- |
| Contact 전투 진입 | 실제 `CombatEntryPoint`가 전투를 시작 | PASS / FAIL / 미실행 | |
| Field Attack 전투 진입 | `PlayerFieldAttackController` hit가 전투를 시작 | PASS / FAIL / 미실행 | |
| 스킬 선택·실행 | 대상/비용/피해/상태/표현이 기획과 일치 | PASS / FAIL / 미실행 | |
| Film 획득 | Victory + defeated mapped enemy에서만 공유 Film 추가 | PASS / FAIL / 미실행 | |
| Film 비획득 | 패배/중단/미매핑/살아 있는 적에서 Film 미추가 | PASS / FAIL / 미실행 | |
| Film 장착·해제 | 파티원별 독립 장착, 같은 캐릭터 중복 장착 불가 | PASS / FAIL / 미실행 | |
| Film 전투 snapshot | 장착한 캐릭터의 다음 전투에만 포함 | PASS / FAIL / 미실행 | |
| Unique 잠금 | 해금 레벨 전에는 UI와 전투 snapshot에서 잠김 | PASS / FAIL / 미실행 | |
| Unique 해금 | 소유 캐릭터가 5/10/20/40에 도달하면 UI/전투에 포함 | PASS / FAIL / 미실행 | |
| Unique 소유 제한 | 다른 캐릭터는 해당 Unique를 사용하지 않음 | PASS / FAIL / 미실행 | |
| Reward / Quest / Exploration | 전투 종료 후 기존 흐름이 정상 복귀 | PASS / FAIL / 미실행 | |
| Save/Load | 공유 Film, 장착, 캐릭터 진행도/Unique 상태가 Cold Load 뒤 유지 | PASS / FAIL / 미실행 | |

## 5. 통합 완료 판정

| 검증 층 | 상태 | 근거 |
| --- | --- | --- |
| Compile | Unverified / Verified / Failed | |
| Inspector / serialized asset | Unverified / Verified / Failed | |
| EditMode validator | Unverified / Verified / Failed | |
| Play Mode | Unverified / Verified / Failed | |
| Save/Load integration | Unverified / Verified / Failed | |
| 기존 Reward/Quest/Exploration 회귀 | Unverified / Verified / Failed | |
| 권장 tracking 상태 | Implementation Complete — Runtime Verification Required / Integration Verified / Needs Fix | |

## 6. 실패 처리와 인계

- [ ] Console error/warning과 관련 SO/Scene 경로를 기록했다.
- [ ] persistent key/skillId/GUID를 임의 변경해 우회하지 않았다.
- [ ] 새 Manager/EntryPoint/직접 GameState writer를 추가하지 않았다.
- [ ] 코드가 필요한 요구는 별도 기능 요청으로 분리했다.
- [ ] 수정한 Unity asset, Inspector 연결, 생성한 asset과 `.meta`를 모두 기록했다.
- [ ] 기존 사용자 변경과 관련 없는 파일은 건드리지 않았다.

## 7. 최종 서명

| 역할 | 이름 | 날짜 | 판정 / 메모 |
| --- | --- | --- | --- |
| 기획 | | | |
| Unity 적용 | | | |
| QA / Play Mode 검수 | | | |
| 기술 검토 | | | |
