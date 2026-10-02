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
            Array.Empty<ICombatant>(),
            Array.Empty<ICombatant>(),
            null,
            null,
            null,
            null,
            false,
            false,
            false,
            false,
            false);

        public CombatExchangeDecisionKind? DecisionKind { get; }
        public int ExchangeVersion { get; }
        public ICombatant ActingActor { get; }
        public IReadOnlyList<ICombatant> SelectableActors { get; }
        public IReadOnlyList<ISkill> SelectableSkills { get; }
        public IReadOnlyList<ICombatant> SelectableTargets { get; }
        public IReadOnlyList<ICombatant> HandoffCandidates { get; }
        public ICombatant SelectedActor { get; }
        public ISkill SelectedSkill { get; }
        public ICombatant SelectedTarget { get; }
        public ICombatant SelectedHandoffTarget { get; }
        public bool CanConfirm { get; }
        public bool CanNoResponse { get; }
        public bool CanContinue { get; }
        public bool CanHandoff { get; }
        public bool CanEnd { get; }

        internal PlayerCombatDecisionViewState(
            CombatExchangeDecisionKind? decisionKind,
            int exchangeVersion,
            ICombatant actingActor,
            IReadOnlyList<ICombatant> selectableActors,
            IReadOnlyList<ISkill> selectableSkills,
            IReadOnlyList<ICombatant> selectableTargets,
            IReadOnlyList<ICombatant> handoffCandidates,
            ICombatant selectedActor,
            ISkill selectedSkill,
            ICombatant selectedTarget,
            ICombatant selectedHandoffTarget,
            bool canConfirm,
            bool canNoResponse,
            bool canContinue,
            bool canHandoff,
            bool canEnd)
        {
            DecisionKind = decisionKind;
            ExchangeVersion = exchangeVersion;
            ActingActor = actingActor;
            SelectableActors = selectableActors ?? Array.Empty<ICombatant>();
            SelectableSkills = selectableSkills ?? Array.Empty<ISkill>();
            SelectableTargets = selectableTargets ?? Array.Empty<ICombatant>();
            HandoffCandidates = handoffCandidates ?? Array.Empty<ICombatant>();
            SelectedActor = selectedActor;
            SelectedSkill = selectedSkill;
            SelectedTarget = selectedTarget;
            SelectedHandoffTarget = selectedHandoffTarget;
            CanConfirm = canConfirm;
            CanNoResponse = canNoResponse;
            CanContinue = canContinue;
            CanHandoff = canHandoff;
            CanEnd = canEnd;
        }
    }
}
