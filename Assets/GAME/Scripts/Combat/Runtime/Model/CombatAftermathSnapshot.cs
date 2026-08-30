using System;
using System.Collections.Generic;

namespace Game.Combat.Model
{
    public sealed class CombatAftermathSnapshot
    {
        public CombatClashOutcome ClashOutcome { get; }
        public ICombatant AttackInitiator { get; }
        public ICombatant ExchangeOpponent { get; }
        public ICombatant ClashWinner { get; }
        public bool HadOutcomeAction { get; }
        public bool HasExecutionResult { get; }
        public int LivingAlliesCount { get; }
        public int DefeatedAlliesCount { get; }
        public int LivingEnemiesCount { get; }
        public int DefeatedEnemiesCount { get; }
        public int StunnedLivingAlliesCount { get; }
        public int StunnedLivingEnemiesCount { get; }
        public IReadOnlyList<ICombatant> DefeatedAllies { get; }
        public IReadOnlyList<ICombatant> DefeatedEnemies { get; }
        public IReadOnlyList<ICombatant> ExecutionZeroHpTargets { get; }
        public bool WasPostureApplied { get; }
        public bool PostureReachedMaximum { get; }
        public bool RequiresTiePolicy { get; }

        internal CombatAftermathSnapshot(CombatSession session, CombatExchangeState exchange)
        {
            CombatClashResult clash = exchange.CurrentClashResult;
            ClashOutcome = clash.Outcome;
            AttackInitiator = clash.AttackDeclaration.Attacker;
            ExchangeOpponent = clash.AttackDeclaration.Target;
            ClashWinner = clash.Winner;
            HadOutcomeAction = exchange.CurrentOutcomeAction != null;
            HasExecutionResult = exchange.CurrentExecutionResult != null;

            DefeatedAllies = ObserveSide(
                session.Allies,
                out int livingAllies,
                out int stunnedLivingAllies);
            LivingAlliesCount = livingAllies;
            DefeatedAlliesCount = DefeatedAllies.Count;
            StunnedLivingAlliesCount = stunnedLivingAllies;

            DefeatedEnemies = ObserveSide(
                session.Enemies,
                out int livingEnemies,
                out int stunnedLivingEnemies);
            LivingEnemiesCount = livingEnemies;
            DefeatedEnemiesCount = DefeatedEnemies.Count;
            StunnedLivingEnemiesCount = stunnedLivingEnemies;

            ExecutionZeroHpTargets = ObserveExecutionZeroHpTargets(exchange.CurrentExecutionResult);
            WasPostureApplied = exchange.PostureResolutionState == CombatPostureResolutionState.Applied;
            PostureReachedMaximum = exchange.CurrentPostureResult?.ReachedMaximum == true;
            RequiresTiePolicy = clash.Outcome == CombatClashOutcome.Tie ||
                                exchange.PostureResolutionState == CombatPostureResolutionState.PolicyRequired ||
                                exchange.StunResolutionState == CombatStunResolutionState.PolicyRequired;
        }

        private static IReadOnlyList<ICombatant> ObserveSide(
            IReadOnlyList<ICombatant> roster,
            out int livingCount,
            out int stunnedLivingCount)
        {
            livingCount = 0;
            stunnedLivingCount = 0;
            List<ICombatant> defeated = new List<ICombatant>();
            for (int i = 0; i < roster.Count; i++)
            {
                ICombatant combatant = roster[i];
                if (combatant == null)
                    continue;

                if (combatant.HP <= 0)
                {
                    defeated.Add(combatant);
                    continue;
                }

                livingCount++;
                if (combatant.IsStunned)
                    stunnedLivingCount++;
            }

            return Array.AsReadOnly(defeated.ToArray());
        }

        private static IReadOnlyList<ICombatant> ObserveExecutionZeroHpTargets(
            CombatSkillExecutionResult executionResult)
        {
            List<ICombatant> targets = new List<ICombatant>();
            IReadOnlyList<CombatSkillTargetResult> targetResults = executionResult?.TargetResults;
            if (targetResults != null)
            {
                for (int i = 0; i < targetResults.Count; i++)
                {
                    CombatSkillTargetResult targetResult = targetResults[i];
                    if (targetResult?.Target != null && targetResult.HpAfter <= 0)
                        targets.Add(targetResult.Target);
                }
            }

            return Array.AsReadOnly(targets.ToArray());
        }
    }
}
