using System;
using System.Collections.Generic;

namespace Game.Combat.Model
{
    public enum CombatCardKind
    {
        Normal = 0,
        Clash = 1
    }

    public readonly struct CombatHandEntry
    {
        public ISkill Skill { get; }
        public CombatCardKind Kind { get; }

        public CombatHandEntry(ISkill skill, CombatCardKind kind)
        {
            Skill = skill;
            Kind = kind;
        }
    }

    public sealed class CombatHandCard
    {
        public long RuntimeId { get; }
        public ISkill Skill { get; }
        public CombatCardKind Kind { get; }

        internal CombatHandCard(long runtimeId, ISkill skill, CombatCardKind kind)
        {
            RuntimeId = runtimeId;
            Skill = skill;
            Kind = kind;
        }
    }

    /// <summary>Per-combatant, per-session card locations. Entries and skill references are snapshotted at construction.</summary>
    public sealed class CombatHandRuntime
    {
        private readonly List<CombatHandCard> _normalDraw = new();
        private readonly List<CombatHandCard> _clashDraw = new();
        private readonly List<CombatHandCard> _normalDiscard = new();
        private readonly List<CombatHandCard> _clashDiscard = new();
        private readonly List<CombatHandCard> _hand = new();
        private readonly CombatHandCard[] _deckSnapshot;
        private readonly int _normalCapacity;
        private readonly int _clashCapacity;
        private uint _randomState;

        public IReadOnlyList<CombatHandCard> DeckSnapshot => Array.AsReadOnly(_deckSnapshot);
        public IReadOnlyList<CombatHandCard> Hand => _hand.ToArray();
        public IReadOnlyList<CombatHandCard> NormalDiscard => _normalDiscard.ToArray();
        public IReadOnlyList<CombatHandCard> ClashDiscard => _clashDiscard.ToArray();
        public int NormalDrawCount => _normalDraw.Count;
        public int ClashDrawCount => _clashDraw.Count;
        public int RefillCount { get; private set; }

        public CombatHandRuntime(
            IEnumerable<CombatHandEntry> entries,
            int normalCapacity,
            int clashCapacity,
            int seed,
            long firstRuntimeId = 1)
        {
            if (entries == null || normalCapacity < 0 || clashCapacity < 0 ||
                firstRuntimeId <= 0)
                throw new ArgumentException("A hand requires entries, nonnegative capacities, and a positive first ID.");

            _normalCapacity = normalCapacity;
            _clashCapacity = clashCapacity;
            _randomState = unchecked((uint)seed);
            List<CombatHandCard> snapshot = new();
            foreach (CombatHandEntry entry in entries)
            {
                if (entry.Skill == null || !Enum.IsDefined(typeof(CombatCardKind), entry.Kind) ||
                    firstRuntimeId == long.MaxValue)
                    throw new ArgumentException("Every card needs a skill, a valid kind, and a unique runtime ID.");

                CombatHandCard card = new CombatHandCard(firstRuntimeId++, entry.Skill, entry.Kind);
                snapshot.Add(card);
                DrawPile(entry.Kind).Add(card);
            }

            _deckSnapshot = snapshot.ToArray();
            Shuffle(_normalDraw);
            Shuffle(_clashDraw);
            Fill();
        }

        public bool TryFindInHand(ISkill skill, CombatCardKind kind, long? runtimeId,
            out CombatHandCard card)
        {
            for (int i = 0; i < _hand.Count; i++)
            {
                CombatHandCard candidate = _hand[i];
                if (ReferenceEquals(candidate.Skill, skill) && candidate.Kind == kind &&
                    (!runtimeId.HasValue || candidate.RuntimeId == runtimeId.Value))
                {
                    card = candidate;
                    return true;
                }
            }

            card = null;
            return false;
        }

        internal void Commit(CombatHandCard card)
        {
            if (card == null || !_hand.Remove(card))
                throw new InvalidOperationException("A validated card changed before synchronous commit.");
            DiscardPile(card.Kind).Add(card);
        }

        internal void Refill()
        {
            RefillCount++;
            Fill();
        }

        private void Fill()
        {
            FillKind(CombatCardKind.Clash, _clashCapacity);
            FillKind(CombatCardKind.Normal, _normalCapacity);
        }

        private void FillKind(CombatCardKind kind, int capacity)
        {
            int count = 0;
            for (int i = 0; i < _hand.Count; i++)
                if (_hand[i].Kind == kind) count++;

            List<CombatHandCard> draw = DrawPile(kind);
            List<CombatHandCard> discard = DiscardPile(kind);
            while (count < capacity)
            {
                if (draw.Count == 0)
                {
                    if (discard.Count == 0) break;
                    draw.AddRange(discard);
                    discard.Clear();
                    Shuffle(draw);
                }

                int last = draw.Count - 1;
                _hand.Add(draw[last]);
                draw.RemoveAt(last);
                count++;
            }
        }

        private List<CombatHandCard> DrawPile(CombatCardKind kind) =>
            kind == CombatCardKind.Clash ? _clashDraw : _normalDraw;

        private List<CombatHandCard> DiscardPile(CombatCardKind kind) =>
            kind == CombatCardKind.Clash ? _clashDiscard : _normalDiscard;

        private void Shuffle(List<CombatHandCard> cards)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                _randomState = unchecked(_randomState * 1664525u + 1013904223u);
                int other = (int)(_randomState % (uint)(i + 1));
                (cards[i], cards[other]) = (cards[other], cards[i]);
            }
        }
    }
}
