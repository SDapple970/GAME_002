using System;
using System.Collections.Generic;
using Game.Combat.Model;

namespace Game.Combat.UI
{
    /// <summary>
    /// Presentation-neutral snapshot for a FinalExchange player decision. The driver and
    /// session remain authoritative; this object only describes the currently selectable UI.
    /// </summary>
    public sealed class PlayerCombatDecisionViewState
    {
        public static PlayerCombatDecisionViewState Empty { get; } = new PlayerCombatDecisionViewState(
            null,
            -1,
            null,
            Array.Empty<ICombatant>(),
            Array.Empty<ISkill>(),
            Array.Empty<CombatItemOption>(),
            Array.Empty<ICombatant>(),
            Array.Empty<ICombatant>(),
            null,
            null,
            null,
            null,
            null,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            0,
            0,
            false,
            false);

        public CombatExchangeDecisionKind? DecisionKind { get; }
        public int ExchangeVersion { get; }
        public ICombatant ActingActor { get; }
        public IReadOnlyList<ICombatant> SelectableActors { get; }
        public IReadOnlyList<ISkill> SelectableSkills { get; }
        public IReadOnlyList<CombatItemOption> SelectableItems { get; }
        public IReadOnlyList<ICombatant> SelectableTargets { get; }
        public IReadOnlyList<ICombatant> HandoffCandidates { get; }
        public ICombatant SelectedActor { get; }
        public ISkill SelectedSkill { get; }
        public CombatItemOption SelectedItem { get; }
        public ICombatant SelectedTarget { get; }
        public ICombatant SelectedHandoffTarget { get; }
        public bool CanConfirm { get; }
        public bool CanNoResponse { get; }
        public bool CanContinue { get; }
        public bool CanHandoff { get; }
        public bool CanEnd { get; }
        public bool CanAllOut { get; }
        public bool CanAttack { get; }
        public bool CanUseItem { get; }
        public int CurrentMental { get; }
        public int MaxMental { get; }
        public bool IsPanicked { get; }
        public bool CanOvercome { get; }

        internal PlayerCombatDecisionViewState(
            CombatExchangeDecisionKind? decisionKind,
            int exchangeVersion,
            ICombatant actingActor,
            IReadOnlyList<ICombatant> selectableActors,
            IReadOnlyList<ISkill> selectableSkills,
            IReadOnlyList<CombatItemOption> selectableItems,
            IReadOnlyList<ICombatant> selectableTargets,
            IReadOnlyList<ICombatant> handoffCandidates,
            ICombatant selectedActor,
            ISkill selectedSkill,
            CombatItemOption selectedItem,
            ICombatant selectedTarget,
            ICombatant selectedHandoffTarget,
            bool canConfirm,
            bool canNoResponse,
            bool canContinue,
            bool canHandoff,
            bool canEnd,
            bool canAllOut,
            bool canAttack,
            bool canUseItem,
            int currentMental,
            int maxMental,
            bool isPanicked,
            bool canOvercome)
        {
            DecisionKind = decisionKind;
            ExchangeVersion = exchangeVersion;
            ActingActor = actingActor;
            SelectableActors = selectableActors ?? Array.Empty<ICombatant>();
            SelectableSkills = selectableSkills ?? Array.Empty<ISkill>();
            SelectableItems = selectableItems ?? Array.Empty<CombatItemOption>();
            SelectableTargets = selectableTargets ?? Array.Empty<ICombatant>();
            HandoffCandidates = handoffCandidates ?? Array.Empty<ICombatant>();
            SelectedActor = selectedActor;
            SelectedSkill = selectedSkill;
            SelectedItem = selectedItem;
            SelectedTarget = selectedTarget;
            SelectedHandoffTarget = selectedHandoffTarget;
            CanConfirm = canConfirm;
            CanNoResponse = canNoResponse;
            CanContinue = canContinue;
            CanHandoff = canHandoff;
            CanEnd = canEnd;
            CanAllOut = canAllOut;
            CanAttack = canAttack;
            CanUseItem = canUseItem;
            CurrentMental = currentMental;
            MaxMental = maxMental;
            IsPanicked = isPanicked;
            CanOvercome = canOvercome;
        }
    }
}
