using Game.Combat.Model;

namespace Game.Combat.Adapters
{
    public interface ICombatantFactory
    {
        void PopulateCombatants(CombatSession session, CombatStartRequest req);
    }
}
