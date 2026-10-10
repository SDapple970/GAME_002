using System;
using System.Collections.Generic;
using System.Linq;
using Game.Combat.Data;
using Game.Combat.Core;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.EditorTools;
using Game.Enemies;
using Game.NonCombat.Progress;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.Integration
{
    public sealed class SharedFilmOwnershipTests : FilmUniqueTestFixture
    {
        [Test]
        public void SharedOwnership_AllowsIndependentSimultaneousEquipWithoutConsumptionOrAutoEquip()
        {
            CharacterSkillRuntime runtime = Participant.Runtime;
            Assert.That(runtime.TryAcquireSharedFilm(" film.a ").Changed, Is.True);
            Assert.That(runtime.TryAcquireSharedFilm("film.a").Status, Is.EqualTo(CharacterSkillAcquireStatus.AlreadyOwned));
            Assert.That(runtime.GetEquippedSkills("hero.a"), Is.Empty);
            Assert.That(runtime.TryEquip("hero.a", "film.a").Changed, Is.True);
            Assert.That(runtime.TryEquip("hero.b", "film.a").Changed, Is.True);
            Assert.That(runtime.TryEquip("hero.a", "film.a").Status, Is.EqualTo(CharacterSkillEquipStatus.AlreadyEquipped));
            runtime.TryUnequip("hero.a", "film.a");
            Assert.That(runtime.IsEquipped("hero.b", "film.a"), Is.True);
            Assert.That(runtime.GetSharedFilms(), Is.EqualTo(new[] { "film.a" }));
            Assert.That(runtime.GetAcquiredSkills("hero.a"), Is.Empty);
        }

        [Test]
        public void InvalidKeysAndLegacyAcquisitionRemainSafeAndSnapshotsAreReadOnly()
        {
            CharacterSkillRuntime runtime = Participant.Runtime;
            Assert.That(runtime.TryAcquireSharedFilm(" ").Status, Is.EqualTo(CharacterSkillAcquireStatus.InvalidPersistentSkillKey));
            Assert.That(runtime.TryEquip("hero.a", "film.missing").Status, Is.EqualTo(CharacterSkillEquipStatus.NotAcquired));
            Assert.That(runtime.TryEquip(" ", "film.a").Status, Is.EqualTo(CharacterSkillEquipStatus.InvalidCharacterId));
            runtime.TryAcquire("hero.a", "legacy.a");
            Assert.That(runtime.TryEquip("hero.a", "legacy.a").Changed, Is.True);
            Assert.That(runtime.TryEquip("hero.b", "legacy.a").Status, Is.EqualTo(CharacterSkillEquipStatus.NotAcquired));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)runtime.GetSharedFilms()).Add("mutated"));
        }

        [Test]
        public void KnownFilmLegacyAcquireImmediatelyPromotesWithoutReclassifyingUnknownOrUniqueKeys()
        {
            SkillDefinitionSO film = Skill("film.known", 11, SkillOwnershipCategory.Film);
            SkillDefinitionSO unique = Skill("unique.known", 21, SkillOwnershipCategory.Unique, "hero.a");
            new FilmEquipApplication(Participant.Runtime, Party, new[] { film, unique }, State, Entry);
            Participant.Runtime.TryAcquire("hero.a", film.PersistentKey);
            Participant.Runtime.TryAcquire("hero.a", unique.PersistentKey);
            Participant.Runtime.TryAcquire("hero.a", "unknown");
            Assert.That(Participant.Runtime.GetSharedFilms(), Is.EqualTo(new[] { film.PersistentKey }));
            Assert.That(Participant.Runtime.TryEquip("hero.b", film.PersistentKey).Changed, Is.True);
            Assert.That(Participant.Runtime.HasAcquired("hero.a", film.PersistentKey), Is.True);
        }

        [Test]
        public void MultiPartyVictory_AcquiresFilmOnceWithoutRecipientAndExcludesUniqueMapping()
        {
            SkillDefinitionSO film = Skill("film.test", 11, SkillOwnershipCategory.Film);
            SkillDefinitionSO unique = Skill("unique.test", 12, SkillOwnershipCategory.Unique, "hero.a");
            Register(film, unique);
            var request = Request("hero.a", "hero.b");
            request.SetEnemySourceSnapshot(request.EnemyFieldObjects[0],
                new EnemySourceSnapshot("enemy.test", new[] { film.PersistentKey, film.PersistentKey, unique.PersistentKey, "unknown" }));
            Assert.That(Entry.StartCombat(request), Is.True);
            Entry.ActiveSession.Enemies.Single().ApplyDamage(10);
            CombatResult result = CombatResultBuilder.Build(Entry.ActiveSession, CombatEndReason.Victory);
            Assert.That(result.SkillAcquisitionRecipientCharacterId, Is.Null);
            Assert.That(result.IsPartySkillAcquisitionEligible, Is.True);
            var acquired = EnemySkillAcquisitionIntegration.ProcessCompletion(result, new[] { film, unique }, null);
            Assert.That(acquired.AcquiredCount, Is.EqualTo(1));
            Assert.That(Participant.Runtime.GetSharedFilms(), Is.EqualTo(new[] { film.PersistentKey }));
            Assert.That(Participant.Runtime.GetAcquiredSkills("hero.a"), Is.Empty);
            Assert.That(Participant.Runtime.GetEquippedSkills("hero.a"), Is.Empty);
            Assert.That(EnemySkillAcquisitionIntegration.ProcessCompletion(result, new[] { film, unique }, null).AcquiredCount, Is.Zero);
        }

        [Test]
        public void NonPartyVictory_CannotGrantFilmEvenWithSyntacticallyValidRecipient()
        {
            SkillDefinitionSO film = Skill("film.test", 11, SkillOwnershipCategory.Film);
            Register(film);
            var request = Request("not.owned");
            request.SetEnemySourceSnapshot(request.EnemyFieldObjects[0], new EnemySourceSnapshot("enemy.test", new[] { film.PersistentKey }));
            Assert.That(Entry.StartCombat(request), Is.True);
            Entry.ActiveSession.Enemies.Single().ApplyDamage(10);
            CombatResult result = CombatResultBuilder.Build(Entry.ActiveSession, CombatEndReason.Victory);
            Assert.That(result.IsPartySkillAcquisitionEligible, Is.False);
            Assert.That(EnemySkillAcquisitionIntegration.ProcessCompletion(result, new[] { film }, null).AcquiredCount, Is.Zero);
            Assert.That(Participant.Runtime.GetSharedFilms(), Is.Empty);
        }

        [Test]
        public void EnemyValidation_RejectsUniqueMappingAndExistingAssetsDefaultToCompatibility()
        {
            SkillDefinitionSO unique = Skill("unique.test", 11, SkillOwnershipCategory.Unique, "hero.a");
            EnemyDefinitionSO enemy = Asset<EnemyDefinitionSO>();
            SerializedObject data = new(enemy);
            data.FindProperty("persistentKey").stringValue = "enemy.test";
            data.FindProperty("acquirableSkillPersistentKeys").arraySize = 1;
            data.FindProperty("acquirableSkillPersistentKeys").GetArrayElementAtIndex(0).stringValue = unique.PersistentKey;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(EnemySkillAcquisitionValidator.CollectIssues(new[] { enemy }, new[] { unique }), Has.Some.Contains("Unique"));
            Assert.That(AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>("Assets/GAME/Data/Skill/Angel_Skill.asset").OwnershipCategory,
                Is.EqualTo(SkillOwnershipCategory.Compatibility));
        }
    }
}
