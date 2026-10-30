using System;
using System.Collections.Generic;
using Game.Combat.Actions;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Model;
using UnityEngine;

namespace Game.Combat.Debugging
{
    /// <summary>Validation fixture attached to the existing FinalExchange startup event.</summary>
    [DisallowMultipleComponent]
    public sealed class CombatBpValidationOptIn : MonoBehaviour
    {
        [Serializable]
        private sealed class SkillFixture
        {
            [SerializeField] internal SkillDefinitionSO skill;
            [SerializeField] internal CombatBpEnhancementKind kind;
            [SerializeField] [Min(1)] internal int perPoint = 1;
        }

        [SerializeField] private CombatEntryPoint entryPoint;
        [SerializeField] private CombatFlowOrchestrator orchestrator;
        [SerializeField] private bool enableValidation;
        [SerializeField] [Min(1)] private int maximumBp = 3;
        [SerializeField] [Min(1)] private int roundCyclesPerGain = 2;
        [SerializeField] [Min(1)] private int maximumInvestment = 3;
        [Tooltip("Validation-only player BP before the first declaration. This does not charge Ultimate.")]
        [SerializeField] [Min(0)] private int initialPlayerBp;
        [SerializeField] private SkillFixture[] skillFixtures = Array.Empty<SkillFixture>();
        [SerializeField] private SkillDefinitionSO ultimateSkill;

        private CombatFlowOrchestrator _subscribedOrchestrator;
        public bool WasApplied { get; private set; }
        public string LastStatus { get; private set; }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            if (_subscribedOrchestrator != null)
                _subscribedOrchestrator.ClashSeriesValidationEnabled -= ApplyWhenClashEnabled;
            _subscribedOrchestrator = null;
        }

        [ContextMenu("Apply BP Validation To Active Session")]
        private void ApplyToActiveSession()
        {
            ResolveReferences();
            if (!TryApplyToSession(entryPoint != null ? entryPoint.ActiveSession : null))
                Debug.LogError($"[CombatBpValidationOptIn] {LastStatus}", this);
        }

        public bool TryApplyToSession(CombatSession session)
        {
            WasApplied = false;
            if (session?.HasProductionHandBpInitialization == true)
            {
                LastStatus = "Production owns this session's Hand/BP initialization.";
                return false;
            }
            if (!enableValidation || session == null ||
                session.FlowMode != CombatFlowMode.StandoffClashChain ||
                session.Allies.Count < 1 || session.Allies.Count > 3 || session.Enemies.Count < 1 ||
                maximumBp <= 0 || roundCyclesPerGain <= 0 || maximumInvestment <= 0 ||
                initialPlayerBp < 0 || initialPlayerBp > maximumBp)
            {
                LastStatus = "Enable validation on a new 1-3 ally FinalExchange with positive BP settings.";
                return false;
            }

            List<CombatBpEnhancementEntry> entries = new();
            foreach (ICombatant ally in session.Allies) CollectFixtures(ally, entries);
            foreach (ICombatant enemy in session.Enemies) CollectFixtures(enemy, entries);
            WasApplied = session.TryEnableBpValidation(
                new CombatBpValidationConfig(maximumBp, roundCyclesPerGain, maximumInvestment), entries);
            if (WasApplied && initialPlayerBp > 0 &&
                !session.TrySetInitialBpForValidation(session.Allies[0], initialPlayerBp))
            {
                LastStatus = "BP validation started, but the explicit initial player BP was rejected.";
                WasApplied = false;
                return false;
            }
            if (WasApplied && ultimateSkill != null)
            {
                foreach (ICombatant ally in session.Allies) AssignUltimateFixture(session, ally);
                foreach (ICombatant enemy in session.Enemies) AssignUltimateFixture(session, enemy);
            }
            LastStatus = WasApplied ? "BP validation applied." :
                "CombatSession rejected BP validation; apply before its first attack declaration.";
            return WasApplied;
        }

        private void AssignUltimateFixture(CombatSession session, ICombatant actor)
        {
            if (actor?.Skills == null) return;
            foreach (ISkill skill in actor.Skills)
            {
                if (skill is SoSkill soSkill && ReferenceEquals(soSkill.Definition, ultimateSkill))
                {
                    session.TryAssignUltimateSkill(actor, skill);
                    return;
                }
            }
        }

        private void CollectFixtures(ICombatant actor, List<CombatBpEnhancementEntry> entries)
        {
            if (actor?.Skills == null || skillFixtures == null) return;
            foreach (SkillFixture fixture in skillFixtures)
            {
                if (fixture?.skill == null || fixture.kind == CombatBpEnhancementKind.None ||
                    fixture.perPoint <= 0) continue;
                foreach (ISkill skill in actor.Skills)
                    if (skill is SoSkill soSkill && ReferenceEquals(soSkill.Definition, fixture.skill))
                        entries.Add(new CombatBpEnhancementEntry(skill, fixture.kind, fixture.perPoint));
            }
        }

        private void ApplyWhenClashEnabled()
        {
            if (!enableValidation || IsShadowedByConfiguredFixture()) return;
            CombatSession session = entryPoint != null ? entryPoint.ActiveSession : null;
            if (session?.HasProductionHandBpInitialization == true) return;
            if (session?.BpRuntime != null)
            {
                if (ultimateSkill != null)
                {
                    foreach (ICombatant ally in session.Allies) AssignUltimateFixture(session, ally);
                    foreach (ICombatant enemy in session.Enemies) AssignUltimateFixture(session, enemy);
                }
                LastStatus = "BP validation already active for this session.";
                return;
            }
            if (!TryApplyToSession(session))
                Debug.LogError($"[CombatBpValidationOptIn] {LastStatus}", this);
        }

        private bool IsShadowedByConfiguredFixture()
        {
            if (initialPlayerBp > 0 || ultimateSkill != null || skillFixtures?.Length > 0) return false;
            CombatBpValidationOptIn[] components = GetComponents<CombatBpValidationOptIn>();
            for (int i = 0; i < components.Length; i++)
            {
                CombatBpValidationOptIn other = components[i];
                if (!ReferenceEquals(other, this) && other.isActiveAndEnabled &&
                    other.enableValidation &&
                    (other.initialPlayerBp > 0 || other.ultimateSkill != null ||
                     other.skillFixtures?.Length > 0))
                    return true;
            }
            return false;
        }

        private void ResolveReferences()
        {
            if (entryPoint == null) entryPoint = GetComponent<CombatEntryPoint>();
            if (orchestrator == null) orchestrator = GetComponent<CombatFlowOrchestrator>();
        }

        private void Subscribe()
        {
            if (ReferenceEquals(_subscribedOrchestrator, orchestrator)) return;
            if (_subscribedOrchestrator != null)
                _subscribedOrchestrator.ClashSeriesValidationEnabled -= ApplyWhenClashEnabled;
            _subscribedOrchestrator = orchestrator;
            if (_subscribedOrchestrator != null)
                _subscribedOrchestrator.ClashSeriesValidationEnabled += ApplyWhenClashEnabled;
        }
    }
}
