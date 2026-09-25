#if UNITY_INCLUDE_TESTS
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Reward
{
    /// <summary>
    /// Guards the RWD-01 production ownership boundaries. These source checks keep
    /// reward request producers from becoming second Currency/Inventory owners.
    /// </summary>
    public sealed class RewardProductionOwnershipTests
    {
        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        [Test]
        public void ProductionDungeon_HasOneAuthoredRewardServiceAndNoLegacyRewardOwner()
        {
            string scene = Read("Assets/GAME/Scenes/Dungeon_1_Production.unity");
            string bootstrapper = Read("Assets/GAME/Scripts/Core/RuntimeBootstrapper.cs");

            Assert.That(Count(scene, "Assembly-CSharp::Game.Reward.RewardService"), Is.EqualTo(1));
            Assert.That(Count(scene, "Assembly-CSharp::Game.NonCombat.Inventory.CurrencyWallet"), Is.EqualTo(1));
            Assert.That(scene, Does.Not.Contain("Assembly-CSharp::Game.Search.SearchRewardManager"));
            Assert.That(scene, Does.Not.Contain("Assembly-CSharp::Game.Quest.QuestManager"));
            Assert.That(bootstrapper, Does.Contain("FindOrCreate<InventoryService>"));
            Assert.That(bootstrapper, Does.Contain("FindOrCreate<CharacterProgressionService>"));
            Assert.That(bootstrapper, Does.Contain("FindOrCreate<RewardService>"));
        }

        [Test]
        public void CombatBinder_GrantsThroughRewardServiceAndRewardPanelOnlyPresentsResults()
        {
            string binder = Read("Assets/GAME/Scripts/Combat/Runtime/UI/CombatRewardUIBinder.cs");
            string panel = Read("Assets/GAME/Scripts/UI/RewardUIPanel.cs");
            string toast = Read("Assets/GAME/Scripts/UI/FieldRewardToast.cs");

            Assert.That(binder, Does.Contain("RewardService.CreateCombatRewardRequest"));
            Assert.That(binder, Does.Contain("rewardService.GrantReward(request)"));
            Assert.That(binder, Does.Not.Contain("CurrencyWallet"));
            Assert.That(binder, Does.Not.Contain("InventoryService"));
            Assert.That(panel, Does.Contain("AddGrantResultRows"));
            Assert.That(panel, Does.Not.Contain("CurrencyWallet"));
            Assert.That(panel, Does.Not.Contain("InventoryService"));
            Assert.That(toast, Does.Not.Contain("RewardService"));
        }

        [Test]
        public void QuestAndProductionInteractions_CreateRewardRequestsInsteadOfMutatingStores()
        {
            string questCompletion = Read("Assets/GAME/Scripts/Quest/QuestCompletionFlow.cs");
            string rewardInteraction = Read("Assets/GAME/Scripts/Interaction/RewardInteractionEventSO.cs");
            string lootInteraction = Read("Assets/GAME/Scripts/Interaction/RandomLootInteractionEventSO.cs");
            string explorationOutcome = Read("Assets/GAME/Scripts/Interaction/ExplorationOutcomeInteractionEventSO.cs");

            Assert.That(questCompletion, Does.Contain("rewardService.GrantQuestCompletion"));
            Assert.That(questCompletion, Does.Not.Contain("TryAddGold("));
            Assert.That(questCompletion, Does.Not.Contain("TryAddItem("));
            Assert.That(questCompletion, Does.Not.Contain("ApplyExperience("));

            Assert.That(rewardInteraction, Does.Contain("rewardService.GrantReward(new RewardGrantRequest"));
            Assert.That(lootInteraction, Does.Contain("rewardService.GrantReward(new RewardGrantRequest"));
            Assert.That(explorationOutcome, Does.Contain("service.GrantReward(new RewardGrantRequest"));
            Assert.That(rewardInteraction, Does.Not.Contain("TryAddItem("));
            Assert.That(lootInteraction, Does.Not.Contain("TryAddItem("));
            Assert.That(explorationOutcome, Does.Not.Contain("TryAddGold("));
        }

        [Test]
        public void CanonicalSave_RestoresStateOwnersBeforeRewardLedger()
        {
            string saveLoad = Read("Assets/GAME/Scripts/Core/SaveLoadService.cs");

            int inventoryPriority = saveLoad.IndexOf("if (name.Contains(\"Inventory\")", System.StringComparison.Ordinal);
            int progressionPriority = saveLoad.IndexOf("if (name.Contains(\"CharacterProgression\")", System.StringComparison.Ordinal);
            int rewardPriority = saveLoad.IndexOf("if (name.Contains(\"RewardService\")", System.StringComparison.Ordinal);
            Assert.That(inventoryPriority, Is.GreaterThanOrEqualTo(0));
            Assert.That(progressionPriority, Is.GreaterThan(inventoryPriority));
            Assert.That(rewardPriority, Is.GreaterThan(progressionPriority));
            Assert.That(saveLoad, Does.Contain("CaptureGameSaveDataSnapshot"));
            Assert.That(saveLoad, Does.Contain("RestoreGameSaveDataSnapshot"));
        }

        private static string Read(string relativePath) => File.ReadAllText(Path(relativePath));

        private static string Path(string relativePath) =>
            System.IO.Path.Combine(ProjectRoot, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

        private static int Count(string value, string pattern)
        {
            int count = 0;
            int offset = 0;
            while ((offset = value.IndexOf(pattern, offset, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += pattern.Length;
            }

            return count;
        }
    }
}
#endif
