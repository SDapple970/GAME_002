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
        private readonly Dictionary<ICombatant, EnemySourceSnapshot> _enemySources =
            new(CombatantReferenceComparer.Instance);
        private readonly Dictionary<ICombatant, EnemyCombatBehaviorSnapshot> _enemyBehaviors =
            new(CombatantReferenceComparer.Instance);
        private readonly Dictionary<ICombatant, string> _allyCharacterIds =
            new(CombatantReferenceComparer.Instance);
        private readonly HashSet<string> _registeredAllyCharacterIds = new(StringComparer.Ordinal);
        private readonly Dictionary<ICombatant, CombatHandRuntime> _hands =
            new(CombatantReferenceComparer.Instance);
        private readonly int[] _lastCompletedAttackCycle = new int[2];
        private int _completedCycleSides;
        private long _nextCardRuntimeId = 1;

        public string SkillAcquisitionRecipientCharacterId { get; internal set; }
        public string ProgressionTargetCharacterId { get; internal set; }
        public bool IsPartySkillAcquisitionEligible { get; internal set; }
        internal bool RequiresExplicitExperienceRecipients { get; set; }
        internal IReadOnlyDictionary<ICombatant, string> AllyCharacterIds => _allyCharacterIds;

        internal void RegisterEnemySource(ICombatant enemy, EnemySourceSnapshot source)
        {
            if (enemy != null && enemy.Side == Side.Enemies && source?.SourceKey != null)
                _enemySources.TryAdd(enemy, source);
        }

        internal bool RegisterAllyCharacter(ICombatant ally, string characterId)
        {
            if (ally == null || ally.Side != Side.Allies || string.IsNullOrWhiteSpace(characterId))
                return false;

            string normalizedId = characterId.Trim();
            if (!_registeredAllyCharacterIds.Add(normalizedId))
                return false;

            _allyCharacterIds[ally] = normalizedId;
            return true;
        }

        public bool TryGetAllyCharacterId(ICombatant ally, out string characterId)
        {
            if (ally != null && ally.Side == Side.Allies && _allyCharacterIds.TryGetValue(ally, out characterId))
                return true;

            characterId = null;
            return false;
        }

        public bool TryGetEnemySource(ICombatant enemy, out EnemySourceSnapshot source)
        {
            if (enemy != null && _enemySources.TryGetValue(enemy, out source))
                return true;
            source = null;
            return false;
        }

        internal void RegisterEnemyBehavior(ICombatant enemy, EnemyCombatBehaviorSnapshot behavior)
        {
            if (enemy != null && enemy.Side == Side.Enemies && behavior?.HasBehavior == true)
                _enemyBehaviors.TryAdd(enemy, behavior);
        }

        public bool TryGetEnemyBehavior(ICombatant enemy, out EnemyCombatBehaviorSnapshot behavior)
        {
            if (enemy != null && _enemyBehaviors.TryGetValue(enemy, out behavior))
                return true;

            behavior = null;
            return false;
        }

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
        public bool OpeningEffectApplied { get; private set; }
        public bool HasHandValidation => _hands.Count > 0;
        public CombatBpRuntime BpRuntime { get; private set; }

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

        /// <summary>Explicit validation opt-in. The caller supplies card classification, capacities, and seed.</summary>
        public bool TryEnableHandValidation(
            ICombatant actor,
            IEnumerable<CombatHandEntry> entries,
            int normalCapacity,
            int clashCapacity,
            int seed)
        {
            if (FlowMode != CombatFlowMode.StandoffClashChain ||
                ExchangeState.DeclarationId != 0 || actor == null ||
                !TryGetCombatState(actor, out _) || _hands.ContainsKey(actor) ||
                entries == null || normalCapacity < 0 || clashCapacity < 0)
                return false;

            List<CombatHandEntry> snapshot = new();
            foreach (CombatHandEntry entry in entries)
            {
                if (entry.Skill == null || !Enum.IsDefined(typeof(CombatCardKind), entry.Kind) ||
                    !OwnsSkillReference(actor, entry.Skill) ||
                    _nextCardRuntimeId > long.MaxValue - snapshot.Count - 1)
                    return false;
                snapshot.Add(entry);
            }

            CombatHandRuntime hand = new CombatHandRuntime(
                snapshot, normalCapacity, clashCapacity, seed, _nextCardRuntimeId);
            _nextCardRuntimeId += snapshot.Count;
            _hands.Add(actor, hand);
            return true;
        }

        public bool TryGetHand(ICombatant actor, out CombatHandRuntime hand)
        {
            hand = null;
            return actor != null && _hands.TryGetValue(actor, out hand);
        }

        public bool TryEnableBpValidation(CombatBpValidationConfig config)
        {
            return TryEnableBpValidation(config, null);
        }

        public bool TryEnableBpValidation(CombatBpValidationConfig config,
            IReadOnlyList<CombatBpEnhancementEntry> enhancements)
        {
            if (BpRuntime != null || FlowMode != CombatFlowMode.StandoffClashChain ||
                ExchangeState.DeclarationId != 0 || Allies.Count != 1 || Enemies.Count != 1 ||
                config.MaximumBp <= 0 || config.RoundCyclesPerGain <= 0 ||
                config.MaximumInvestment <= 0)
                return false;

            BpRuntime = new CombatBpRuntime(config, this, enhancements);
            return true;
        }

        public bool TryRecordCompletedBpAttackCycle(Side side, int cycleId, bool hadSuccessfulExchange)
        {
            return BpRuntime != null &&
                CombatEndEvaluator.Evaluate(this) == CombatEndReason.None &&
                BpRuntime.RecordAttackCycle(side, cycleId, hadSuccessfulExchange, this);
        }

        /// <summary>One refill only after both sides complete distinct, successful attack cycles.</summary>
        public bool TryRecordCompletedAttackCycle(Side side, int cycleId, bool hadSuccessfulExchange)
        {
            if (!HasHandValidation || !Enum.IsDefined(typeof(Side), side) || cycleId <= 0 ||
                !hadSuccessfulExchange || CombatEndEvaluator.Evaluate(this) != CombatEndReason.None)
                return false;

            int index = (int)side;
            if (_lastCompletedAttackCycle[index] >= cycleId)
                return false;

            _lastCompletedAttackCycle[index] = cycleId;
            _completedCycleSides |= 1 << index;
            if (_completedCycleSides != 3)
                return true;

            _completedCycleSides = 0;
            foreach (CombatHandRuntime hand in _hands.Values)
                hand.Refill();
            return true;
        }

        private static bool OwnsSkillReference(ICombatant actor, ISkill skill)
        {
            if (actor.Skills == null) return false;
            for (int i = 0; i < actor.Skills.Count; i++)
                if (ReferenceEquals(actor.Skills[i], skill)) return true;
            return false;
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

        internal bool TryMarkOpeningEffectApplied()
        {
            if (OpeningEffectApplied)
                return false;

            OpeningEffectApplied = true;
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
                {
                    CombatantCombatState state = new CombatantCombatState(combatant, config);
                    _combatStates.Add(combatant, state);
                    if (combatant is ICombatantRuntimeStateBinding binding)
                        binding.BindCombatState(state);
                }
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
