using System.Collections.Generic;
using Game.Combat.Data; // Playbook 사용을 위해 추가

namespace Game.Combat.Model
{
    public sealed class CombatTurn
    {
        public readonly Dictionary<CombatantId, ActionPlan> Plans = new();

        // 텍스트 출력용 (레거시/디버깅용)
        public readonly List<ResolvedEvent> Events = new();

        // [NEW] 비주얼 담당자가 애니메이션을 순차 재생하기 위해 읽어갈 대본(Timeline)
        public readonly List<PlaybookEvent> Playbook = new();

        public void SetPlan(CombatantId id, ActionPlan plan) => Plans[id] = plan;
        public bool TryGetPlan(CombatantId id, out ActionPlan plan) => Plans.TryGetValue(id, out plan);
    }

    public readonly struct ResolvedEvent
    {
        public readonly string Message;
        public ResolvedEvent(string message) => Message = message;
        public override string ToString() => Message;
    }
}