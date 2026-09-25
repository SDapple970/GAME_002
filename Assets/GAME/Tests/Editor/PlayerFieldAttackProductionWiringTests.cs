#if UNITY_INCLUDE_TESTS
using System.IO;
using System.Linq;
using Game.Combat.Core;
using Game.Combat.Integration;
using Game.EditorTools;
using Game.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests.Integration
{
    public sealed class PlayerFieldAttackProductionWiringTests
    {
        [TearDown]
        public void TearDown()
        {
            if (!Application.isPlaying)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        [Category("CNTD102")]
        public void ProductionPlayer_UsesItsRootAsAttackOriginAndOnlyTargetsEnemyLayer()
        {
            EditorSceneManager.OpenScene(
                DungeonOneProductionMigrationUtility.ProductionScenePath,
                OpenSceneMode.Single);

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            Assert.That(enemyLayer, Is.EqualTo(7));

            PlayerFieldAttackController attack = Object
                .FindObjectsByType<PlayerFieldAttackController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single();
            SerializedObject serializedAttack = new(attack);
            Transform attackOrigin = serializedAttack.FindProperty("attackOrigin").objectReferenceValue as Transform;
            int targetMask = serializedAttack.FindProperty("targetMask").intValue;

            Assert.That(attackOrigin, Is.SameAs(attack.transform));
            Assert.That(targetMask, Is.EqualTo(1 << enemyLayer));
            Assert.That((targetMask & (1 << LayerMask.NameToLayer("Default"))), Is.Zero);
        }

        [Test]
        [Category("CNTD102")]
        public void ProductionEnemies_AreDetectableAndShareTheCanonicalCombatEntryPoint()
        {
            EditorSceneManager.OpenScene(
                DungeonOneProductionMigrationUtility.ProductionScenePath,
                OpenSceneMode.Single);

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            CombatEntryPoint entryPoint = Object
                .FindObjectsByType<CombatEntryPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single();
            PlayerFieldAttackController attack = Object
                .FindObjectsByType<PlayerFieldAttackController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single();

            Assert.That(new SerializedObject(attack).FindProperty("entryPoint").objectReferenceValue,
                Is.SameAs(entryPoint));

            foreach (string enemyName in new[] { "Enemy_01", "Enemy_02", "Enemy_03" })
            {
                GameObject enemy = GameObject.Find(enemyName);
                Assert.That(enemy, Is.Not.Null, enemyName);
                Assert.That(enemy.layer, Is.EqualTo(enemyLayer), enemyName);
                Assert.That(enemy.GetComponent<Collider2D>(), Is.Not.Null, enemyName);

                CombatEncounterGroup group = enemy.GetComponentInParent<CombatEncounterGroup>();
                Assert.That(group, Is.Not.Null, enemyName);
                Assert.That(group.GetActiveEnemies(), Does.Contain(enemy), enemyName);

                CombatEncounterTrigger2D trigger = group.GetComponentInChildren<CombatEncounterTrigger2D>(true);
                Assert.That(trigger, Is.Not.Null, enemyName);
                Assert.That(new SerializedObject(trigger).FindProperty("entryPoint").objectReferenceValue,
                    Is.SameAs(entryPoint), enemyName);
            }
        }

        [Test]
        [Category("CNTD102")]
        public void ProductionEncounterReservations_BlockConcurrentFieldAttackAndContactStarts()
        {
            EditorSceneManager.OpenScene(
                DungeonOneProductionMigrationUtility.ProductionScenePath,
                OpenSceneMode.Single);

            PlayerFieldAttackController attack = Object
                .FindObjectsByType<PlayerFieldAttackController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single();

            foreach (CombatEncounterGroup group in Object.FindObjectsByType<CombatEncounterGroup>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                CombatEncounterTrigger2D trigger = group.GetComponentInChildren<CombatEncounterTrigger2D>(true);
                Assert.That(trigger, Is.Not.Null, group.EncounterId);

                Assert.That(group.TryReserve(attack), Is.True, group.EncounterId);
                Assert.That(group.TryReserve(trigger), Is.False,
                    "A same-frame contact request must not start a second combat session.");
                group.ReleaseReservation(attack);

                Assert.That(group.TryReserve(trigger), Is.True, group.EncounterId);
                Assert.That(group.TryReserve(attack), Is.False,
                    "A repeated field attack must not start a second combat session.");
                group.ReleaseReservation(trigger);
            }
        }

        [Test]
        [Category("CNTD102")]
        public void FieldAttack_UsesTheExistingEncounterReservationAndCombatEntryPath()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Assets/GAME/Scripts/Player/Runtime/PlayerFieldAttackController.cs"));

            Assert.That(source, Does.Contain("encounterOwner.TryReserve(this)"));
            Assert.That(source, Does.Contain("entryPoint.StartCombat(request)"));
            Assert.That(source, Does.Contain("encounterOwner?.CommitReservation"));
            Assert.That(source, Does.Contain("encounterOwner?.ReleaseReservation(this)"));
        }

    }
}
#endif
