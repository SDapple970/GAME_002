using System;

namespace Game.Combat.Model
{
    public sealed class CombatApproachPresentationRequest
    {
        private readonly Func<bool> _completion;
        private bool _completionRequested;

        public int ExchangeVersion { get; }
        public Phase Phase { get; }
        public CombatAttackDeclaration Declaration { get; }

        internal CombatApproachPresentationRequest(
            int exchangeVersion,
            CombatAttackDeclaration declaration,
            Func<bool> completion)
        {
            ExchangeVersion = exchangeVersion;
            Phase = Phase.Approach;
            Declaration = declaration;
            _completion = completion;
        }

        public bool TryComplete()
        {
            if (_completionRequested)
                return false;

            _completionRequested = true;
            return _completion != null && _completion.Invoke();
        }
    }
}
