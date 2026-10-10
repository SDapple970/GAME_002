# GAME_002 Skill Content Planning Template

> 복사해서 콘텐츠 기획서에 사용한다.  
> `콘텐츠 임시 ID`는 사람을 위한 추적 ID다. Unity persistent key, `skillId`, Unity GUID는 Unity 적용 담당자가 Registry를 확인한 뒤 별도로 배정한다.

## 0. 요청 정보

| 항목 | 내용 |
| --- | --- |
| 묶음/버전 | |
| 기획 담당 | |
| Unity 적용 담당 | |
| 기획 승인자 / 승인일 | |
| 대상 빌드/챕터 | |
| 참조 전투 설계/근거 | |
| 미결정 사항 | |

## 1. Skill Catalog

| 콘텐츠 임시 ID | 스킬 이름 | 스킬 분류 | 설명 | 사용 캐릭터 | 획득 방법 | 사용 조건 | 비용 | 대상 | 피해/회복 수치 | 상태이상 | 추가 효과 | 애니메이션 | VFX/SFX | 필요 에셋 | 구현 지원 여부 | 기획 상태 | 검수 상태 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 예: EX-FILM-001 | Example Film | Film | Example/Test 전용 | 장착한 파티원 | Enemy victory | 전투 중 | 영감 1 | 단일 적 | 피해 3 | 없음 | 없음 | Attack | 없음 | SO, 아이콘 | 지원 | 예시 | 미검수 |
| | | Compatibility / Film / Unique | | | 기본/적 Victory/레벨 해금 | | 영감 / MP / Final MP | 현재 targeting enum 안에서 | 현재 `baseDamage` 기준. 회복은 기능 요청 | Status SO 또는 없음 | 현재 구현/기능 요청 구분 | Animator trigger | Cast/Impact | prefab/clip/status SO | 지원 / 추가 코드 필요 / 미정 | 초안/승인/보류 | 미검수/자동 통과/Play Mode 통과 |

작성 메모:

- Film은 **실제 처치한 mapped enemy의 Victory**로만 획득한다. 확률, 도주, 패배, 대화 보상은 `추가 코드 필요`로 표시한다.
- Unique는 캐릭터별 전용이다. 실제 지원되는 level 해금은 5, 10, 20, 40이다.
- 피해/회복 칸에서 회복을 요구할 때는 Inspector-only 지원으로 가정하지 말고 추가 기능 요청으로 표시한다.
- `사용 캐릭터`는 기획상 이름과 캐릭터 stable ID 후보를 함께 쓰되, 최종 Unity ID는 적용 담당자가 existing progression/party data와 대조한다.

## 2. Enemy Film Acquisition

| Enemy 이름 | Enemy stable identity | 획득 가능한 Film | 획득 조건 | 기획 근거 | Production 연결 여부 |
| --- | --- | --- | --- | --- | --- |
| 예: Example Enemy | `enemy.example` (예시, 확정 ID 아님) | EX-FILM-001 | Victory + 실제 defeated source | 문서/전투 설계 링크 | Example/Test only |
| | | | 현재 지원: Victory + defeated enemy source + Film mapping | | 미연결 / 연결 예정 / Play Mode 확인 |

작성 메모:

- Enemy stable identity와 Film persistent key는 기획자가 확정값을 생성하지 않는다. 기획 문서에는 후보 또는 콘텐츠 임시 ID를 쓰고 Unity 담당자가 Registry/전체 asset 중복을 검사한다.
- 적의 기본 공격 스킬과 획득 Film은 다른 표로 관리한다.
- 같은 Film을 여러 적이 주는 설계는 가능하지만, 중복 획득 시 공유 보유 상태는 하나로 유지된다. 의도와 근거를 적는다.

## 3. Character Unique Skill

| Character | Unique Skill | 소유 Character ID | 표시 순서 | 해금 조건 | 해금 레벨 | 스킬 효과 | 콘텐츠 확정 여부 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 예: Example Hero | EX-UNIQUE-001 | `character.example` (예시) | 1 | Level | 5 | 피해 5 | Example/Test only |
| | | 적용 담당자 확인 | 1–6 | Level / Unconfigured | 5 / 10 / 20 / 40 / 미정 | Skill Catalog 참조 | 초안/승인/보류 |

작성 메모:

- Unique는 최대 6개까지 표시 순서대로 작성한다.
- 레벨 5/10/20/40은 현재 지원된다. 추가 두 스킬의 해금 조건은 미정이면 `Unconfigured`, 해금 레벨은 비워 둔다. UI에는 “조건 미정”으로 표시된다.
- 퀘스트, 아이템, 친밀도, 확률, 다른 레벨을 해금 조건으로 적으면 `추가 코드 필요`로 별도 요청한다.

## 4. 연출·에셋 요청

| 콘텐츠 임시 ID | Animator trigger | Cast VFX | Impact VFX | Cast SFX | Impact SFX | 담당 | 납기 | 참조 경로/링크 | 준비 상태 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| | 기존 trigger 재사용 / 신규 요청 | | | | | | | | 미요청/제작 중/완료 |

## 5. 개발 요청으로 분리할 기능

| 요청 ID | 원하는 규칙 | 왜 Inspector만으로 불가한가 | UI 표시 | Save/Load 영향 | 전투/Reward/Quest 영향 | 승인 상태 |
| --- | --- | --- | --- | --- | --- | --- |
| | | | | | | 초안/승인/보류 |

## 6. 전달 전 확인

- [ ] 모든 신규 콘텐츠에 임시 ID가 있다.
- [ ] Film은 Enemy Film Acquisition 표에 Victory 조건과 기획 근거가 있다.
- [ ] Unique는 소유 캐릭터, 표시 순서, 5/10/20/40 또는 Unconfigured가 명시됐다.
- [ ] 수치, 대상, 비용, 상태, 연출의 미정 항목이 숨겨지지 않았다.
- [ ] persistent key, `skillId`, Unity GUID 생성/변경을 기획 범위로 요구하지 않았다.
- [ ] 추가 코드가 필요한 기능은 별도 요청으로 분리했다.
