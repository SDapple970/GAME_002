using System.Collections.Generic;
using Game.Combat.Core;
using Game.Combat.Environment;

namespace Game.Combat.Model
{
    public sealed class CombatSession
    {
        public readonly List<ICombatant> Allies = new();
        public readonly List<ICombatant> Enemies = new();

        public InspirationPool Inspiration { get; }
        public CombatEnvironment Env { get; }
        public int TurnIndex { get; private set; }
        public StartReason StartReason { get; }
        public Side InitiativeSide { get; }
        public CombatTurn CurrentTurn { get; private set; } = new();
        public KnowledgeBook Knowledge { get; } = new KnowledgeBook();

        public CombatSession(
            StartReason reason,
            Side initiativeSide,
            InspirationPool inspiration,
            CombatEnvironment env)
        {
            StartReason = reason;
            InitiativeSide = initiativeSide;
            Inspiration = inspiration;
            Env = env;
        }

        public void BeginNewTurn()
        {
            TryBeginNewTurn();
        }

        public bool TryBeginNewTurn()
        {
            if (TurnIndex > 0 &&
                CurrentTurn != null &&
                CurrentTurn.Lifecycle != CombatTurnLifecycle.Completed)
            {
                return false;
            }

            TurnIndex++;
            CurrentTurn = new CombatTurn();
            Inspiration.GainPerTurn(1);
            return true;
        }

        public IReadOnlyList<ICombatant> GetSide(Side side)
        {
            return side == Side.Allies ? Allies : Enemies;
        }
    }
}
