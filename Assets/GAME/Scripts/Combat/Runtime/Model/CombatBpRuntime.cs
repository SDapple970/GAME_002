using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Game.Combat.Model
{
    public sealed class CombatBpSpendResult
    {
        public long ReceiptId { get; }
        public ICombatant Actor { get; }
        public int Amount { get; }

        internal CombatBpSpendResult(long receiptId, ICombatant actor, int amount)
        {
            ReceiptId = receiptId;
            Actor = actor;
            Amount = amount;
        }
    }

    /// <summary>Session-owned BP ledger and independent attack-cycle clock.</summary>
    public sealed class CombatBpRuntime
    {
        private readonly Dictionary<ICombatant, int> _balances = new(ActorReferenceComparer.Instance);
        private readonly int[] _lastCycleIds = new int[2];
        private readonly List<CombatBpSpendResult> _spends = new();
        private readonly Dictionary<ISkill, CombatBpEnhancementEntry> _enhancements =
            new(SkillReferenceComparer.Instance);
        private readonly int[] _pendingCycles = new int[2];
        private readonly HashSet<ICombatant> _initialBalanceActors = new(ActorReferenceComparer.Instance);
        private long _nextReceiptId = 1;

        public CombatBpValidationConfig Config { get; }
        public int RoundCycleCount { get; private set; }
        public int RoundCyclesUntilGain => Config.RoundCyclesPerGain -
            RoundCycleCount % Config.RoundCyclesPerGain;
        public IReadOnlyList<CombatBpSpendResult> ApprovedSpends => _spends.AsReadOnly();
        public event Action<CombatBpSpendResult> InvestmentCommitted;

        internal CombatBpRuntime(CombatBpValidationConfig config, CombatSession session,
            IReadOnlyList<CombatBpEnhancementEntry> enhancements)
        {
            Config = config;
            foreach (ICombatant actor in session.Allies) _balances.Add(actor, 0);
            foreach (ICombatant actor in session.Enemies) _balances.Add(actor, 0);
            if (enhancements == null) return;
            for (int i = 0; i < enhancements.Count; i++)
            {
                CombatBpEnhancementEntry entry = enhancements[i];
                if (entry.Skill != null && entry.Kind != CombatBpEnhancementKind.None &&
                    entry.PerPoint > 0 && !_enhancements.ContainsKey(entry.Skill))
                    _enhancements.Add(entry.Skill, entry);
            }
        }

        public bool TryGetEnhancement(ISkill skill, out CombatBpEnhancementKind kind, out int perPoint)
        {
            if (skill != null && _enhancements.TryGetValue(skill, out CombatBpEnhancementEntry entry))
            {
                kind = entry.Kind;
                perPoint = entry.PerPoint;
                return true;
            }
            kind = CombatBpEnhancementKind.None;
            perPoint = 0;
            return false;
        }

        public bool TryGetBalance(ICombatant actor, out int balance)
        {
            balance = 0;
            return actor != null && _balances.TryGetValue(actor, out balance);
        }

        public bool CanInvest(ICombatant actor, int amount)
        {
            return amount >= 0 && amount <= Config.MaximumInvestment &&
                TryGetBalance(actor, out int balance) && balance >= amount;
        }

        /// <summary>One-time validation setup. This creates no spend receipt and never charges Ultimate.</summary>
        internal bool TrySetInitialBalanceForValidation(ICombatant actor, int amount)
        {
            if (amount < 0 || amount > Config.MaximumBp || actor == null ||
                !_balances.ContainsKey(actor) || !_initialBalanceActors.Add(actor))
                return false;

            _balances[actor] = amount;
            return true;
        }

        internal void CommitInvestment(ICombatant actor, int amount)
        {
            if (!CanInvest(actor, amount))
                throw new InvalidOperationException("BP changed after synchronous commit validation.");
            if (amount == 0) return;
            _balances[actor] -= amount;
            CombatBpSpendResult receipt = new CombatBpSpendResult(_nextReceiptId++, actor, amount);
            _spends.Add(receipt);
            InvestmentCommitted?.Invoke(receipt);
        }

        internal bool RecordAttackCycle(Side side, int cycleId, bool successful, CombatSession session)
        {
            if (!successful || cycleId <= 0 || (side != Side.Allies && side != Side.Enemies))
                return false;
            int index = (int)side;
            if (_lastCycleIds[index] >= cycleId) return false;
            _lastCycleIds[index] = cycleId;
            if (_pendingCycles[index] < int.MaxValue) _pendingCycles[index]++;
            if (_pendingCycles[0] == 0 || _pendingCycles[1] == 0) return true;

            _pendingCycles[0]--;
            _pendingCycles[1]--;
            RoundCycleCount++;
            if (RoundCycleCount % Config.RoundCyclesPerGain != 0) return true;
            foreach (ICombatant actor in session.Allies) GrantIfActive(actor);
            foreach (ICombatant actor in session.Enemies) GrantIfActive(actor);
            return true;
        }

        private void GrantIfActive(ICombatant actor)
        {
            if (actor == null || actor.HP <= 0 || actor.IsStunned || !_balances.ContainsKey(actor))
                return;
            _balances[actor] = Math.Min(Config.MaximumBp, _balances[actor] + 1);
        }

        private sealed class ActorReferenceComparer : IEqualityComparer<ICombatant>
        {
            public static readonly ActorReferenceComparer Instance = new();
            public bool Equals(ICombatant left, ICombatant right) => ReferenceEquals(left, right);
            public int GetHashCode(ICombatant actor) => RuntimeHelpers.GetHashCode(actor);
        }

        private sealed class SkillReferenceComparer : IEqualityComparer<ISkill>
        {
            public static readonly SkillReferenceComparer Instance = new();
            public bool Equals(ISkill left, ISkill right) => ReferenceEquals(left, right);
            public int GetHashCode(ISkill skill) => RuntimeHelpers.GetHashCode(skill);
        }
    }
}
