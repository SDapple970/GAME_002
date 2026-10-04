#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Environment;
using Game.Combat.Model;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.Combat
{
    public sealed class CombatMentalRuntimeTests
    {
        [Test]
        public void CombatantState_InitializesClampsAndAppliesPanicOnlyOnTransition()
        {
            TestCombatant combatant = new TestCombatant(1, Side.Allies);
            CombatantCombatState state = new CombatantCombatState(combatant, Config(initialMental: 120));

            Assert.That(state.MaxMental, Is.EqualTo(100));
            Assert.That(state.CurrentMental, Is.EqualTo(100));
            Assert.That(state.IsPanicked, Is.False);

            CombatMentalMutationResult capped = state.ApplyMentalDelta(10);
            Assert.That(capped.AppliedDelta, Is.Zero);
            Assert.That(capped.PanicApplied, Is.False);

            CombatMentalMutationResult panic = state.ApplyMentalDelta(-100);
            Assert.That(panic.MentalAfter, Is.Zero);
            Assert.That(panic.PanicApplied, Is.True);
            Assert.That(state.MentalState, Is.EqualTo(CombatMentalState.Panicked));

            CombatMentalMutationResult repeated = state.ApplyMentalDelta(-10);
            Assert.That(repeated.AppliedDelta, Is.Zero);
            Assert.That(repeated.PanicApplied, Is.False);
        }

        [Test]
        public void FinalRuntimeConfigAsset_AuthorsApprovedMentalDefaults()
        {
            FinalCombatRuntimeConfigSO asset = AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(
                "Assets/GAME/Data/Combat/Runtime/FinalCombatRuntimeConfig_V1.asset");

            Assert.That(asset, Is.Not.Null);
            CombatRuntimeConfig config = asset.CreateRuntimeConfig();
            Assert.That(config.MaxMental, Is.EqualTo(100));
            Assert.That(config.InitialMental, Is.EqualTo(100));
            Assert.That(config.ClashMentalLoss, Is.EqualTo(10));
            Assert.That(config.DamageMentalLoss, Is.EqualTo(5));
            Assert.That(config.StunMentalLoss, Is.EqualTo(20));
            Assert.That(config.PanicThreshold, Is.Zero);
            Assert.That(config.OvercomeRecoveryMental, Is.EqualTo(35));
        }

        [Test]
        public void Session_CombatantMentalStateIsLocalAndResetsForANewSession()
        {
            TestCombatant ally = new TestCombatant(1, Side.Allies);
            TestCombatant enemy = new TestCombatant(2, Side.Enemies);
            CombatSession first = CreateSession(ally, enemy);
            first.InitializeCombatStates(first.RuntimeConfig);
            first.GetCombatState(ally).ApplyMentalDelta(-40);

            CombatSession second = CreateSession(ally, enemy);
            second.InitializeCombatStates(second.RuntimeConfig);

            Assert.That(first.GetCombatState(ally).CurrentMental, Is.EqualTo(60));
            Assert.That(first.GetCombatState(enemy).CurrentMental, Is.EqualTo(100));
            Assert.That(second.GetCombatState(ally).CurrentMental, Is.EqualTo(100));
            Assert.That(second.GetCombatState(enemy).CurrentMental, Is.EqualTo(100));
        }

        [Test]
        public void Rule_AggregatesClashDamageAndNewStunPerTarget()
        {
            TestCombatant attacker = new TestCombatant(1, Side.Allies);
            TestCombatant target = new TestCombatant(2, Side.Enemies);
            CombatAttackDeclaration attack = new CombatAttackDeclaration(attacker, target, null);
            CombatClashResult clash = new CombatClashResult(
                CombatClashOutcome.AttackerWin,
                attack,
                new CombatResponseDeclaration(target, null),
                attacker);
            CombatSkillExecutionResult execution = new CombatSkillExecutionResult(
                attacker,
                null,
                CombatClashOutcome.AttackerWin,
                new[] { new CombatSkillTargetResult(target, 10, 8) },
                null);
            CombatStunResult stun = new CombatStunResult(target, false, true);

            IReadOnlyList<FinalCombatMentalRule.CombatMentalDelta> results =
                new FinalCombatMentalRule(Config()).Calculate(clash, execution, stun);

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Target, Is.SameAs(target));
            Assert.That(results[0].RequestedDelta, Is.EqualTo(-35));
        }

        [Test]
        public void Rule_ExcludesTieUnopposedAndZeroDamageFromClashLoss()
        {
            TestCombatant attacker = new TestCombatant(1, Side.Allies);
            TestCombatant target = new TestCombatant(2, Side.Enemies);
            CombatAttackDeclaration attack = new CombatAttackDeclaration(attacker, target, null);
            CombatClashResult unopposed = new CombatClashResult(
                CombatClashOutcome.Unopposed,
                attack,
                null,
                attacker);
            CombatSkillExecutionResult zeroDamage = new CombatSkillExecutionResult(
                attacker,
                null,
                CombatClashOutcome.Unopposed,
                new[] { new CombatSkillTargetResult(target, 10, 10) },
                null);

            IReadOnlyList<FinalCombatMentalRule.CombatMentalDelta> results =
                new FinalCombatMentalRule(Config()).Calculate(unopposed, zeroDamage, null);

            Assert.That(results, Is.Empty);
        }

        [Test]
        public void Rule_AlreadyStunnedTargetDoesNotReceiveStunLossAgain()
        {
            TestCombatant attacker = new TestCombatant(1, Side.Allies);
            TestCombatant target = new TestCombatant(2, Side.Enemies);
            CombatAttackDeclaration attack = new CombatAttackDeclaration(attacker, target, null);
            CombatClashResult clash = new CombatClashResult(
                CombatClashOutcome.AttackerWin,
                attack,
                new CombatResponseDeclaration(target, null),
                attacker);
            CombatSkillExecutionResult execution = new CombatSkillExecutionResult(
                attacker,
                null,
                CombatClashOutcome.AttackerWin,
                new[] { new CombatSkillTargetResult(target, 10, 9) },
                null);

            IReadOnlyList<FinalCombatMentalRule.CombatMentalDelta> results =
                new FinalCombatMentalRule(Config()).Calculate(
                    clash,
                    execution,
                    new CombatStunResult(target, true, true));

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].RequestedDelta, Is.EqualTo(-15));
        }

        private static CombatSession CreateSession(TestCombatant ally, TestCombatant enemy)
        {
            CombatSession session = new CombatSession(
                StartReason.PlayerFirstHit,
                Side.Allies,
                new InspirationPool(10),
                new CombatEnvironment(),
                CombatFlowMode.StandoffClashChain,
                Config());
            session.Allies.Add(ally);
            session.Enemies.Add(enemy);
            return session;
        }

        private static CombatRuntimeConfig Config(int initialMental = 100)
        {
            return new CombatRuntimeConfig(10, 5, 3, 0, 0f, 0f, 0f, 100, initialMental, 10, 5, 20, 0);
        }

        private sealed class TestCombatant : ICombatant
        {
            public CombatantId Id { get; }
            public Side Side { get; }
            public int HP => 10;
            public int MaxHP => 10;
            public KeywordMask Weakness => KeywordMask.None;
            public KeywordMask Resist => KeywordMask.None;
            public int Stagger => 0;
            public int StaggerMax => 0;
            public bool IsStunned { get; private set; }
            public IReadOnlyList<ISkill> Skills { get; } = new ISkill[0];

            public TestCombatant(int id, Side side)
            {
                Id = new CombatantId(id);
                Side = side;
            }

            public void ApplyDamage(int amount) { }
            public void AddStagger(int amount) { }
            public void SetStunned(bool value) => IsStunned = value;
            public void ResetStaggerIfNeededOnStunEnd() { }
        }
    }
}
#endif
