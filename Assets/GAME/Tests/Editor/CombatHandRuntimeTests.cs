#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Environment;
using Game.Combat.Model;
using NUnit.Framework;

namespace Game.Tests.Combat
{
    public sealed class CombatHandRuntimeTests
    {
        // All card counts, MP, and seeds in this class are validation fixtures, not Production balance.
        [Test]
        public void SnapshotInitialDraw_SeparatesClashAndNormalWithoutInventingCards()
        {
            Fixture f = NewFixture();
            List<CombatHandEntry> equipped = new()
            {
                new(f.AllyNormal, CombatCardKind.Normal),
                new(f.AllyNormal, CombatCardKind.Normal),
                new(f.AllyClash, CombatCardKind.Clash),
                new(f.AllyClash, CombatCardKind.Clash),
                new(f.AllyClash, CombatCardKind.Clash)
            };
            Assert.That(f.Session.TryEnableHandValidation(f.Ally, equipped, 2, 3, 71), Is.True);
            equipped.Clear();
            Assert.That(f.Session.TryGetHand(f.Ally, out CombatHandRuntime hand), Is.True);
            Assert.That(hand.DeckSnapshot.Count, Is.EqualTo(5));
            Assert.That(Count(hand.Hand, CombatCardKind.Normal), Is.EqualTo(2));
            Assert.That(Count(hand.Hand, CombatCardKind.Clash), Is.EqualTo(3));
            Assert.That(new HashSet<long>(Ids(hand.Hand)).Count, Is.EqualTo(5));
            Assert.That(f.Session.TryEnableHandValidation(f.Ally, equipped, 2, 3, 71), Is.False);

            Fixture sparse = NewFixture();
            Assert.That(sparse.Session.TryEnableHandValidation(sparse.Ally,
                new[] { new CombatHandEntry(sparse.AllyClash, CombatCardKind.Clash) },
                2, 3, 71), Is.True);
            sparse.Session.TryGetHand(sparse.Ally, out CombatHandRuntime sparseHand);
            Assert.That(Count(sparseHand.Hand, CombatCardKind.Normal), Is.Zero);
            Assert.That(Count(sparseHand.Hand, CombatCardKind.Clash), Is.EqualTo(1));
        }

        [Test]
        public void Commit_MovesExactCardAndMpOnce_RejectionLeavesBothUntouched()
        {
            Fixture f = NewFixture();
            Enable(f, 4);
            f.Session.TryGetHand(f.Ally, out CombatHandRuntime hand);
            f.Session.TryGetHand(f.Enemy, out CombatHandRuntime enemyHand);
            CombatHandCard attackCard = Find(hand, CombatCardKind.Normal);
            CombatHandCard responseCard = Find(enemyHand, CombatCardKind.Clash);
            int initialMp = f.Session.GetCombatState(f.Ally).CurrentMp;
            int hp = f.Enemy.HP;

            CombatAttackDeclaration invalid = new(f.Ally, f.Enemy, f.AllyNormal, 99999);
            CombatResponseDeclaration response = new(f.Enemy, f.EnemyClash, responseCard.RuntimeId);
            Assert.That(CombatExchangeCommitPolicy.TryCommit(f.Session, invalid, response, out _), Is.False);
            Assert.That(f.Session.GetCombatState(f.Ally).CurrentMp, Is.EqualTo(initialMp));
            Assert.That(hand.NormalDiscard.Count, Is.Zero);
            Assert.That(enemyHand.ClashDiscard.Count, Is.Zero);

            CombatAttackDeclaration attack = new(f.Ally, f.Enemy, f.AllyNormal, attackCard.RuntimeId);
            Assert.That(CombatExchangeCommitPolicy.TryCommit(f.Session, attack, response, out _), Is.True);
            Assert.That(f.Session.GetCombatState(f.Ally).CurrentMp, Is.EqualTo(initialMp - 1));
            Assert.That(hand.NormalDiscard[0].RuntimeId, Is.EqualTo(attackCard.RuntimeId));
            Assert.That(Count(hand.Hand, CombatCardKind.Normal), Is.EqualTo(1));
            Assert.That(enemyHand.ClashDiscard[0].RuntimeId, Is.EqualTo(responseCard.RuntimeId));
            Assert.That(f.Enemy.HP, Is.EqualTo(hp));
            Assert.That(CombatExchangeCommitPolicy.TryCommit(f.Session, attack, response, out _), Is.False);
            Assert.That(f.Session.GetCombatState(f.Ally).CurrentMp, Is.EqualTo(initialMp - 1));
            Assert.That(hand.NormalDiscard.Count, Is.EqualTo(1));
        }

        [Test]
        public void ClashSeries_ReusesFirstCards_ThenCommitsNewIdsOnlyOnce()
        {
            Fixture f = NewFixture();
            Enable(f, 5);
            f.Session.TryGetHand(f.Ally, out CombatHandRuntime allyHand);
            f.Session.TryGetHand(f.Enemy, out CombatHandRuntime enemyHand);
            CombatHandCard firstAttack = Find(allyHand, CombatCardKind.Normal);
            CombatHandCard firstResponse = Find(enemyHand, CombatCardKind.Clash);
            CombatStateMachine sm = f.StateMachine;
            Assert.That(sm.TryDeclareAttack(new CombatAttackDeclaration(f.Ally, f.Enemy,
                f.AllyNormal, firstAttack.RuntimeId), f.Session.ExchangeState.Version), Is.True);
            Assert.That(sm.TryDeclareResponse(new CombatResponseDeclaration(f.Enemy,
                f.EnemyClash, firstResponse.RuntimeId), f.Session.ExchangeState.Version), Is.True);
            Assert.That(sm.TryCommitExchange(f.Session.ExchangeState.Version), Is.True);
            Assert.That(allyHand.NormalDiscard.Count, Is.EqualTo(1));
            Assert.That(enemyHand.ClashDiscard.Count, Is.EqualTo(1));
            Assert.That(sm.TryBeginApproach(f.Session.ExchangeState.Version), Is.True);
            Assert.That(sm.CompleteApproach(f.Session.ExchangeState.Version), Is.True);
            CombatClashSeriesState series = new(f.Session, sm,
                new CombatClashSeriesConfig(5, 5, 3));
            int mpAfterFirst = f.Session.GetCombatState(f.Ally).CurrentMp;
            Assert.That(series.TryResolveRound(series.CommandToken,
                new CombatClashSideInput(f.Ally.Id, f.AllyNormal.Id, 2, 1, true),
                new CombatClashSideInput(f.Enemy.Id, f.EnemyClash.Id, 2, 1, false),
                out _), Is.True);
            Assert.That(f.Session.GetCombatState(f.Ally).CurrentMp, Is.EqualTo(mpAfterFirst));
            Assert.That(allyHand.NormalDiscard.Count, Is.EqualTo(1));

            long staleToken = series.CommandToken - 1;
            CombatHandCard nextAttack = Find(allyHand, CombatCardKind.Clash);
            CombatHandCard nextResponse = Find(enemyHand, CombatCardKind.Clash);
            Assert.That(series.TryCommitNextRound(staleToken, f.AllyClash, f.EnemyClash,
                nextAttack.RuntimeId, nextResponse.RuntimeId, out _), Is.False);
            Assert.That(series.TryCommitNextRound(series.CommandToken, f.AllyClash, f.EnemyClash,
                nextAttack.RuntimeId, nextResponse.RuntimeId, out _), Is.True);
            Assert.That(f.Session.GetCombatState(f.Ally).CurrentMp, Is.EqualTo(mpAfterFirst - 1));
            Assert.That(allyHand.ClashDiscard[0].RuntimeId, Is.EqualTo(nextAttack.RuntimeId));
            Assert.That(enemyHand.ClashDiscard.Count, Is.EqualTo(2));
            Assert.That(series.TryCommitNextRound(staleToken, f.AllyClash, f.EnemyClash,
                nextAttack.RuntimeId, nextResponse.RuntimeId, out _), Is.False);
            Assert.That(allyHand.ClashDiscard.Count, Is.EqualTo(1));
            Assert.That(f.Enemy.HP, Is.EqualTo(10));
        }

        [Test]
        public void MpShortage_AndWrongCardKind_KeepCardInHand()
        {
            Fixture f = NewFixture(initialMp: 0);
            Enable(f, 3);
            f.Session.TryGetHand(f.Ally, out CombatHandRuntime hand);
            CombatHandCard normal = Find(hand, CombatCardKind.Normal);
            Assert.That(CombatExchangeCommitPolicy.TryCommit(f.Session,
                new CombatAttackDeclaration(f.Ally, f.Enemy, f.AllyNormal, normal.RuntimeId),
                null, out CombatExchangeCommitResult failure), Is.False);
            Assert.That(failure.Failure, Is.EqualTo(CombatExchangeCommitFailure.InsufficientAttackMp));
            Assert.That(hand.NormalDiscard.Count, Is.Zero);
            Assert.That(hand.Hand.Count, Is.EqualTo(3));

            Assert.That(CombatExchangeCommitPolicy.TryCommit(f.Session,
                new CombatAttackDeclaration(f.Ally, f.Enemy, f.AllyClash,
                    Find(hand, CombatCardKind.Clash).RuntimeId),
                null, out failure), Is.False);
            Assert.That(failure.Failure, Is.EqualTo(CombatExchangeCommitFailure.InvalidAttack));
        }

        [Test]
        public void ResponseMpShortage_DowngradesWithoutDiscardingResponseCard()
        {
            Fixture f = NewFixture(initialMp: 1);
            Enable(f, 5);
            f.Session.GetCombatState(f.Enemy).SetMp(0);
            f.Session.TryGetHand(f.Ally, out CombatHandRuntime allyHand);
            f.Session.TryGetHand(f.Enemy, out CombatHandRuntime enemyHand);
            CombatHandCard attack = Find(allyHand, CombatCardKind.Normal);
            CombatHandCard response = Find(enemyHand, CombatCardKind.Clash);

            Assert.That(CombatExchangeCommitPolicy.TryCommit(f.Session,
                new CombatAttackDeclaration(f.Ally, f.Enemy, f.AllyNormal, attack.RuntimeId),
                new CombatResponseDeclaration(f.Enemy, f.EnemyClash, response.RuntimeId),
                out CombatExchangeCommitResult result), Is.True);
            Assert.That(result.ResponseDowngradedForInsufficientMp, Is.True);
            Assert.That(allyHand.NormalDiscard.Count, Is.EqualTo(1));
            Assert.That(enemyHand.ClashDiscard.Count, Is.Zero);
            Assert.That(enemyHand.Hand.Count, Is.EqualTo(2));
        }

        [Test]
        public void EmptyHand_RejectsSkillWithoutMpChange()
        {
            Fixture f = NewFixture();
            Assert.That(f.Session.TryEnableHandValidation(f.Ally,
                Array.Empty<CombatHandEntry>(), 2, 3, 4), Is.True);
            int mp = f.Session.GetCombatState(f.Ally).CurrentMp;
            Assert.That(CombatExchangeCommitPolicy.TryCommit(f.Session,
                new CombatAttackDeclaration(f.Ally, f.Enemy, f.AllyNormal), null,
                out CombatExchangeCommitResult result), Is.False);
            Assert.That(result.Failure, Is.EqualTo(CombatExchangeCommitFailure.InvalidAttack));
            Assert.That(f.Session.GetCombatState(f.Ally).CurrentMp, Is.EqualTo(mp));
        }

        [Test]
        public void DistinctCyclesRefillOnce_AndTerminalBlocksRefill()
        {
            Fixture f = NewFixture();
            Enable(f, 5);
            f.Session.TryGetHand(f.Ally, out CombatHandRuntime hand);
            Assert.That(CombatExchangeCommitPolicy.TryCommit(f.Session,
                new CombatAttackDeclaration(f.Ally, f.Enemy, f.AllyNormal,
                    Find(hand, CombatCardKind.Normal).RuntimeId), null, out _), Is.True);
            Assert.That(hand.Hand.Count, Is.EqualTo(4));
            Assert.That(f.Session.TryRecordCompletedAttackCycle(Side.Allies, 1, true), Is.True);
            Assert.That(hand.Hand.Count, Is.EqualTo(4));
            Assert.That(f.Session.TryRecordCompletedAttackCycle(Side.Allies, 1, true), Is.False);
            Assert.That(f.Session.TryRecordCompletedAttackCycle(Side.Enemies, 2, false), Is.False);
            Assert.That(hand.Hand.Count, Is.EqualTo(4));
            Assert.That(f.Session.TryRecordCompletedAttackCycle(Side.Enemies, 2, true), Is.True);
            Assert.That(hand.Hand.Count, Is.EqualTo(5));
            Assert.That(hand.RefillCount, Is.EqualTo(1));
            Assert.That(f.Session.TryRecordCompletedAttackCycle(Side.Enemies, 2, true), Is.False);
            f.Enemy.ApplyDamage(999);
            Assert.That(f.Session.TryRecordCompletedAttackCycle(Side.Allies, 3, true), Is.False);
            Assert.That(hand.RefillCount, Is.EqualTo(1));
        }

        [Test]
        public void SeedAndCardIds_GiveRepeatableDrawDiscardTrace()
        {
            string first = Trace(123);
            Assert.That(Trace(123), Is.EqualTo(first));
            Assert.That(first, Does.Contain("discard:"));
            Assert.That(first, Does.Contain("refill:"));
        }

        [Test]
        public void MidCombatEquippedListMutation_DoesNotChangeHand_NewSessionUsesNewList()
        {
            Fixture f = NewFixture();
            List<CombatHandEntry> equipped = new() { new(f.AllyNormal, CombatCardKind.Normal) };
            Assert.That(f.Session.TryEnableHandValidation(f.Ally, equipped, 2, 3, 8), Is.True);
            equipped.Add(new CombatHandEntry(f.AllyClash, CombatCardKind.Clash));
            f.Session.TryGetHand(f.Ally, out CombatHandRuntime hand);
            Assert.That(hand.DeckSnapshot.Count, Is.EqualTo(1));

            Fixture next = NewFixture();
            Assert.That(next.Session.TryEnableHandValidation(next.Ally,
                new[] { new CombatHandEntry(next.AllyNormal, CombatCardKind.Normal),
                    new CombatHandEntry(next.AllyClash, CombatCardKind.Clash) }, 2, 3, 8), Is.True);
            next.Session.TryGetHand(next.Ally, out CombatHandRuntime nextHand);
            Assert.That(nextHand.DeckSnapshot.Count, Is.EqualTo(2));
        }

        private static string Trace(int seed)
        {
            Fixture f = NewFixture();
            Assert.That(f.Session.TryEnableHandValidation(f.Ally, new[]
            {
                new CombatHandEntry(f.AllyNormal, CombatCardKind.Normal),
                new CombatHandEntry(f.AllyNormal, CombatCardKind.Normal),
                new CombatHandEntry(f.AllyNormal, CombatCardKind.Normal)
            }, 2, 0, seed), Is.True);
            f.Session.TryGetHand(f.Ally, out CombatHandRuntime hand);
            List<long> initial = Ids(hand.Hand);
            Assert.That(CombatExchangeCommitPolicy.TryCommit(f.Session,
                new CombatAttackDeclaration(f.Ally, f.Enemy, f.AllyNormal, initial[0]),
                null, out _), Is.True);
            string discard = string.Join(",", Ids(hand.NormalDiscard));
            f.Session.TryRecordCompletedAttackCycle(Side.Allies, 1, true);
            f.Session.TryRecordCompletedAttackCycle(Side.Enemies, 2, true);
            Assert.That(CombatExchangeCommitPolicy.TryCommit(f.Session,
                new CombatAttackDeclaration(f.Ally, f.Enemy, f.AllyNormal, initial[1]),
                null, out _), Is.True);
            f.Session.TryRecordCompletedAttackCycle(Side.Allies, 3, true);
            f.Session.TryRecordCompletedAttackCycle(Side.Enemies, 4, true);
            Assert.That(hand.Hand.Count, Is.EqualTo(2));
            Assert.That(hand.NormalDiscard.Count, Is.Zero);
            Assert.That(Ids(hand.Hand).Contains(initial[0]) ||
                Ids(hand.Hand).Contains(initial[1]), Is.True);
            return $"initial:{string.Join(",", initial)};discard:{discard};refill:{string.Join(",", Ids(hand.Hand))}";
        }

        private static void Enable(Fixture f, int allyCards)
        {
            List<CombatHandEntry> ally = new();
            for (int i = 0; i < allyCards / 2; i++)
                ally.Add(new CombatHandEntry(f.AllyNormal, CombatCardKind.Normal));
            for (int i = allyCards / 2; i < allyCards; i++)
                ally.Add(new CombatHandEntry(f.AllyClash, CombatCardKind.Clash));
            Assert.That(f.Session.TryEnableHandValidation(f.Ally, ally, 2, 3, 13), Is.True);
            Assert.That(f.Session.TryEnableHandValidation(f.Enemy,
                new[] { new CombatHandEntry(f.EnemyClash, CombatCardKind.Clash),
                    new CombatHandEntry(f.EnemyClash, CombatCardKind.Clash) },
                0, 3, 17), Is.True);
        }

        private static CombatHandCard Find(CombatHandRuntime hand, CombatCardKind kind)
        {
            foreach (CombatHandCard card in hand.Hand)
                if (card.Kind == kind) return card;
            Assert.Fail($"Fixture has no {kind} card.");
            return null;
        }

        private static int Count(IReadOnlyList<CombatHandCard> cards, CombatCardKind kind)
        {
            int count = 0;
            foreach (CombatHandCard card in cards)
                if (card.Kind == kind) count++;
            return count;
        }

        private static List<long> Ids(IReadOnlyList<CombatHandCard> cards)
        {
            List<long> ids = new();
            foreach (CombatHandCard card in cards) ids.Add(card.RuntimeId);
            return ids;
        }

        private static Fixture NewFixture(int initialMp = 5)
        {
            TestCombatant ally = new(1, Side.Allies);
            TestCombatant enemy = new(2, Side.Enemies);
            TestSkill normal = new(1);
            TestSkill clash = new(2);
            TestSkill enemyClash = new(3);
            ally.AddSkill(normal);
            ally.AddSkill(clash);
            enemy.AddSkill(enemyClash);
            CombatRuntimeConfig config = new(10, initialMp, 3, 0);
            CombatSession session = new(StartReason.PlayerFirstHit, Side.Allies,
                new InspirationPool(10), new CombatEnvironment(),
                CombatFlowMode.StandoffClashChain, config);
            session.Allies.Add(ally);
            session.Enemies.Add(enemy);
            CombatStateMachine stateMachine = new(session);
            stateMachine.Tick();
            return new Fixture(session, stateMachine, ally, enemy, normal, clash, enemyClash);
        }

        private sealed class Fixture
        {
            public CombatSession Session { get; }
            public CombatStateMachine StateMachine { get; }
            public TestCombatant Ally { get; }
            public TestCombatant Enemy { get; }
            public TestSkill AllyNormal { get; }
            public TestSkill AllyClash { get; }
            public TestSkill EnemyClash { get; }

            public Fixture(CombatSession session, CombatStateMachine stateMachine,
                TestCombatant ally, TestCombatant enemy,
                TestSkill normal, TestSkill clash, TestSkill enemyClash)
            {
                Session = session;
                StateMachine = stateMachine;
                Ally = ally;
                Enemy = enemy;
                AllyNormal = normal;
                AllyClash = clash;
                EnemyClash = enemyClash;
            }
        }

        private sealed class TestSkill : ISkill, ICombatMpCostProvider
        {
            public SkillId Id { get; }
            public string Name => "Fixture";
            public int InspirationCost => 0;
            public KeywordMask Keywords => KeywordMask.None;
            public SkillTag Tag => SkillTag.Attack;
            public TargetingRule Targeting => TargetingRule.SingleEnemy;
            public SkillMovementMode MovementMode => SkillMovementMode.None;
            public float DesiredTargetDistance => 0f;
            public float MoveSpeed => 0f;
            public float ActionDelayAfterMove => 0f;
            public int BaseDamage => 1;
            public int BaseStagger => 0;
            public int WeaknessStaggerBonus => 0;
            public int Speed => 1;
            public bool ConsumesTurn => true;
            public int MpCost => 1;

            public TestSkill(int id) => Id = new SkillId(id);
        }

        private sealed class TestCombatant : ICombatant, ICombatantRuntimeStateBinding
        {
            private readonly List<ISkill> _skills = new();
            private CombatantCombatState _state;
            public CombatantId Id { get; }
            public Side Side { get; }
            public int HP => _state?.CurrentHp ?? 10;
            public int MaxHP => 10;
            public KeywordMask Weakness => KeywordMask.None;
            public KeywordMask Resist => KeywordMask.None;
            public int Stagger => 0;
            public int StaggerMax => 0;
            public bool IsStunned { get; private set; }
            public IReadOnlyList<ISkill> Skills => _skills;

            public TestCombatant(int id, Side side)
            {
                Id = new CombatantId(id);
                Side = side;
            }

            public void AddSkill(ISkill skill) => _skills.Add(skill);
            public void BindCombatState(CombatantCombatState state) => _state = state;
            public void ApplyDamage(int amount) => _state?.ApplyDamage(amount);
            public void AddStagger(int amount) { }
            public void SetStunned(bool value) => IsStunned = value;
            public void ResetStaggerIfNeededOnStunEnd() { }
        }
    }
}
#endif
