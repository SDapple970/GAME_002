namespace Game.Combat.Model
{
    public sealed class CombatPostureResult
    {
        public ICombatant Target { get; }
        public int PostureBefore { get; }
        public int PostureAfter { get; }
        public int PostureApplied { get; }
        public bool ReachedMaximum { get; }

        internal CombatPostureResult(
            ICombatant target,
            int postureBefore,
            int postureAfter,
            bool reachedMaximum)
        {
            Target = target;
            PostureBefore = postureBefore;
            PostureAfter = postureAfter;
            PostureApplied = postureAfter > postureBefore ? postureAfter - postureBefore : 0;
            ReachedMaximum = reachedMaximum;
        }
    }
}
