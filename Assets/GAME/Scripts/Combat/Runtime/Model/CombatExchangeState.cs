using System;

namespace Game.Combat.Model
{
    public sealed class CombatExchangeState
    {
        public Side InitialInitiative { get; }
        public Side CurrentAttackSide { get; private set; }
        public bool IsChainActive { get; private set; }
        public ICombatant ChainOwner { get; private set; }

        public CombatExchangeState(Side initialInitiative)
        {
            if (!Enum.IsDefined(typeof(Side), initialInitiative))
                initialInitiative = Side.Allies;

            InitialInitiative = initialInitiative;
            CurrentAttackSide = initialInitiative;
        }

        public void SetAttackSide(Side side)
        {
            if (!Enum.IsDefined(typeof(Side), side))
                return;

            CurrentAttackSide = side;
        }

        public void SetChainState(bool isActive, ICombatant owner)
        {
            IsChainActive = isActive && owner != null;
            ChainOwner = IsChainActive ? owner : null;
        }
    }
}
