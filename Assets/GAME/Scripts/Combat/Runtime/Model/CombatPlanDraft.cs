// GAME_002/Assets/GAME/Scripts/Combat/Model/CombatPlanDraft.cs
using System.Collections.Generic;

namespace Game.Combat.Model
{
    public sealed class CombatPlanDraft
    {
        private readonly Dictionary<CombatantId, ActionPlan> _plans = new();

        public void Clear()
        {
            _plans.Clear();
        }

        public bool TryGetPlan(CombatantId actorId, out ActionPlan plan)
        {
            return _plans.TryGetValue(actorId, out plan);
        }

        public void EnsureActor(CombatantId actorId)
        {
            if (_plans.ContainsKey(actorId))
                return;

            _plans[actorId] = new ActionPlan(PlannedAction.None, PlannedAction.None);
        }

        public void SetSlot(CombatantId actorId, int slotIndex, PlannedAction action)
        {
            EnsureActor(actorId);

            var current = _plans[actorId];
            var slot1 = current.Slot1;
            var slot2 = current.Slot2;

            if (slotIndex == 0) slot1 = action;
            else slot2 = action;

            _plans[actorId] = new ActionPlan(slot1, slot2);
        }

        public IReadOnlyDictionary<CombatantId, ActionPlan> Plans => _plans;
    }
}