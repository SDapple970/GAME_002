namespace Game.Combat.Core
{
    public static class StaggerSystem
    {
        public static void AddStagger(Model.ICombatant target, int amount)
        {
            if (amount <= 0) return;
            target.AddStagger(amount);

            // 최대치 도달 시 1턴 기절(무력화) — 고정 규칙
            if (target.Stagger >= target.StaggerMax)
            {
                target.SetStunned(true);
            }
        }

        public static void ClearStunAtTurnEnd(Model.ICombatant target)
        {
            // “기절 1턴” 규칙 처리:
            // - 기절은 다음 턴 Planning에서 행동 슬롯이 None으로 고정되게 하거나
            // - 여기서 턴 종료 시 풀어도 되는데, MVP에선 “턴 종료 시 해제”로 단순화
            // (UI/연출에서 기절 턴을 보여주기 쉬운 쪽으로 나중에 조정 가능)
            target.SetStunned(false);
            target.ResetStaggerIfNeededOnStunEnd();
        }
    }
}
