using System;

namespace Game.Combat.Model
{
    /// <summary>Visual-only round snapshot. Completion gates the next skill decision.</summary>
    public sealed class CombatClashRoundPresentationRequest
    {
        private readonly Func<bool> _completion;
        private bool _completionRequested;

        public int ExchangeVersion { get; }
        public long SeriesToken { get; }
        public CombatClashRoundResult Result { get; }
        public ICombatant Attacker { get; }
        public ICombatant Responder { get; }
        public ISkill AttackSkill { get; }
        public ISkill ResponseSkill { get; }

        internal CombatClashRoundPresentationRequest(
            int exchangeVersion,
            long seriesToken,
            CombatClashRoundResult result,
            ICombatant attacker,
            ICombatant responder,
            ISkill attackSkill,
            ISkill responseSkill,
            Func<bool> completion)
        {
            ExchangeVersion = exchangeVersion;
            SeriesToken = seriesToken;
            Result = result;
            Attacker = attacker;
            Responder = responder;
            AttackSkill = attackSkill;
            ResponseSkill = responseSkill;
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
