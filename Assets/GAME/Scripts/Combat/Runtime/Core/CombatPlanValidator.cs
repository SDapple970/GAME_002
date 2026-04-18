// GAME_002/Assets/GAME/Scripts/Combat/Core/CombatPlanValidator.cs
using Game.Combat.Model;

namespace Game.Combat.Core
{
    public static class CombatPlanValidator
    {
        public static bool ValidatePlayerDraft(
            CombatSession session,
            CombatPlanDraft draft,
            ICombatant actor,
            out string errorMessage)
        {
            errorMessage = null;

            if (session == null)
            {
                errorMessage = "세션이 없습니다.";
                return false;
            }

            if (draft == null)
            {
                errorMessage = "입력 초안이 없습니다.";
                return false;
            }

            if (actor == null)
            {
                errorMessage = "행동 주체가 없습니다.";
                return false;
            }

            if (!draft.TryGetPlan(actor.Id, out var plan))
            {
                errorMessage = "선택된 아군의 행동이 비어 있습니다.";
                return false;
            }

            if (plan.Slot1.IsNone && plan.Slot2.IsNone)
            {
                errorMessage = "최소 1개 이상의 행동을 지정해야 합니다.";
                return false;
            }

            return true;
        }
    }
}