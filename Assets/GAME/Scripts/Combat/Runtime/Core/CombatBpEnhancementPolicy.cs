using Game.Combat.Model;

namespace Game.Combat.Core
{
    public static class CombatBpEnhancementPolicy
    {
        public static bool CanInvest(CombatSession session, ISkill skill, int amount)
        {
            if (amount == 0) return true;
            return amount > 0 && TryGetProfile(session, skill, out _, out _);
        }

        public static int Magnitude(CombatSession session, ISkill skill, int amount,
            CombatBpEnhancementKind kind)
        {
            if (amount <= 0 || !TryGetProfile(session, skill, out CombatBpEnhancementKind actual,
                    out int perPoint) || actual != kind)
                return 0;
            long value = (long)amount * perPoint;
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        public static bool TryGetProfile(CombatSession session, ISkill skill,
            out CombatBpEnhancementKind kind, out int perPoint)
        {
            kind = CombatBpEnhancementKind.None;
            perPoint = 0;
            if (session?.BpRuntime == null || skill == null) return false;
            if (session.BpRuntime.TryGetEnhancement(skill, out kind, out perPoint)) return true;
            if (skill is not ICombatBpEnhancementProvider provider ||
                provider.BpEnhancementKind == CombatBpEnhancementKind.None ||
                provider.BpEnhancementPerPoint <= 0) return false;
            kind = provider.BpEnhancementKind;
            perPoint = provider.BpEnhancementPerPoint;
            return true;
        }
    }
}
