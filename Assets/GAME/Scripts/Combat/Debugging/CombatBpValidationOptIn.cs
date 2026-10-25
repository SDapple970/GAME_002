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
        [SerializeField] private SkillFixture[] skillFixtures = Array.Empty<SkillFixture>();

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
            if (!enableValidation || session == null ||
                session.FlowMode != CombatFlowMode.StandoffClashChain ||
                session.Allies.Count != 1 || session.Enemies.Count != 1 ||
                maximumBp <= 0 || roundCyclesPerGain <= 0 || maximumInvestment <= 0)
            {
                LastStatus = "Enable validation on a new 1v1 FinalExchange with positive BP settings.";
                return false;
            }

            List<CombatBpEnhancementEntry> entries = new();
            CollectFixtures(session.Allies[0], entries);
            CollectFixtures(session.Enemies[0], entries);
            WasApplied = session.TryEnableBpValidation(
                new CombatBpValidationConfig(maximumBp, roundCyclesPerGain, maximumInvestment), entries);
            LastStatus = WasApplied ? "BP validation applied." :
                "CombatSession rejected BP validation; apply before its first attack declaration.";
            return WasApplied;
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
            if (!enableValidation) return;
            if (!TryApplyToSession(entryPoint != null ? entryPoint.ActiveSession : null))
                Debug.LogError($"[CombatBpValidationOptIn] {LastStatus}", this);
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
