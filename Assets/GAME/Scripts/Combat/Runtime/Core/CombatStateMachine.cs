using System;
using System.Linq;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    public sealed class CombatStateMachine
    {
        public Phase Phase { get; private set; } = Phase.EnterCombat;
        private CombatSession _session;

        // ✅ 시각화 디렉터에게 연출 실행을 지시하는 이벤트 
        // (세션 데이터와, 연출 완료 시 호출할 콜백 함수를 함께 전달합니다)
        public event Action<CombatSession, Action> OnRequireResolutionPlay;

        private bool _isResolving = false;

        public CombatStateMachine(CombatSession session)
        {
            _session = session;
        }

        // [에러(CS1729) 수정] CombatBootstrapper 및 TestRunner와의 호환성을 위한 생성자
        // 기존에 넘겨주던 2개의 추가 인자(레거시 콜백 등)를 허용하되 무시하고 세션만 받도록 처리합니다.
        public CombatStateMachine(CombatSession session, object legacyArg1, object legacyArg2)
        {
            _session = session;
        }

        public void ConfirmPlanning()
        {
            if (Phase == Phase.Planning)
            {
                Phase = Phase.Resolution;
                _isResolving = false;
            }
        }

        public void Tick()
        {
            switch (Phase)
            {
                case Phase.EnterCombat:
                    _session.BeginNewTurn();
                    Phase = Phase.Planning;
                    break;

                case Phase.Planning:
                    // 플레이어의 UI 입력(Confirm)을 대기하는 상태
                    // CombatPlanningHUD에서 ConfirmPlanning()을 호출할 때까지 대기
                    break;

                case Phase.Resolution:
                    // 연출이 진행 중이 아니라면 디렉터에게 연출 지시
                    if (!_isResolving)
                    {
                        _isResolving = true;
                        // 디렉터가 연출을 끝마치면 OnResolutionFinished를 호출하게 콜백으로 넘겨줌
                        OnRequireResolutionPlay?.Invoke(_session, OnResolutionFinished);
                    }
                    break;

                case Phase.EndTurn:
                    // 모든 연출이 끝난 후, 전투 종료 조건 확인
                    CheckCombatEndConditions();

                    // 전투가 끝나지 않았다면 다음 턴으로
                    if (Phase != Phase.ExitCombat)
                    {
                        _session.BeginNewTurn();
                        Phase = Phase.Planning;
                    }
                    break;

                case Phase.ExitCombat:
                    // 전투 종료. EntryPoint에서 감지하여 필드로 복귀시킴
                    break;
            }
        }

        private void OnResolutionFinished()
        {
            // 디렉터가 연출을 모두 끝내면 이 콜백이 실행되어 페이즈가 넘어갑니다.
            Phase = Phase.EndTurn;
        }

        private void CheckCombatEndConditions()
        {
            bool alliesDead = _session.Allies.All(a => a.HP <= 0);
            bool enemiesDead = _session.Enemies.All(e => e.HP <= 0);

            if (alliesDead || enemiesDead)
            {
                Phase = Phase.ExitCombat;
            }
        }
    }
}