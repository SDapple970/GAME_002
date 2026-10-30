using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Game.Combat.Model
{
    /// <summary>Session-owned validation gauge. Only receipts issued by its BP ledger can charge it.</summary>
    public sealed class CombatUltimateRuntime : IDisposable
    {
        public const int MaximumStage = 3;

        private readonly CombatBpRuntime _bp;
        private readonly Dictionary<ICombatant, int> _stages = new(ActorComparer.Instance);
        private readonly Dictionary<ICombatant, ISkill> _skills = new(ActorComparer.Instance);
        private readonly HashSet<long> _acceptedReceipts = new();
        private bool _disposed;

        internal CombatUltimateRuntime(CombatBpRuntime bp, CombatSession session)
        {
            _bp = bp ?? throw new ArgumentNullException(nameof(bp));
            foreach (ICombatant actor in session.Allies) _stages.Add(actor, 0);
            foreach (ICombatant actor in session.Enemies) _stages.Add(actor, 0);
            _bp.InvestmentCommitted += HandleInvestmentCommitted;
        }

        public bool TryGetStage(ICombatant actor, out int stage)
        {
            stage = 0;
            return !_disposed && actor != null && _stages.TryGetValue(actor, out stage);
        }

        public bool TryGetSkill(ICombatant actor, out ISkill skill)
        {
            skill = null;
            return !_disposed && actor != null && _skills.TryGetValue(actor, out skill);
        }

        internal bool TryAssignSkill(ICombatant actor, ISkill skill)
        {
            if (_disposed || actor == null || skill == null || !_stages.ContainsKey(actor) ||
                _skills.ContainsKey(actor) || actor.Skills == null)
                return false;

            for (int i = 0; i < actor.Skills.Count; i++)
            {
                if (!ReferenceEquals(actor.Skills[i], skill)) continue;
                _skills.Add(actor, skill);
                return true;
            }

            return false;
        }

        public bool TryAcceptApprovedSpend(CombatBpSpendResult receipt)
        {
            if (_disposed || receipt == null || receipt.Amount <= 0 || receipt.ReceiptId <= 0 ||
                receipt.Actor == null || !_stages.ContainsKey(receipt.Actor) ||
                _acceptedReceipts.Contains(receipt.ReceiptId))
                return false;

            bool issuedByLedger = false;
            IReadOnlyList<CombatBpSpendResult> approved = _bp.ApprovedSpends;
            for (int i = 0; i < approved.Count; i++)
            {
                if (!ReferenceEquals(approved[i], receipt)) continue;
                issuedByLedger = true;
                break;
            }

            if (!issuedByLedger) return false;
            _acceptedReceipts.Add(receipt.ReceiptId);
            _stages[receipt.Actor] = Math.Min(MaximumStage,
                _stages[receipt.Actor] + receipt.Amount);
            return true;
        }

        internal bool TryConsumeReadyStage(ICombatant actor)
        {
            if (!TryGetStage(actor, out int stage) || stage != MaximumStage)
                return false;
            _stages[actor] = 0;
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _bp.InvestmentCommitted -= HandleInvestmentCommitted;
            _acceptedReceipts.Clear();
            _skills.Clear();
            _stages.Clear();
        }

        private void HandleInvestmentCommitted(CombatBpSpendResult receipt)
        {
            TryAcceptApprovedSpend(receipt);
        }

        private sealed class ActorComparer : IEqualityComparer<ICombatant>
        {
            public static readonly ActorComparer Instance = new();
            public bool Equals(ICombatant left, ICombatant right) => ReferenceEquals(left, right);
            public int GetHashCode(ICombatant actor) => RuntimeHelpers.GetHashCode(actor);
        }
    }
}
