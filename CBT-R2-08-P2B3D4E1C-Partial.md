# CBT-R2-08-P2B3D-4E1C — 부분 진행

완료되지 않았다. 승인된 두 Skill SO와 기존 `FinalCombatRuntimeConfigSO`를 사용하는 비활성 후보 `Assets/GAME/Data/Combat/Runtime/FinalCombatRuntimeConfig_AnandStart_PlaytestV1_Candidate.asset`을 추가했다. Normal 10001 2장, Clash 10002 3장이고 슬롯은 Normal2/Clash3이다. MP2, BP 및 Clash3/3·최대3·5초는 기존 후보의 값이다. GUID `44a8b6f4c6314396953f0792e126d13a`, 활성화 false, 기존 Production 참조에는 연결하지 않았다. Seed0은 기존 후보의 설정이며 별도 정식 shuffle 정책 승인으로 해석하지 않는다.

New Game 지급·장착 코드와 후보 Start 데이터는 아직 구현하지 않았다. 기존 `SaveLoadService.TryResetForNewGame`는 Title 상태에서 모든 `INewGameRuntimeReset`을 호출하고 요청 식별자가 없다. `CharacterSkillSaveParticipant.ResetForNewGame`는 Runtime을 비운다. 따라서 동일 요청 재전송과 명시적 새 게임 재시작의 구분 및 중복 요청에서의 사용자 변경 보존을 충족하는 연결을 설계·구현하고, Load가 이를 호출하지 않는 것을 검증해야 한다. 단순히 전투/씬 시작마다 지급하거나 Reset에 무조건 덮어쓰는 코드는 추가하지 않았다. 기존 CharacterStart SO에는 초기 Skill 목록 필드가 없다.

기존 CharacterSkillRuntime은 보유 key와 장착을 저장하고 Film 공유 소유권과 분리한다. 카드 copies는 Config가 소유하고 전투 Hand/Discard는 CombatSession에서 생성된다. 카드 copies를 별도 영구 인벤토리로 저장하는 새 구조는 추가하지 않았다. 후보의 개별 카드·Snapshot·Save/Load 실제 통합은 아직 검증하지 않았다.

루트 AGENTS.md 적용, 중첩 없음. branch main / HEAD b42544824838e7de0e70b71afc2ca21a59db8192. 새 후보와 meta만 추가했으며 기존 Scene/Prefab/SO/Runtime·Quest/Reward/Save·Legacy와 사용자 변경은 수정하지 않았다. 추가한 GUID는 다른 meta와 충돌하지 않는 것을 확인했다.

Unity 6000.3.23f1에서 새 후보 import 및 기존 CombatPlaytestV1ConfigTests **22/22 PASS**, C# 오류/경고 없음. 이 테스트는 기존 fixture의 테스트이며 새 후보의 실전 실행 또는 New Game 지급 검증이 아니다. `git diff --check` 오류 없음, 기존 줄바꿈 안내 출력. 기존 실패6건과 전체 선택 회귀는 이번에 재실행하지 않았다. D4E0R1 이전 결과는 989 PASS / 6 FAIL / 1 IGNORED이다.

남은 작업: New Game 요청 식별/재시도 계약, 기존 Owner의 데이터 주도 초기 지급, 새 Start 후보와 Save 호환 검사, 실제 후보 기반 Hand/카드·MP·Discard 테스트, 재전투/씬 이동/Cold Load 및 기존 회귀. Production Inspector 연결·실제 Play Mode·Cutover 미수행. 개발현황: **In Progress — Candidate Imported; New Game Implementation and Verification Pending**.
