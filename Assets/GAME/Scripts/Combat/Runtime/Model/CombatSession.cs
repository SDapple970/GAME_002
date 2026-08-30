using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Game.Combat.Core;
using Game.Combat.Environment;

namespace Game.Combat.Model
{
    public sealed class CombatSession
    {
        public readonly List<ICombatant> Allies = new();
        public readonly List<ICombatant> Enemies = new();

        private readonly Dictionary<ICombatant, CombatantCombatState> _combatStates =
            new Dictionary<ICombatant, CombatantCombatState>(CombatantReferenceComparer.Instance);

        public InspirationPool Inspiration { get; }
        public CombatEnvironment Env { get; }
        public int TurnIndex { get; private set; }
        public StartReason StartReason { get; }
        public Side InitiativeSide { get; }
        public CombatTurn CurrentTurn { get; private set; } = new();
        public KnowledgeBook Knowledge { get; } = new KnowledgeBook();
        public string CompletionId { get; }
        public CombatExchangeState ExchangeState { get; }
        public int CombatStateCount => _combatStates.Count;
        public CombatFlowMode FlowMode { get; }
        public CombatRuntimeConfig RuntimeConfig { get; }
        public StandoffRuntimeState StandoffState { get; }

        public CombatSession(
            StartReason reason,
            Side initiativeSide,
            InspirationPool inspiration,
            CombatEnvironment env)
            : this(
                reason,
                initiativeSide,
                inspiration,
                env,
                CombatFlowMode.LegacyPlanning,
                CombatRuntimeConfig.Compatibility)
        {
        }

        public CombatSession(
            StartReason reason,
            Side initiativeSide,
            InspirationPool inspiration,
            CombatEnvironment env,
            CombatFlowMode flowMode)
            : this(reason, initiativeSide, inspiration, env, flowMode, CombatRuntimeConfig.Compatibility)
        {
        }

        public CombatSession(
            StartReason reason,
            Side initiativeSide,
            InspirationPool inspiration,
            CombatEnvironment env,
            CombatFlowMode flowMode,
            CombatRuntimeConfig runtimeConfig)
        {
            CompletionId = Guid.NewGuid().ToString("N");
            StartReason = reason;
            InitiativeSide = initiativeSide;
            Inspiration = inspiration;
            Env = env;
            FlowMode = flowMode == CombatFlowMode.StandoffClashChain
                ? flowMode
                : CombatFlowMode.LegacyPlanning;
            RuntimeConfig = runtimeConfig;
            ExchangeState = new CombatExchangeState(initiativeSide);
            StandoffState = new StandoffRuntimeState(runtimeConfig.PressureMax);
        }

        public void InitializeCombatStates(CombatRuntimeConfig config)
        {
            RegisterCombatStates(Allies, config);
            RegisterCombatStates(Enemies, config);
        }

        public CombatantCombatState GetCombatState(ICombatant combatant)
        {
            if (!TryGetCombatState(combatant, out CombatantCombatState state))
                throw new KeyNotFoundException("The combatant is not registered in this combat session.");

            return state;
        }

        public bool TryGetCombatState(ICombatant combatant, out CombatantCombatState state)
        {
            if (combatant == null)
            {
                state = null;
                return false;
            }

            return _combatStates.TryGetValue(combatant, out state);
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

        private void RegisterCombatStates(IReadOnlyList<ICombatant> combatants, CombatRuntimeConfig config)
        {
            for (int i = 0; i < combatants.Count; i++)
            {
                ICombatant combatant = combatants[i];
                if (combatant != null && !_combatStates.ContainsKey(combatant))
                    _combatStates.Add(combatant, new CombatantCombatState(combatant, config));
            }
        }

        private sealed class CombatantReferenceComparer : IEqualityComparer<ICombatant>
        {
            public static CombatantReferenceComparer Instance { get; } = new CombatantReferenceComparer();

            public bool Equals(ICombatant x, ICombatant y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(ICombatant obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
