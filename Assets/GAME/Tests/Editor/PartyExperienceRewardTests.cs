#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Combat.Model;
using Game.NonCombat.Inventory;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using Game.Reward;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Integration
{
    public sealed class PartyExperienceRewardTests
    {
        private readonly List<UnityEngine.Object> _created = new();
        private CharacterProgressionService _progression;
        private RewardService _rewards;
        private CurrencyWallet _wallet;
        private InventoryService _inventory;

        [SetUp]
        public void SetUp()
        {
            _progression = Component<CharacterProgressionService>();
            Configure("A", "B", "C", "reserve");
            _wallet = Component<CurrencyWallet>();
            _inventory = Component<InventoryService>();
            _rewards = Component<RewardService>();
            SetField(_rewards, "currencyWallet", _wallet);
            SetField(_rewards, "inventoryService", _inventory);
            SetField(_rewards, "characterProgressionService", _progression);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Victory_EachParticipantGetsFullExperienceIncludingKo(int count)
        {
            CombatResult combat = Victory("battle", new[] { "A", "B", "C" }.Take(count).ToArray());
            RewardGrantResult grant = _rewards.GrantReward(RewardService.CreateCombatRewardRequest(combat, null));
            foreach (string id in new[] { "A", "B", "C" }.Take(count)) AssertExperience(id, 100);
            AssertExperience("reserve", 0);
            Assert.That(grant.Exp, Is.EqualTo(100));
            Assert.That(grant.ExperienceResults, Has.Count.EqualTo(count));
            Assert.That(grant.ExperienceResults.All(result => result.Settled && result.AppliedExperience == 100), Is.True);
            Assert.That(grant.PartialFailure, Is.False);
            Assert.That(_wallet.Gold, Is.EqualTo(7));
            Assert.That(Capture().reward.ledger, Has.Count.EqualTo(1));
        }

        [Test]
        public void RecipientSnapshot_IsImmutableNormalizedAndDeduplicated()
        {
            List<string> ids = new() { " B ", "A", "A", null, " " };
            RewardGrantRequest request = Request("snapshot", ids);
            ids.Clear(); ids.Add("reserve");
            Assert.That(request.ExperienceRecipientIds, Is.EqualTo(new[] { "A", "B" }));
            _rewards.GrantReward(request);
            AssertExperience("A", 100); AssertExperience("B", 100); AssertExperience("reserve", 0);
        }

        [Test]
        public void ReorderedOrChangedRecipients_CannotReplayCompletionOrNonExpChannels()
        {
            _rewards.GrantReward(Request("once", new[] { "A", "B", "C" }));
            RewardGrantResult duplicate = _rewards.GrantReward(Request("once", new[] { "C", "B", "A", "reserve" }));
            Assert.That(duplicate.DuplicateBlocked, Is.True);
            Assert.That(duplicate.Gold, Is.Zero); Assert.That(duplicate.Exp, Is.Zero); Assert.That(duplicate.ItemCount, Is.Zero);
            foreach (string id in new[] { "A", "B", "C" }) AssertExperience(id, 100);
            AssertExperience("reserve", 0);
            Assert.That(_wallet.Gold, Is.EqualTo(7)); Assert.That(_inventory.GetCount("token"), Is.EqualTo(2));
            Assert.That(Capture().reward.combatLedger, Has.Count.EqualTo(1));
        }

        [Test]
        public void LegacySingleResult_PreservesDefaultAndExplicitTarget()
        {
            CombatResult legacy = Victory("legacy");
            Assert.That(RewardService.CreateCombatRewardRequest(legacy, null).ExperienceRecipientIds, Is.Null);
            _rewards.GrantCombatResult(legacy);
            AssertExperience("A", 100);
            SetProperty(legacy, "CompletionId", "legacy.explicit");
            SetProperty(legacy, "ProgressionTargetCharacterId", "B");
            _rewards.GrantCombatResult(legacy);
            AssertExperience("B", 100);
        }

        [Test]
        public void EmptyExplicitMapping_DoesNotUseDefaultTargetOrMarkExpSettled()
        {
            CombatResult invalid = Victory("invalid");
            SetProperty(invalid, "RequiresExplicitExperienceRecipients", true);
            RewardGrantResult grant = _rewards.GrantReward(RewardService.CreateCombatRewardRequest(invalid, null));
            Assert.That(grant.InvalidRequest, Is.True); Assert.That(grant.PartialFailure, Is.True);
            Assert.That(grant.ExpSettled, Is.False);
            AssertExperience("A", 0);
            _rewards.ReconcilePendingExperience();
            _rewards.RestoreSaveData(Migrate(JsonUtility.ToJson(Capture())));
            AssertExperience("A", 0);
            Assert.That(_rewards.GrantCombatResult(invalid).DuplicateBlocked, Is.True);
        }

        [Test]
        public void Defeat_PreservesNoRewardPolicyEvenWithParticipants()
        {
            CombatResult combat = Victory("defeat", "A", "B");
            combat.EndReason = CombatEndReason.Defeat;
            _rewards.GrantCombatResult(combat);
            AssertExperience("A", 0); AssertExperience("B", 0); Assert.That(_wallet.Gold, Is.Zero);
        }

        [Test]
        public void MissingRecipient_PreservesSuccessfulRecipientsAndRetriesOnlyPendingAfterLoad()
        {
            Configure("A");
            RewardGrantResult partial = _rewards.GrantReward(Request("partial", new[] { "A", "B" }));
            Assert.That(partial.PartialFailure, Is.True); Assert.That(partial.ExpSettled, Is.False);
            AssertExperience("A", 100);
            Assert.That(_rewards.GrantReward(Request("partial", new[] { "A", "C" })).DuplicateBlocked, Is.True);
            Assert.That(_rewards.ReconcilePendingExperience(), Is.Zero);
            GameSaveData saved = Migrate(JsonUtility.ToJson(Capture()));
            Assert.That(saved.reward.ledger[0].experienceRecipients[0].settled, Is.True);
            Assert.That(saved.reward.ledger[0].experienceRecipients[1].settled, Is.False);
            Configure("A", "B", "C");
            _progression.RestoreSaveData(saved);
            _rewards.RestoreSaveData(saved);
            AssertExperience("A", 100); AssertExperience("B", 100); AssertExperience("C", 0);
            Assert.That(_rewards.ReconcilePendingExperience(), Is.Zero);
            Assert.That(_wallet.Gold, Is.EqualTo(7)); Assert.That(_inventory.GetCount("token"), Is.EqualTo(2));
        }

        [Test]
        public void ReentrantCallbacks_AreBlockedBeforeGoldAndExpNotifications()
        {
            RewardGrantRequest request = Request("callback", new[] { "A", "B" });
            int duplicates = 0;
            _wallet.Changed += _ => { Assert.That(_rewards.GrantReward(request).DuplicateBlocked, Is.True); duplicates++; };
            _progression.ProgressionChanged += _ =>
            {
                Assert.That(_rewards.GrantReward(request).DuplicateBlocked, Is.True);
                Assert.That(_rewards.ReconcilePendingExperience(), Is.Zero);
                duplicates++;
            };
            _rewards.GrantReward(request);
            Assert.That(duplicates, Is.EqualTo(3));
            AssertExperience("A", 100); AssertExperience("B", 100);
            Assert.That(_wallet.Gold, Is.EqualTo(7)); Assert.That(_inventory.GetCount("token"), Is.EqualTo(2));
        }

        [Test]
        public void SaveDuringFirstRecipientCallback_RestoresWithoutRegrantingThatRecipient()
        {
            GameSaveData duringCallback = null;
            _progression.ProgressionChanged += result =>
            {
                if (result.CharacterId == "A") duringCallback = Migrate(JsonUtility.ToJson(Capture()));
            };
            RewardGrantRequest request = Request("save.callback", new[] { "A", "B", "C" });
            _rewards.GrantReward(request);
            Assert.That(duringCallback.reward.ledger[0].experienceRecipients[0].settled, Is.True);
            Assert.That(duringCallback.reward.ledger[0].experienceRecipients[1].settled, Is.False);
            _progression.RestoreSaveData(duringCallback);
            _rewards.RestoreSaveData(duringCallback);
            foreach (string id in new[] { "A", "B", "C" }) AssertExperience(id, 100);
            Assert.That(_rewards.GrantReward(request).DuplicateBlocked, Is.True);
        }

        [Test]
        public void ThrowingProgressionObserver_CannotTurnAppliedExpIntoPendingOrBlockNextRecipient()
        {
            int observed = 0;
            _progression.ProgressionChanged += _ => throw new InvalidOperationException("observer failure");
            _progression.ProgressionChanged += _ => observed++;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("ProgressionChanged subscriber failed.*observer failure"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("ProgressionChanged subscriber failed.*observer failure"));
            RewardGrantResult result = _rewards.GrantReward(Request("throw", new[] { "A", "B" }));
            Assert.That(result.PartialFailure, Is.False); Assert.That(result.ExpSettled, Is.True);
            Assert.That(observed, Is.EqualTo(2)); Assert.That(_rewards.ReconcilePendingExperience(), Is.Zero);
            AssertExperience("A", 100); AssertExperience("B", 100);
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        public void ExistingSingleLedger_MigratesWithoutRecalculatingPaidExperience(int schema)
        {
            string json = "{\"header\":{\"formatId\":\"GAME_002\",\"schemaVersion\":" + schema +
                "},\"progression\":{\"characters\":[{\"characterId\":\"A\",\"level\":1,\"experience\":100}]}," +
                "\"reward\":{\"ledger\":[{\"sourceType\":\"Combat\",\"sourceId\":\"old\",\"requestedExp\":100,\"exp\":100,\"progressionTargetId\":\"A\",\"expSettled\":true}]}}";
            GameSaveData saved = Migrate(json);
            _progression.RestoreSaveData(saved); _rewards.RestoreSaveData(saved);
            AssertExperience("A", 100);
            Assert.That(_rewards.GrantReward(Request("old", new[] { "A", "B" })).DuplicateBlocked, Is.True);
            AssertExperience("B", 0);
        }

        [Test]
        public void MixedSingleAndPartyLedger_RoundTripsAndClonesRecipientRecordsIndependently()
        {
            _rewards.GrantReward(new RewardGrantRequest(RewardSourceType.Story, "single", exp: 50, progressionTargetId: "A"));
            RewardGrantRequest request = Request("party.save", new[] { "A", "B", "C" });
            _rewards.GrantReward(request);
            GameSaveData data = Capture();
            data.reward.combatLedger[0].experienceRecipients[0].appliedExp = 0;
            Assert.That(data.reward.ledger[0].experienceRecipients[0].appliedExp, Is.EqualTo(100));
            GameSaveData loaded = Migrate(JsonUtility.ToJson(data));
            _progression.RestoreSaveData(loaded); _rewards.RestoreSaveData(loaded);
            AssertExperience("A", 150); AssertExperience("B", 100); AssertExperience("C", 100);
            Assert.That(_rewards.GrantReward(request).DuplicateBlocked, Is.True);
            Assert.That(loaded.reward.ledger, Has.Count.EqualTo(2));
        }

        [Test]
        public void FailedLedgerCheckpoint_RollsBackProgressionBeforeObservers()
        {
            int notifications = 0;
            _progression.ProgressionChanged += _ => notifications++;
            MethodInfo method = typeof(CharacterProgressionService).GetMethod("ApplyExperience",
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(string), typeof(int), typeof(Action<ExperienceApplyResult>) }, null);
            Action<ExperienceApplyResult> failCheckpoint = _ => throw new InvalidOperationException("ledger failure");
            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                method.Invoke(_progression, new object[] { "A", 100, failCheckpoint }));
            Assert.That(exception.InnerException.Message, Is.EqualTo("ledger failure"));
            AssertExperience("A", 0); Assert.That(notifications, Is.Zero);
            Assert.That(_rewards.GrantReward(Request("retry.checkpoint", new[] { "A", "B" })).ExpSettled, Is.True);
            AssertExperience("A", 100); AssertExperience("B", 100);
        }

        [Test]
        public void MissingProgressionService_RemainsPendingAndReconcilesEachRecipientOnce()
        {
            UnityEngine.Object.DestroyImmediate(_progression.gameObject);
            SetField(_rewards, "characterProgressionService", null);
            RewardGrantRequest request = Request("missing.service", new[] { "A", "B" });
            RewardGrantResult pending = _rewards.GrantReward(request);
            Assert.That(pending.PartialFailure, Is.True); Assert.That(pending.ExpSettled, Is.False);
            _progression = Component<CharacterProgressionService>(); Configure("A", "B");
            SetField(_rewards, "characterProgressionService", _progression);
            Assert.That(_rewards.ReconcilePendingExperience(), Is.EqualTo(2));
            Assert.That(_rewards.ReconcilePendingExperience(), Is.Zero);
            AssertExperience("A", 100); AssertExperience("B", 100);
            Assert.That(_wallet.Gold, Is.EqualTo(7)); Assert.That(_inventory.GetCount("token"), Is.EqualTo(2));
            Assert.That(_rewards.GrantReward(request).DuplicateBlocked, Is.True);
        }

        [Test]
        public void MaxLevelRecipient_IsTerminallySettledWithoutRedirectingItsExperience()
        {
            GameSaveData data = Capture();
            data.progression.characters.Find(character => character.characterId == "B").level = 3;
            _progression.RestoreSaveData(data);
            RewardGrantResult result = _rewards.GrantReward(Request("max", new[] { "A", "B" }));
            Assert.That(result.ExpSettled, Is.True);
            Assert.That(result.ExperienceResults[1].AppliedExperience, Is.Zero);
            Assert.That(result.ExperienceResults[1].Settled, Is.True);
            Assert.That(_rewards.ReconcilePendingExperience(), Is.Zero);
            AssertExperience("A", 100); AssertExperience("reserve", 0);
        }

        [Test]
        public void OldResultAfterNextCompletion_IsStillBlockedAndCannotLeakRecipients()
        {
            RewardGrantRequest old = Request("old.session", new[] { "A", "B" });
            _rewards.GrantReward(old);
            _rewards.GrantReward(Request("new.session", new[] { "C" }));
            Assert.That(_rewards.GrantReward(old).DuplicateBlocked, Is.True);
            AssertExperience("A", 100); AssertExperience("B", 100); AssertExperience("C", 100);
            Assert.That(Capture().reward.ledger, Has.Count.EqualTo(2));
        }

        private static RewardGrantRequest Request(string completion, IEnumerable<string> ids) =>
            new(RewardSourceType.Combat, completion, gold: 7, exp: 100, itemId: "token", itemCount: 2, experienceRecipientIds: ids);

        private static CombatResult Victory(string completion, params string[] ids)
        {
            CombatResult result = new() { IsWin = true, EndReason = CombatEndReason.Victory, TotalExp = 100, TotalGold = 7 };
            SetProperty(result, "CompletionId", completion);
            List<CombatAllyParticipantResult> participants = new();
            ConstructorInfo constructor = typeof(CombatAllyParticipantResult).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            for (int i = 0; i < ids.Length; i++)
                participants.Add((CombatAllyParticipantResult)constructor.Invoke(new object[] { ids[i], i + 1, i == 1 ? 0 : 10 }));
            SetProperty(result, "AllyParticipants", participants.AsReadOnly());
            return result;
        }

        private void Configure(params string[] ids)
        {
            List<CharacterProgressionDefinitionSO> definitions = new();
            foreach (string id in ids)
            {
                CharacterProgressionDefinitionSO definition = ScriptableObject.CreateInstance<CharacterProgressionDefinitionSO>();
                _created.Add(definition);
                SetField(definition, "characterId", id); SetField(definition, "startingLevel", 1);
                SetField(definition, "maximumLevel", 3); SetField(definition, "experienceRequiredByLevel", new[] { 1000, 1000 });
                definitions.Add(definition);
            }
            typeof(CharacterProgressionService).GetMethod("ConfigureForTests", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_progression, new object[] { "A", definitions.ToArray() });
        }

        private GameSaveData Capture()
        {
            GameSaveData data = new();
            _progression.CaptureSaveData(data); _wallet.CaptureSaveData(data); _inventory.CaptureSaveData(data); _rewards.CaptureSaveData(data);
            return data;
        }

        private static GameSaveData Migrate(string json)
        {
            Type migrator = typeof(GameSaveData).Assembly.GetType("Game.NonCombat.Save.GameSaveDataMigrator");
            object[] args = { json, null, false, null };
            Assert.That((bool)migrator.GetMethod("TryMigrate", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args), Is.True, args[3] as string);
            return (GameSaveData)args[1];
        }

        private void AssertExperience(string id, int expected)
        {
            Assert.That(_progression.TryGetState(id, out int level, out int exp), Is.True, id);
            Assert.That(level, Is.EqualTo(1), id); Assert.That(exp, Is.EqualTo(expected), id);
        }

        private T Component<T>() where T : Component
        {
            GameObject owner = new(typeof(T).Name); _created.Add(owner); return owner.AddComponent<T>();
        }

        private static void SetField(object owner, string name, object value) =>
            owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
        private static void SetProperty(object owner, string name, object value) =>
            owner.GetType().GetProperty(name).SetValue(owner, value);
    }
}
#endif
