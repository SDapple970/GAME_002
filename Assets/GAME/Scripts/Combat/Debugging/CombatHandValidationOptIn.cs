using System;
using System.Collections.Generic;
using Game.Combat.Actions;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Model;
using UnityEngine;

namespace Game.Combat.Debugging
{
    /// <summary>
    /// Explicit validation-only bridge from a constructed 1v1 FinalExchange session to its
    /// per-session hand. It never edits persistent loadouts or supplies a Production default.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatHandValidationOptIn : MonoBehaviour
    {
        public enum CardSource
        {
            SessionSkillSnapshot = 0,
            InspectorFixture = 1
        }

        [Serializable]
        public sealed class FixtureCard
        {
            [SerializeField] private SkillDefinitionSO skill;
            [SerializeField] private CombatCardKind kind;
            [SerializeField] [Min(1)] private int copies = 1;

            public SkillDefinitionSO Skill => skill;
            public CombatCardKind Kind => kind;
            public int Copies => copies;
        }

        [SerializeField] private CombatEntryPoint entryPoint;
        [SerializeField] private CombatFlowOrchestrator orchestrator;
        [SerializeField] private bool enableValidation;

        [Header("Player hand fixture")]
        [SerializeField] private CardSource playerCardSource;
        [SerializeField] private SkillDefinitionSO[] clashSkillDefinitions = Array.Empty<SkillDefinitionSO>();
        [SerializeField] private FixtureCard[] fixtureCards = Array.Empty<FixtureCard>();
        [SerializeField] [Min(0)] private int normalHandCapacity = 2;
        [SerializeField] [Min(0)] private int clashHandCapacity = 3;
        [SerializeField] private int seed = 2303;

        [Header("Enemy hand fixture (D15 validation opt-in)")]
        [SerializeField] private bool enableEnemyHand;
        [SerializeField] private CardSource enemyCardSource;
        [SerializeField] private SkillDefinitionSO[] enemyClashSkillDefinitions = Array.Empty<SkillDefinitionSO>();
        [SerializeField] private FixtureCard[] enemyFixtureCards = Array.Empty<FixtureCard>();
        [SerializeField] private int enemySeed = 2304;

        public bool WasApplied { get; private set; }
        public string LastStatus { get; private set; }

        private CombatFlowOrchestrator _subscribedOrchestrator;

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeToClashValidation();
        }

        private void OnDisable()
        {
            if (_subscribedOrchestrator != null)
                _subscribedOrchestrator.ClashSeriesValidationEnabled -= HandleClashSeriesValidationEnabled;
            _subscribedOrchestrator = null;
        }

        [ContextMenu("Apply Hand Validation To Active Session")]
        private void ApplyToActiveSession()
        {
            ResolveReferences();
            CombatSession session = entryPoint != null ? entryPoint.ActiveSession : null;
            if (!enableValidation || orchestrator == null || !orchestrator.IsClashSeriesValidationEnabled ||
                !TryApplyToSession(session))
            {
                Debug.LogError($"[CombatHandValidationOptIn] {LastStatus ?? "Hand validation was rejected."}", this);
            }
        }

        /// <summary>
        /// Public for deterministic validation tests and manual tooling. Normal combat startup reaches this
        /// method through the existing clash-series opt-in event before the first declaration.
        /// </summary>
        public bool TryApplyToSession(CombatSession session)
        {
            WasApplied = false;
            LastStatus = null;
            if (session == null || session.FlowMode != CombatFlowMode.StandoffClashChain ||
                session.Allies.Count != 1 || session.Enemies.Count != 1 ||
                session.ExchangeState.DeclarationId != 0)
            {
                LastStatus = "Hand validation requires a new 1v1 StandoffClashChain session before the first declaration.";
                return false;
            }

            if (normalHandCapacity < 0 || clashHandCapacity < 0)
            {
                LastStatus = "Hand capacities must be zero or greater.";
                return false;
            }

            List<CombatHandEntry> playerEntries = BuildEntries(
                session.Allies[0], playerCardSource, clashSkillDefinitions, fixtureCards);
            if (playerEntries.Count == 0)
            {
                LastStatus = "Player has no matching session skills. Check the selected fixture.";
                return false;
            }

            List<CombatHandEntry> enemyEntries = null;
            if (enableEnemyHand)
            {
                enemyEntries = BuildEntries(session.Enemies[0], enemyCardSource,
                    enemyClashSkillDefinitions, enemyFixtureCards);
                if (enemyEntries.Count == 0)
                {
                    LastStatus = "Enemy has no matching session skills. Check the selected fixture.";
                    return false;
                }
            }

            if (!session.TryEnableHandValidation(session.Allies[0], playerEntries,
                    normalHandCapacity, clashHandCapacity, seed))
            {
                LastStatus = "Player hand validation was rejected by CombatSession.";
                return false;
            }

            if (enableEnemyHand && !session.TryEnableHandValidation(session.Enemies[0], enemyEntries,
                    normalHandCapacity, clashHandCapacity, enemySeed))
            {
                // Both requests were completely validated before the first commit. A failure here means
                // the session changed unexpectedly during this synchronous validation callback.
                LastStatus = "Enemy hand validation was rejected after player hand commit.";
                Debug.LogError($"[CombatHandValidationOptIn] {LastStatus}", this);
                return false;
            }

            WasApplied = true;
            LastStatus = enableEnemyHand
                ? "Player and enemy hand validation applied."
                : "Player hand validation applied.";
            Debug.Log($"[CombatHandValidationOptIn] {LastStatus}", this);
            return true;
        }

        private void HandleClashSeriesValidationEnabled()
        {
            if (!enableValidation)
                return;

            CombatSession session = entryPoint != null ? entryPoint.ActiveSession : null;
            if (!TryApplyToSession(session))
                Debug.LogError($"[CombatHandValidationOptIn] {LastStatus}", this);
        }

        private static List<CombatHandEntry> BuildEntries(
            ICombatant actor,
            CardSource source,
            IReadOnlyList<SkillDefinitionSO> clashDefinitions,
            IReadOnlyList<FixtureCard> fixtures)
        {
            List<CombatHandEntry> entries = new();
            if (actor?.Skills == null)
                return entries;

            if (source == CardSource.SessionSkillSnapshot)
            {
                for (int i = 0; i < actor.Skills.Count; i++)
                {
                    ISkill skill = actor.Skills[i];
                    if (skill == null) continue;
                    entries.Add(new CombatHandEntry(skill,
                        IsClashDefinition(skill, clashDefinitions) ? CombatCardKind.Clash : CombatCardKind.Normal));
                }
                return entries;
            }

            if (fixtures == null)
                return entries;

            for (int i = 0; i < fixtures.Count; i++)
            {
                FixtureCard fixture = fixtures[i];
                if (fixture?.Skill == null || fixture.Copies < 1 ||
                    !TryFindSessionSkill(actor.Skills, fixture.Skill, out ISkill skill))
                    continue;

                for (int copy = 0; copy < fixture.Copies; copy++)
                    entries.Add(new CombatHandEntry(skill, fixture.Kind));
            }

            return entries;
        }

        private static bool IsClashDefinition(ISkill skill, IReadOnlyList<SkillDefinitionSO> clashDefinitions)
        {
            if (skill is not SoSkill soSkill || clashDefinitions == null)
                return false;

            for (int i = 0; i < clashDefinitions.Count; i++)
                if (ReferenceEquals(soSkill.Definition, clashDefinitions[i])) return true;
            return false;
        }

        private static bool TryFindSessionSkill(
            IReadOnlyList<ISkill> sessionSkills,
            SkillDefinitionSO definition,
            out ISkill skill)
        {
            if (sessionSkills != null)
            {
                for (int i = 0; i < sessionSkills.Count; i++)
                {
                    if (sessionSkills[i] is SoSkill soSkill && ReferenceEquals(soSkill.Definition, definition))
                    {
                        skill = sessionSkills[i];
                        return true;
                    }
                }
            }

            skill = null;
            return false;
        }

        private void ResolveReferences()
        {
            if (entryPoint == null)
                entryPoint = GetComponent<CombatEntryPoint>();
            if (orchestrator == null)
                orchestrator = GetComponent<CombatFlowOrchestrator>();
        }

        private void SubscribeToClashValidation()
        {
            if (ReferenceEquals(_subscribedOrchestrator, orchestrator))
                return;

            if (_subscribedOrchestrator != null)
                _subscribedOrchestrator.ClashSeriesValidationEnabled -= HandleClashSeriesValidationEnabled;
            _subscribedOrchestrator = orchestrator;
            if (_subscribedOrchestrator != null)
                _subscribedOrchestrator.ClashSeriesValidationEnabled += HandleClashSeriesValidationEnabled;
        }
    }
}
