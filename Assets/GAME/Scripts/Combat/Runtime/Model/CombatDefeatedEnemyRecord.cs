namespace Game.Combat.Model
{
    public sealed class CombatDefeatedEnemyRecord
    {
        public int CombatantId { get; }
        public EnemySourceSnapshot Source { get; }

        public CombatDefeatedEnemyRecord(int combatantId, EnemySourceSnapshot source)
        {
            CombatantId = combatantId;
            Source = source;
        }
    }
}
