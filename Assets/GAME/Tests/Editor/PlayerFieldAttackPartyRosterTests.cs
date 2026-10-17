#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Core;
using Game.NonCombat.Inventory;
using Game.NonCombat.Party;
using Game.NonCombat.Progress;
using Game.Quest;
using Game.Reward;
using Game.Player;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Integration
{
    [Category("CBT_PARTY_06")]
    public sealed class PlayerFieldAttackPartyRosterTests
    {
        private readonly List<UnityEngine.Object> _created = new();
        private readonly List<string> _runtimeErrors = new();

        [SetUp]
        public void SetUp()
        {
            if (Application.isPlaying)
                return;

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            DestroyExistingOwners();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null)
                    UnityEngine.Object.DestroyImmediate(_created[i]);
            _created.Clear();
            DestroyExistingOwners();
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void PartyOptIn_UsesCharacterPartyAdapterForOneToThreeMembers(int partyCount)
        {
            Fixture fixture = CreateFixture(partyCount);

            Assert.That(Build(fixture.Attack, fixture.EnemyObjects, CombatFlowMode.StandoffClashChain,
                out CombatStartRequest request, out string message), Is.True, message);

            Assert.That(request.FlowMode, Is.EqualTo(CombatFlowMode.StandoffClashChain));
            Assert.That(request.Reason, Is.EqualTo(StartReason.PlayerFirstHit));
            Assert.That(request.InitiativeSide, Is.EqualTo(Side.Allies));
            Assert.That(request.AllyFieldObjects, Is.EqualTo(fixture.Allies.Take(partyCount)));
            for (int i = 0; i < partyCount; i++)
            {
                Assert.That(request.TryGetAllyCharacterId(fixture.Allies[i], out string characterId), Is.True);
                Assert.That(characterId, Is.EqualTo(fixture.Ids[i]));
            }
            Assert.That(request.EnemyFieldObjects, Is.EqualTo(fixture.EnemyObjects));
        }

        [Test]
        public void PartyOptIn_InvalidRoster_DoesNotFallbackToSinglePlayer()
        {
            Fixture fixture = CreateFixture(2);
            SetField(fixture.Attack, "partyCombatantBindings", new List<PartyCombatantReference>());

            Assert.That(Build(fixture.Attack, fixture.EnemyObjects, CombatFlowMode.LegacyPlanning,
                out CombatStartRequest request, out string message), Is.False);
            Assert.That(request, Is.Null);
            Assert.That(message, Does.Contain("explicit field binding"));
        }

        [Test]
        public void PartyOptIn_RejectsZeroHpAndDuplicateBindings()
        {
            Fixture fixture = CreateFixture(2);
            fixture.Allies[1].GetComponent<CombatHpComponent>().HP = 0;

            Assert.That(Build(fixture.Attack, fixture.EnemyObjects, CombatFlowMode.LegacyPlanning,
                out _, out string hpMessage), Is.False);
            Assert.That(hpMessage, Does.Contain("invalid HP"));

            fixture.Allies[1].GetComponent<CombatHpComponent>().HP = 10;
            SetField(fixture.Attack, "partyCombatantBindings", new List<PartyCombatantReference>
            {
                Binding(fixture.Ids[0], fixture.Allies[0]), Binding(fixture.Ids[0], fixture.Allies[1])
            });
            Assert.That(Build(fixture.Attack, fixture.EnemyObjects, CombatFlowMode.LegacyPlanning,
                out _, out string duplicateMessage), Is.False);
            Assert.That(duplicateMessage, Does.Contain("duplicate field bindings"));
        }

        [Test]
        public void PartyOptIn_RejectsEmptyAndOversizedLineups()
        {
            Fixture fixture = CreateFixture(1);
            ForceLineup(fixture.Party, Array.Empty<string>());
            Assert.That(Build(fixture.Attack, fixture.EnemyObjects, CombatFlowMode.LegacyPlanning,
                out _, out string emptyMessage), Is.False);
            Assert.That(emptyMessage, Does.Contain("1 to 3"));

            ForceLineup(fixture.Party, fixture.Ids);
            Assert.That(Build(fixture.Attack, fixture.EnemyObjects, CombatFlowMode.LegacyPlanning,
                out _, out string oversizedMessage), Is.False);
            Assert.That(oversizedMessage, Does.Contain("1 to 3"));
        }

        [Test]
        public void DefaultSinglePlayerPath_RemainsAvailableWithoutPartyOptIn()
        {
            GameObject player = Track(new GameObject("Player"));
            PlayerFieldAttackController attack = player.AddComponent<PlayerFieldAttackController>();
            GameObject enemy = Actor("Enemy");

            Assert.That(Build(attack, new[] { enemy }, CombatFlowMode.LegacyPlanning,
                out CombatStartRequest request, out string message), Is.True, message);
            Assert.That(request.AllyFieldObjects, Is.EqualTo(new[] { player }));
            Assert.That(request.EnemyFieldObjects, Is.EqualTo(new[] { enemy }));
        }

        [Test]
        public void FieldAttackCollision_StartsOnePartySessionAndReservesEncounter()
        {
            GameStateMachine state = Track(new GameObject("State")).AddComponent<GameStateMachine>();
            Invoke(state, "Awake");
            GameFlowController flow = Track(new GameObject("Flow")).AddComponent<GameFlowController>();
            Invoke(flow, "Awake");
            CombatEntryPoint entry = Track(new GameObject("Entry")).AddComponent<CombatEntryPoint>();
            Invoke(entry, "Awake");
            Fixture fixture = CreateFixture(3, entry);
            CombatEncounterGroup group = Track(new GameObject("Encounter")).AddComponent<CombatEncounterGroup>();
            GameObject enemy = fixture.EnemyObjects.Single();
            enemy.transform.SetParent(group.transform);
            enemy.layer = 9;
            enemy.AddComponent<BoxCollider2D>();
            fixture.Attack.transform.position = Vector3.zero;
            enemy.transform.position = new Vector3(0.5f, 0f);
            SetField(fixture.Attack, "attackOrigin", fixture.Attack.transform);
            SetField(fixture.Attack, "targetMask", (LayerMask)(1 << enemy.layer));
            SetField(fixture.Attack, "hitBoxSize", new Vector2(2f, 2f));
            Physics2D.SyncTransforms();
            Assert.That(Physics2D.OverlapBoxAll(fixture.Attack.transform.position, new Vector2(2f, 2f), 0f, 1 << enemy.layer),
                Does.Contain(enemy.GetComponent<Collider2D>()));

            Invoke(fixture.Attack, "TryResolveHit", 0);

            Assert.That(entry.ActiveSession, Is.Not.Null);
            Assert.That(entry.ActiveSession.Allies, Has.Count.EqualTo(3));
            CombatResult result = CombatResultBuilder.Build(entry.ActiveSession, CombatEndReason.Victory);
            Assert.That(result.AllyParticipants.Select(item => item.CharacterId), Is.EqualTo(fixture.Ids.Take(3)));
            Assert.That(Invoke<bool>(group, "TryReserve", fixture.Attack), Is.False,
                "A field attack that committed its encounter must block a duplicate field/contact reservation.");
        }

        [Test]
        public void InvalidPartyRequest_ReleasesEncounterReservation()
        {
            GameStateMachine state = Track(new GameObject("State")).AddComponent<GameStateMachine>();
            Invoke(state, "Awake");
            GameFlowController flow = Track(new GameObject("Flow")).AddComponent<GameFlowController>();
            Invoke(flow, "Awake");
            CombatEntryPoint entry = Track(new GameObject("Entry")).AddComponent<CombatEntryPoint>();
            Invoke(entry, "Awake");
            Fixture fixture = CreateFixture(2, entry);
            CombatEncounterGroup group = Track(new GameObject("Encounter")).AddComponent<CombatEncounterGroup>();
            GameObject enemy = fixture.EnemyObjects.Single();
            enemy.transform.SetParent(group.transform);
            enemy.layer = 10;
            enemy.AddComponent<BoxCollider2D>();
            enemy.transform.position = new Vector3(0.5f, 0f);
            SetField(fixture.Attack, "targetMask", (LayerMask)(1 << enemy.layer));
            SetField(fixture.Attack, "hitBoxSize", new Vector2(2f, 2f));
            SetField(fixture.Attack, "partyCombatantBindings", new List<PartyCombatantReference>());
            Physics2D.SyncTransforms();
            Assert.That(Physics2D.OverlapBoxAll(fixture.Attack.transform.position, new Vector2(2f, 2f), 0f, 1 << enemy.layer),
                Does.Contain(enemy.GetComponent<Collider2D>()));
            Assert.That(Build(fixture.Attack, fixture.EnemyObjects, CombatFlowMode.LegacyPlanning,
                out _, out string message), Is.False);
            Assert.That(message, Does.Contain("explicit field binding"));

            Invoke(fixture.Attack, "TryResolveHit", 0);

            Assert.That(entry.ActiveSession, Is.Null);
            Assert.That(Invoke<bool>(group, "TryReserve", fixture.Attack), Is.True,
                "A rejected Party request must release the Field Attack reservation.");
            Invoke(group, "ReleaseReservation", fixture.Attack);
        }

        [UnityTest]
        [Category("CBT_PARTY_06V")]
        public IEnumerator FieldAttackPartyRoster_EntersFinalExchangeAndSettlesThreeParticipantReward_InActualPlayMode()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying, Is.True);

            _runtimeErrors.Clear();
            Application.logMessageReceived += CaptureRuntimeError;
            try
            {
                Assert.That(GameStateMachine.Instance, Is.Not.Null);
                Assert.That(GameFlowController.Instance, Is.Not.Null);
                if (PartyRuntime.Instance != null)
                    UnityEngine.Object.DestroyImmediate(PartyRuntime.Instance.gameObject);
                if (CharacterProgressionService.Instance != null)
                    UnityEngine.Object.DestroyImmediate(CharacterProgressionService.Instance.gameObject);

                PlayModeFixture fixture = CreatePlayModeFixture();
                CurrencyWallet wallet = CurrencyWallet.Instance;
                InventoryService inventory = InventoryService.Instance;
                RewardService rewards = RewardService.Instance;
                Assert.That(wallet, Is.Not.Null);
                Assert.That(inventory, Is.Not.Null);
                Assert.That(rewards, Is.Not.Null);
                wallet.SetGold(0);
                inventory.ResetForNewGame();
                rewards.ResetForNewGame();
                SetField(inventory, "itemCatalog", null);
                SetField(rewards, "currencyWallet", wallet);
                SetField(rewards, "inventoryService", inventory);
                SetField(rewards, "characterProgressionService", fixture.Progression);

                QuestRuntime quests = Track(new GameObject("CBT_PARTY_06V.Quests")).AddComponent<QuestRuntime>();
                QuestDefinitionSO quest = CreateKillQuest("party06v.quest", "party06v.kill");
                quests.StartQuest(quest);
                QuestObjectiveTracker tracker = Track(new GameObject("CBT_PARTY_06V.Tracker"))
                    .AddComponent<QuestObjectiveTracker>();
                SetField(tracker, "questRuntime", quests);
                CreateQuestPublisher(fixture.Entry, fixture.Encounter, quest.QuestId, quest.Objectives[0].ObjectiveId);

                CombatResult published = null;
                fixture.Entry.OnCombatEnded += result =>
                {
                    published = result;
                    rewards.GrantReward(PartyRewardDiskSaveTests.WithTestItem(result));
                };

                fixture.Attack.RequestPrimaryAttack();
                float deadline = Time.realtimeSinceStartup + 5f;
                while (fixture.Entry.ActiveSession == null && Time.realtimeSinceStartup < deadline)
                    yield return null;

                CombatSession session = fixture.Entry.ActiveSession;
                Assert.That(session, Is.Not.Null, "The public field-attack coroutine did not start combat.");
                Assert.That(session.FlowMode, Is.EqualTo(CombatFlowMode.StandoffClashChain));
                Assert.That(session.StartReason, Is.EqualTo(StartReason.PlayerFirstHit));
                Assert.That(session.InitiativeSide, Is.EqualTo(Side.Allies));
                Assert.That(session.OpeningEffectApplied, Is.True);
                Assert.That(session.Allies, Has.Count.EqualTo(3));
                Assert.That(session.Enemies, Has.Count.EqualTo(1));
                Assert.That(fixture.Encounter.GetActiveEnemies(), Is.EqualTo(new[] { fixture.Enemy }));
                Assert.That(fixture.Encounter.TryReserve(fixture.Contact), Is.False,
                    "A Contact request must not reserve an encounter already committed by Field Attack.");
                for (int i = 0; i < 3; i++)
                {
                    FieldCombatantAdapter combatant = session.Allies[i] as FieldCombatantAdapter;
                    Assert.That(combatant, Is.Not.Null);
                    Assert.That(combatant.FieldObject, Is.SameAs(fixture.Allies[i]));
                    Assert.That(session.TryGetAllyCharacterId(combatant, out string characterId), Is.True);
                    Assert.That(characterId, Is.EqualTo(fixture.CharacterIds[i]));
                }

                fixture.Attack.RequestPrimaryAttack();
                yield return null;
                Assert.That(fixture.Entry.ActiveSession, Is.SameAs(session),
                    "A second Field Attack must not create another combat session.");

                session.GetCombatState(session.Allies[1]).ApplyDamage(session.Allies[1].HP);
                session.GetCombatState(session.Enemies[0]).ApplyDamage(session.Enemies[0].HP);
                fixture.Entry.ActiveStateMachine.ForceExit(CombatEndReason.Victory);
                while (published == null && Time.realtimeSinceStartup < deadline)
                    yield return null;

                Assert.That(published, Is.Not.Null, "FinalExchange victory was not published before the fixture deadline.");
                Assert.That(published.EndReason, Is.EqualTo(CombatEndReason.Victory));
                Assert.That(published.AllyParticipants.Select(item => item.CharacterId),
                    Is.EqualTo(fixture.CharacterIds.Take(3)));
                Assert.That(published.AllyParticipants[1].IsAlive, Is.False);
                for (int i = 0; i < fixture.CharacterIds.Length; i++)
                {
                    Assert.That(fixture.Progression.TryGetState(fixture.CharacterIds[i], out _, out int exp), Is.True);
                    Assert.That(exp, Is.EqualTo(i < 3 ? 150 : 0), fixture.CharacterIds[i]);
                }
                Assert.That(wallet.Gold, Is.EqualTo(50));
                Assert.That(quests.GetObjectiveProgress(quest.QuestId, quest.Objectives[0].ObjectiveId), Is.EqualTo(1));
                Assert.That(fixture.Entry.ActiveSession, Is.Null);
                Assert.That(fixture.Entry.ActiveStateMachine, Is.Null);

                Invoke(fixture.Entry, "RaiseCombatEnded", published);
                Assert.That(wallet.Gold, Is.EqualTo(50));
                Assert.That(quests.GetObjectiveProgress(quest.QuestId, quest.Objectives[0].ObjectiveId), Is.EqualTo(1));
                Assert.That(_runtimeErrors, Is.Empty, string.Join("\n", _runtimeErrors));
            }
            finally
            {
                Application.logMessageReceived -= CaptureRuntimeError;
            }

            yield return new ExitPlayMode();
            Assert.That(Application.isPlaying, Is.False);
        }

        private Fixture CreateFixture(int partyCount, CombatEntryPoint entry = null)
        {
            string[] ids = { "field.leader", "field.ally.one", "field.ally.two", "field.reserve" };
            PartyRuntime party = Track(new GameObject("Party")).AddComponent<PartyRuntime>();
            CharacterProgressionService progression = Track(new GameObject("Progression")).AddComponent<CharacterProgressionService>();
            List<CharacterProgressionDefinitionSO> definitions = new();
            List<GameObject> allies = new();
            List<PartyCombatantReference> bindings = new();
            for (int i = 0; i < ids.Length; i++)
            {
                CharacterProgressionDefinitionSO definition = Track(ScriptableObject.CreateInstance<CharacterProgressionDefinitionSO>());
                SetField(definition, "characterId", ids[i]);
                SetField(definition, "startingLevel", 1);
                SetField(definition, "maximumLevel", 3);
                SetField(definition, "experienceRequiredByLevel", new[] { 1000, 1000 });
                definitions.Add(definition);
                party.AddMember(ids[i]);
                if (i < partyCount) party.SelectForCombat(ids[i], true);
                GameObject ally = i == 0 ? Track(new GameObject("Player")) : Actor("Ally." + i);
                if (i == 0) ally.AddComponent<CombatHpComponent>();
                allies.Add(ally);
                bindings.Add(Binding(ids[i], ally));
            }
            ConfigureProgression(progression, ids[0], definitions.ToArray());
            PlayerFieldAttackController attack = allies[0].AddComponent<PlayerFieldAttackController>();
            SetField(attack, "entryPoint", entry);
            SetField(attack, "usePartyCombatRoster", true);
            SetField(attack, "partyRuntime", party);
            SetField(attack, "characterProgression", progression);
            SetField(attack, "partyCombatantBindings", bindings);
            GameObject enemy = Actor("Enemy");
            return new Fixture(attack, party, ids, allies, new List<GameObject> { enemy });
        }

        private PlayModeFixture CreatePlayModeFixture()
        {
            string[] ids = { "party06v.leader", "party06v.ally.one", "party06v.ally.two", "party06v.reserve" };
            PartyRuntime party = Track(new GameObject("CBT_PARTY_06V.Party")).AddComponent<PartyRuntime>();
            CharacterProgressionService progression = Track(new GameObject("CBT_PARTY_06V.Progression"))
                .AddComponent<CharacterProgressionService>();
            CharacterProgressionDefinitionSO[] definitions = new CharacterProgressionDefinitionSO[ids.Length];
            List<GameObject> allies = new();
            List<PartyCombatantReference> bindings = new();
            for (int i = 0; i < ids.Length; i++)
            {
                definitions[i] = Track(ScriptableObject.CreateInstance<CharacterProgressionDefinitionSO>());
                SetField(definitions[i], "characterId", ids[i]);
                SetField(definitions[i], "startingLevel", 1);
                SetField(definitions[i], "maximumLevel", 5);
                SetField(definitions[i], "experienceRequiredByLevel", new[] { 1000, 1000, 1000, 1000 });
                party.AddMember(ids[i]);
                if (i < 3)
                    party.SelectForCombat(ids[i], true);
                GameObject ally = CombatActor($"CBT_PARTY_06V.Ally.{i}", 10 + i, i + 1);
                allies.Add(ally);
                bindings.Add(Binding(ids[i], ally));
            }
            ConfigureProgression(progression, ids[0], definitions);

            CombatFlowOrchestrator orchestrator = Track(new GameObject("CBT_PARTY_06V.Orchestrator"))
                .AddComponent<CombatFlowOrchestrator>();
            Game.Combat.Effects.CombatDirector director = Track(new GameObject("CBT_PARTY_06V.Director"))
                .AddComponent<Game.Combat.Effects.CombatDirector>();
            CombatEntryPoint entry = Track(new GameObject("CBT_PARTY_06V.Entry")).AddComponent<CombatEntryPoint>();
            SetField(entry, "flowOrchestrator", orchestrator);
            SetField(entry, "director", director);
            SetField(orchestrator, "entryPoint", entry);
            SetField(entry, "finalExchangeRuntimeConfig", Track(ScriptableObject.CreateInstance<FinalCombatRuntimeConfigSO>()));
            SetField(entry, "_book", CreateSkillBook());

            CombatEncounterGroup encounter = Track(new GameObject("CBT_PARTY_06V.Encounter"))
                .AddComponent<CombatEncounterGroup>();
            SetField(encounter, "autoCollectChildren", false);
            SetField(encounter, "useEncounterFlowMode", true);
            SetField(encounter, "encounterFlowMode", CombatFlowMode.StandoffClashChain);
            SetField(encounter, "encounterId", "party06v.field");
            GameObject enemy = CombatActor("CBT_PARTY_06V.Enemy", 10, 9);
            enemy.transform.SetParent(encounter.transform);
            enemy.transform.position = new Vector3(0.5f, 0f);
            enemy.layer = 9;
            enemy.AddComponent<BoxCollider2D>();
            SetField(encounter, "enemies", new List<GameObject> { enemy });

            GameObject contactObject = Track(new GameObject("CBT_PARTY_06V.Contact"));
            contactObject.AddComponent<BoxCollider2D>();
            CombatEncounterTrigger2D contact = contactObject.AddComponent<CombatEncounterTrigger2D>();
            SetField(contact, "encounterGroup", encounter);

            GameObject player = allies[0];
            player.transform.position = Vector3.zero;
            PlayerFieldAttackController attack = player.AddComponent<PlayerFieldAttackController>();
            OpeningEffectSO openingEffect = Track(ScriptableObject.CreateInstance<OpeningEffectSO>());
            openingEffect.inspirationDelta = 1;
            SetField(attack, "entryPoint", entry);
            SetField(attack, "attackOrigin", player.transform);
            SetField(attack, "hitBoxSize", new Vector2(2f, 2f));
            SetField(attack, "hitDelay", 0f);
            SetField(attack, "cooldown", 0f);
            SetField(attack, "targetMask", (LayerMask)(1 << enemy.layer));
            SetField(attack, "openingEffectOrNull", openingEffect);
            SetField(attack, "usePartyCombatRoster", true);
            SetField(attack, "partyRuntime", party);
            SetField(attack, "characterProgression", progression);
            SetField(attack, "partyCombatantBindings", bindings);
            Physics2D.SyncTransforms();

            return new PlayModeFixture(attack, entry, encounter, contact, progression, ids, allies, enemy);
        }

        private GameObject Actor(string name)
        {
            GameObject actor = Track(new GameObject(name));
            CombatHpComponent hp = actor.AddComponent<CombatHpComponent>();
            hp.MaxHP = 10;
            hp.HP = 10;
            return actor;
        }

        private GameObject CombatActor(string name, int hp, int skillId)
        {
            GameObject actor = Actor(name);
            actor.GetComponent<CombatHpComponent>().MaxHP = hp;
            actor.GetComponent<CombatHpComponent>().HP = hp;
            CombatSkillLoadoutComponent loadout = actor.AddComponent<CombatSkillLoadoutComponent>();
            SetField(loadout, "skillIds", new[] { skillId });
            return actor;
        }

        private static SkillBook CreateSkillBook()
        {
            SkillBook book = new();
            book.Register(new TestSkill(1));
            book.Register(new TestSkill(2));
            book.Register(new TestSkill(3));
            book.Register(new TestSkill(9));
            return book;
        }

        private QuestDefinitionSO CreateKillQuest(string questId, string objectiveId)
        {
            QuestObjectiveDefinition objective = new();
            SetField(objective, "objectiveId", objectiveId);
            SetField(objective, "eventType", QuestEventType.Kill);
            SetField(objective, "requiredCount", 2);
            QuestDefinitionSO definition = Track(ScriptableObject.CreateInstance<QuestDefinitionSO>());
            SetField(definition, "questId", questId);
            SetField(definition, "objectives", new[] { objective });
            return definition;
        }

        private void CreateQuestPublisher(CombatEntryPoint entry, CombatEncounterGroup encounter, string questId, string objectiveId)
        {
            GameObject owner = Track(new GameObject("CBT_PARTY_06V.QuestPublisher"));
            owner.SetActive(false);
            CombatQuestObjectivePublisher publisher = owner.AddComponent<CombatQuestObjectivePublisher>();
            SetField(publisher, "combatEntryPoint", entry);
            SetField(publisher, "targetEncounter", encounter);
            SetField(publisher, "questId", questId);
            SetField(publisher, "objectiveId", objectiveId);
            owner.SetActive(true);
        }

        private PartyCombatantReference Binding(string id, GameObject fieldObject)
        {
            PartyCombatantReference reference = new();
            SetField(reference, "characterId", id);
            SetField(reference, "fieldObject", fieldObject);
            return reference;
        }

        private static bool Build(PlayerFieldAttackController attack, IReadOnlyList<GameObject> enemies,
            CombatFlowMode flowMode, out CombatStartRequest request, out string message)
        {
            object[] args = { enemies, flowMode, null, null };
            bool result = (bool)typeof(PlayerFieldAttackController)
                .GetMethod("TryCreateAttackRequest", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(attack, args);
            request = args[2] as CombatStartRequest;
            message = args[3] as string;
            return result;
        }

        private static void ConfigureProgression(CharacterProgressionService progression, string leader,
            CharacterProgressionDefinitionSO[] definitions) =>
            typeof(CharacterProgressionService).GetMethod("ConfigureForTests", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(progression, new object[] { leader, definitions });

        private static void ForceLineup(PartyRuntime party, IReadOnlyList<string> ids)
        {
            List<string> lineup = (List<string>)typeof(PartyRuntime)
                .GetField("_combatLineup", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(party);
            lineup.Clear();
            for (int i = 0; i < ids.Count; i++) lineup.Add(ids[i]);
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Invoke(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        private static T Invoke<T>(object target, string method, params object[] args) =>
            (T)target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        private T Track<T>(T value) where T : UnityEngine.Object { _created.Add(value); return value; }

        private void CaptureRuntimeError(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _runtimeErrors.Add($"{type}: {condition}\n{stackTrace}");
        }

        private static void DestroyExistingOwners()
        {
            foreach (Type type in new[] { typeof(GameStateMachine), typeof(GameFlowController), typeof(CombatEntryPoint),
                         typeof(PartyRuntime), typeof(CharacterProgressionService) })
                foreach (Component owner in UnityEngine.Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None)
                             .OfType<Component>().ToArray())
                    UnityEngine.Object.DestroyImmediate(owner.gameObject);
        }

        private sealed class Fixture
        {
            public Fixture(PlayerFieldAttackController attack, PartyRuntime party, string[] ids, List<GameObject> allies, List<GameObject> enemies)
            { Attack = attack; Party = party; Ids = ids; Allies = allies; EnemyObjects = enemies; }
            public PlayerFieldAttackController Attack { get; }
            public PartyRuntime Party { get; }
            public string[] Ids { get; }
            public List<GameObject> Allies { get; }
            public List<GameObject> EnemyObjects { get; }
        }

        private sealed class PlayModeFixture
        {
            public PlayModeFixture(PlayerFieldAttackController attack, CombatEntryPoint entry, CombatEncounterGroup encounter,
                CombatEncounterTrigger2D contact, CharacterProgressionService progression, string[] characterIds,
                List<GameObject> allies, GameObject enemy)
            {
                Attack = attack;
                Entry = entry;
                Encounter = encounter;
                Contact = contact;
                Progression = progression;
                CharacterIds = characterIds;
                Allies = allies;
                Enemy = enemy;
            }

            public PlayerFieldAttackController Attack { get; }
            public CombatEntryPoint Entry { get; }
            public CombatEncounterGroup Encounter { get; }
            public CombatEncounterTrigger2D Contact { get; }
            public CharacterProgressionService Progression { get; }
            public string[] CharacterIds { get; }
            public List<GameObject> Allies { get; }
            public GameObject Enemy { get; }
        }

        private sealed class TestSkill : ISkill, ICombatMpCostProvider
        {
            public TestSkill(int id) => Id = new SkillId(id);
            public SkillId Id { get; }
            public string Name => $"CBT Party 06V Skill {Id.Value}";
            public int InspirationCost => 0;
            public KeywordMask Keywords => KeywordMask.None;
            public SkillTag Tag => SkillTag.Attack;
            public TargetingRule Targeting => TargetingRule.SingleEnemy;
            public SkillMovementMode MovementMode => SkillMovementMode.None;
            public float DesiredTargetDistance => 0f;
            public float MoveSpeed => 0f;
            public float ActionDelayAfterMove => 0f;
            public int BaseDamage => 10;
            public int BaseStagger => 0;
            public int WeaknessStaggerBonus => 0;
            public int Speed => 1;
            public bool ConsumesTurn => true;
            public int MpCost => 1;
        }
    }
}
#endif
