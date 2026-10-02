using System.Collections.Generic;
using System.Linq;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Combat.UI;
using Game.EditorTools;
using Game.Player;
using Game.Quest;
using Game.Reward;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests.Integration
{
    public sealed class DungeonOneEncounterCombatRewardProductionTests
    {
        private static readonly string[] EncounterIds =
        {
            "dungeon1.encounter.01",
            "dungeon1.encounter.02",
            "dungeon1.encounter.03"
        };

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        [Category("CNTD106")]
        public void ProductionDungeonOne_AllEncounterEntriesUseTheCanonicalGroupAndCombatEntryPoint()
        {
            OpenProductionScene();

            CombatEntryPoint entryPoint = FindAll<CombatEntryPoint>().Single();
            CombatEncounterGroup[] groups = FindAll<CombatEncounterGroup>();
            CombatEncounterTrigger2D[] triggers = FindAll<CombatEncounterTrigger2D>();
            PlayerFieldAttackController fieldAttack = FindAll<PlayerFieldAttackController>().Single();

            Assert.That(groups.Select(group => group.EncounterId), Is.EquivalentTo(EncounterIds));
            Assert.That(triggers, Has.Length.EqualTo(3));
            Assert.That(Reference(fieldAttack, "entryPoint"), Is.SameAs(entryPoint));

            foreach (CombatEncounterGroup group in groups)
            {
                CombatEncounterTrigger2D trigger = triggers.Single(item =>
                    Reference(item, "encounterGroup") == group);
                Assert.That(trigger.EncounterId, Is.EqualTo(group.EncounterId));
                Assert.That(Reference(trigger, "entryPoint"), Is.SameAs(entryPoint));

                List<GameObject> members = group.GetActiveEnemies();
                Assert.That(members, Has.Count.EqualTo(1), group.EncounterId);
                Assert.That(members[0].GetComponent<CombatHpComponent>(), Is.Not.Null, group.EncounterId);
            }

            string fieldAttackSource = System.IO.File.ReadAllText(
                "Assets/GAME/Scripts/Player/Runtime/PlayerFieldAttackController.cs");
            Assert.That(fieldAttackSource, Does.Contain("encounterOwner.TryReserve(this)"));
            Assert.That(fieldAttackSource, Does.Contain("encounterOwner?.CommitReservation"));
        }

        [Test]
        [Category("CNTD106")]
        public void ProductionDungeonOne_EncounterEnemyDataAndSkillsRemainValidPlaceholderContent()
        {
            OpenProductionScene();

            CombatEntryPoint entryPoint = FindAll<CombatEntryPoint>().Single();
            SerializedProperty definitions = new SerializedObject(entryPoint).FindProperty("skillDefinitions");
            Assert.That(definitions.arraySize, Is.EqualTo(4));
            for (int index = 0; index < definitions.arraySize; index++)
                Assert.That(definitions.GetArrayElementAtIndex(index).objectReferenceValue, Is.Not.Null);

            foreach (CombatEncounterGroup group in FindAll<CombatEncounterGroup>())
            {
                GameObject enemy = group.GetActiveEnemies().Single();
                CombatHpComponent hp = enemy.GetComponent<CombatHpComponent>();
                CombatSkillLoadoutComponent loadout = enemy.GetComponent<CombatSkillLoadoutComponent>();

                Assert.That(hp.HP, Is.EqualTo(10), group.EncounterId);
                Assert.That(hp.MaxHP, Is.EqualTo(10), group.EncounterId);
                Assert.That(loadout, Is.Not.Null, group.EncounterId);
                Assert.That(loadout.SkillIds, Is.EqualTo(new[] { 11, 12 }), group.EncounterId);
            }
        }

        [Test]
        [Category("CNTD106")]
        public void ProductionDungeonOne_MapsWorldConfirmedClearToOneDistinctQuestObjectivePerEncounter()
        {
            OpenProductionScene();

            Dictionary<string, string> objectiveByEncounter = FindAll<EncounterQuestObjectivePublisher>()
                .ToDictionary(
                    publisher => ((CombatEncounterGroup)Reference(publisher, "targetEncounter")).EncounterId,
                    publisher => (string)Value(publisher, "objectiveId"));

            Assert.That(objectiveByEncounter.Keys, Is.EquivalentTo(EncounterIds));
            Assert.That(objectiveByEncounter["dungeon1.encounter.01"], Is.EqualTo("clear_encounter_01"));
            Assert.That(objectiveByEncounter["dungeon1.encounter.02"], Is.EqualTo("clear_encounter_02"));
            Assert.That(objectiveByEncounter["dungeon1.encounter.03"], Is.EqualTo("clear_encounter_03"));
            Assert.That(FindAll<CombatQuestObjectivePublisher>(), Is.Empty,
                "Dungeon 1 Production must publish progress from confirmed World clear, not CombatResult.");

            string publisherSource = System.IO.File.ReadAllText(
                "Assets/GAME/Scripts/Quest/EncounterQuestObjectivePublisher.cs");
            Assert.That(publisherSource, Does.Contain("OnEncounterCleared"));
            Assert.That(publisherSource, Does.Not.Contain("OnCombatEnded"));
        }

        [Test]
        [Category("CNTD106")]
        public void CombatRewards_UseCompletionIdentityAndRemainVictoryOnly()
        {
            CombatResult victory = new()
            {
                CompletionId = "dungeon1.encounter.01.completion",
                EndReason = CombatEndReason.Victory,
                IsWin = true,
                TotalGold = 50,
                TotalExp = 150
            };
            CombatResult defeat = new()
            {
                CompletionId = "dungeon1.encounter.01.defeat",
                EndReason = CombatEndReason.Defeat
            };

            RewardGrantRequest victoryRequest = RewardService.CreateCombatRewardRequest(victory, null);
            RewardGrantRequest defeatRequest = RewardService.CreateCombatRewardRequest(defeat, null);

            Assert.That(victoryRequest.SourceType, Is.EqualTo(RewardSourceType.Combat));
            Assert.That(victoryRequest.SourceId, Is.EqualTo(victory.CompletionId));
            Assert.That(victoryRequest.Gold, Is.EqualTo(50));
            Assert.That(victoryRequest.Exp, Is.EqualTo(150));
            Assert.That(defeatRequest.SourceId, Is.EqualTo(defeat.CompletionId));
            Assert.That(defeatRequest.Gold, Is.Zero);
            Assert.That(defeatRequest.Exp, Is.Zero);

            string binderSource = System.IO.File.ReadAllText(
                "Assets/GAME/Scripts/Combat/Runtime/UI/CombatRewardUIBinder.cs");
            Assert.That(binderSource, Does.Contain("RewardService.CreateCombatRewardRequest(result, null)"));
            Assert.That(binderSource, Does.Contain("rewardService.GrantReward(request)"));
        }

        private static void OpenProductionScene()
        {
            EditorSceneManager.OpenScene(
                DungeonOneProductionMigrationUtility.ProductionScenePath,
                OpenSceneMode.Single);
        }

        private static T[] FindAll<T>() where T : Object
        {
            return Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }

        private static Object Reference(Object owner, string propertyName)
        {
            return new SerializedObject(owner).FindProperty(propertyName).objectReferenceValue;
        }

        private static object Value(Object owner, string propertyName)
        {
            return new SerializedObject(owner).FindProperty(propertyName).stringValue;
        }
    }
}
