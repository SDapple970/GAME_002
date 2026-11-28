#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Combat.Actions;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Effects;
using Game.Combat.Model;
using Game.Combat.UI;
using Game.EditorTools;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace Game.Tests.UI
{
    public sealed class CombatReferenceUILayoutTests
    {
        private static readonly List<string> PointerFailures = new();
        [Test]
        public void SavedPrefab_RetainsSingleOwnersAndOriginalButtonBindings()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProductionCombatUILayoutSetup.PrefabPath);
            Assert.DoesNotThrow(() => ProductionCombatUILayoutSetup.Validate(prefab));
            FinalCombatUIBinder binder = prefab.GetComponentInChildren<FinalCombatUIBinder>(true);
            Image rowSource = binder.transform.Find("ItemSelectionOverlay/ItemRowSource").GetComponent<Image>();
            Assert.That(AssetDatabase.GetAssetPath(rowSource.sprite),
                Is.EqualTo("Assets/GAME/Image/ui/ui/CombatItemRowBackground.png"));
            Assert.That(Get<Button>(binder, "confirmButton").name, Is.EqualTo("ConfirmButton"));
            Assert.That(Get<Button>(binder, "cancelButton").name, Is.EqualTo("CancelButton"));
            Assert.That(AssetDatabase.GetAssetPath(Get<Button>(binder, "optionButtonPrefab")),
                Is.EqualTo("Assets/GAME/Prefabs/UI/Combat/CombatChoiceButtonPrefab.prefab"));
            foreach (string field in new[] { "actorListRoot", "skillListRoot", "targetListRoot", "handoffListRoot", "itemListRoot" })
            {
                GameObject content = Get<GameObject>(binder, field);
                ScrollRect scroll = content.GetComponentInParent<ScrollRect>(true);
                Assert.That(scroll.content, Is.EqualTo(content.transform), field);
                Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null, field);
            }
            Assert.That(binder.GetComponentInParent<CanvasScaler>(true).screenMatchMode,
                Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
            Assert.That(Get<Text>(binder, "playerHpText").transform.parent.name, Is.EqualTo("PlayerStatus"));
            Assert.That(Get<Text>(binder, "enemyHpText").transform.parent.name, Is.EqualTo("EnemyStatus"));
            Assert.That(Get<Text>(binder, "phaseText").transform.parent.name, Is.EqualTo("TurnStatus"));
            Assert.That(Get<TMP_Text>(binder, "contextText").transform.parent,
                Is.EqualTo(Get<TMP_Text>(binder, "combatDetailsText").transform.parent));
            string before = File.ReadAllText(ProductionCombatUILayoutSetup.PrefabPath);
            ProductionCombatUILayoutSetup.Install();
            ProductionCombatReferenceDesign.Install();
            Assert.That(File.ReadAllText(ProductionCombatUILayoutSetup.PrefabPath), Is.EqualTo(before));
        }

        [Test]
        public void ProductionScene_InheritsLayoutThroughExistingOwners()
        {
            string path = "Assets/GAME/Scenes/Dungeon_1_Production.unity";
            string before = File.ReadAllText(path);
            try
            {
                UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(path);
                GameObject[] roots = scene.GetRootGameObjects();
                FinalCombatUIBinder binder = roots.SelectMany(r => r.GetComponentsInChildren<FinalCombatUIBinder>(true)).Single();
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<CombatUIRootController>(true)).Count(), Is.EqualTo(1));
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<Game.UI.UIScreenRouter>(true)).Count(), Is.EqualTo(1));
                foreach (string field in new[] { "playerHpText", "enemyHpText", "phaseText", "actMenuButton", "itemsMenuButton" })
                {
                    Component component = Get<Component>(binder, field);
                    Assert.That(component.transform.IsChildOf(binder.transform), Is.True, field);
                    Assert.That(EditorUtility.IsPersistent(component), Is.False, field);
                }
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
            Assert.That(File.ReadAllText(path), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator ActualPlayMode_MenuCardsItemsAndResolutionRendering()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CombatReferenceGameViewEvidence.Configure();
            yield return new EnterPlayMode();
            yield return RunScenario();
            yield return new ExitPlayMode();
        }

        private static IEnumerator RunScenario()
        {
            PointerFailures.Clear();
            List<string> errors = new();
            Application.LogCallback capture = (message, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message);
            };
            Application.logMessageReceived += capture;
            GameObject canvasObject = new("ReferenceCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (EventSystem.current == null) canvasObject.AddComponent<EventSystem>();
            GameObject cameraObject = new("ReferenceCamera", typeof(Camera));
            GameObject driverObject = new("ReferenceTestDriver");
            List<SkillDefinitionSO> definitions = new();
            try
            {
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.18f, 0.22f, 0.24f);
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1990, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                FinalCombatUIBinder source = AssetDatabase.LoadAssetAtPath<GameObject>(ProductionCombatUILayoutSetup.PrefabPath)
                    .GetComponentInChildren<FinalCombatUIBinder>(true);
                FinalCombatUIBinder binder = Object.Instantiate(source, canvasObject.transform);
                binder.gameObject.SetActive(true);
                Sprite authoredRow = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GAME/Image/ui/ui/CombatItemRowBackground.png");
                SerializedObject artFields = new(binder);
                SerializedProperty skillArt = artFields.FindProperty("skillArtwork");
                skillArt.arraySize = 1;
                skillArt.GetArrayElementAtIndex(0).FindPropertyRelative("skillId").intValue = 900;
                skillArt.GetArrayElementAtIndex(0).FindPropertyRelative("artwork").objectReferenceValue = authoredRow;
                SerializedProperty itemArt = artFields.FindProperty("itemArtwork");
                itemArt.arraySize = 1;
                itemArt.GetArrayElementAtIndex(0).FindPropertyRelative("itemId").stringValue = "test.0";
                itemArt.GetArrayElementAtIndex(0).FindPropertyRelative("artwork").objectReferenceValue = authoredRow;
                artFields.ApplyModifiedPropertiesWithoutUndo();
                DummyCombatant ally = new(1, Side.Allies, 20, KeywordMask.None, 3);
                DummyCombatant enemy = new(10, Side.Enemies, 20, KeywordMask.None, 3);
                List<CombatHandEntry> entries = new();
                for (int i = 0; i < 12; i++)
                {
                    SkillDefinitionSO definition = ScriptableObject.CreateInstance<SkillDefinitionSO>();
                    definitions.Add(definition);
                    definition.skillId = 900 + i;
                    definition.displayName = $"검증 카드 {i + 1}";
                    definition.targeting = TargetingRule.SingleEnemy;
                    SerializedObject skillFields = new(definition);
                    skillFields.FindProperty("finalMpCost").intValue = i == 11 ? 50 : 2;
                    skillFields.ApplyModifiedPropertiesWithoutUndo();
                    ISkill skill = new SoSkill(definition);
                    ally.AddSkill(skill);
                    entries.Add(new CombatHandEntry(skill, CombatCardKind.Normal));
                }
                CombatSession session = new(StartReason.PlayerFirstHit, Side.Allies, new InspirationPool(10),
                    new Game.Combat.Environment.CombatEnvironment(), CombatFlowMode.StandoffClashChain,
                    new CombatRuntimeConfig(10, 10, 3, 0));
                session.Allies.Add(ally);
                session.Enemies.Add(enemy);
                session.RegisterAllyCharacter(ally, "char.internal_ui_fixture");
                CombatStateMachine machine = new(session);
                Assert.That(session.TryEnableHandValidation(ally, entries, 12, 0, 19), Is.True);
                CombatFlowOrchestrator driver = driverObject.AddComponent<CombatFlowOrchestrator>();
                CombatDirector director = driverObject.AddComponent<CombatDirector>();
                ItemSource itemSource = new();
                driver.RegisterCombatItemUseExecutor(itemSource);
                Assert.That(driver.BindFinalExchange(session, machine, new ZeroRandom(), new AuthoredCombatClashSideInputProvider(session)), Is.True);
                Assert.That(director.BindFinalExchange(driver, session), Is.True);
                binder.Bind(session, machine, driver);
                Assert.That(binder.GetComponentsInChildren<CombatUITransientPresentation>().Count(
                    effect => effect.name == "StandoffPresentation"), Is.EqualTo(1));
                RaiseCue(director, CombatFinalPresentationCueKind.Standoff, ally, null);
                Assert.That(binder.GetComponentsInChildren<CombatUITransientPresentation>().Count(
                    effect => effect.name == "StandoffPresentation"), Is.EqualTo(1), "One banner per Standoff version.");
                RaiseCue(director, CombatFinalPresentationCueKind.AttackAction, ally, enemy);
                RaiseCue(director, CombatFinalPresentationCueKind.HitReaction, ally, enemy);
                Assert.That(Get<Text>(binder, "playerHpText").transform.parent.Find("CombatStatusFlash"), Is.Not.Null);
                Assert.That(Get<Text>(binder, "enemyHpText").transform.parent.Find("CombatStatusFlash"), Is.Not.Null);
                int effects = binder.GetComponentsInChildren<CombatUITransientPresentation>().Length;
                RaiseCue(director, CombatFinalPresentationCueKind.HitReaction, ally,
                    new DummyCombatant(99, Side.Enemies, 20, KeywordMask.None, 3));
                Assert.That(binder.GetComponentsInChildren<CombatUITransientPresentation>().Length, Is.EqualTo(effects));
                Assert.That(ally.HP, Is.EqualTo(20));
                Assert.That(enemy.HP, Is.EqualTo(20));
                yield return new WaitForSecondsRealtime(0.12f);
                CapturePresentation(camera, "StandoffAndCues");
                yield return new WaitForSecondsRealtime(.43f);
                yield return CombatReferenceGameViewEvidence.Capture("Intro");
                Assert.That(binder.transform.Find("StandoffPresentation").GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                yield return new WaitForSecondsRealtime(2.5f);
                Click(Buttons(Get<GameObject>(binder, "actorListRoot")).Single(b => Label(b) == "플레이어"));
                yield return null;
                GameObject cards = Get<GameObject>(binder, "skillListRoot");
                Assert.That(Buttons(cards).Length, Is.EqualTo(12));
                Assert.That(Get<Text>(binder, "playerHpText").text, Is.EqualTo("플레이어  HP 20 / 20"));
                Assert.That(Get<Text>(binder, "enemyHpText").text, Is.EqualTo("적  HP 20 / 20"));
                Assert.That(Get<TMP_Text>(binder, "contextText").text, Does.Not.Contain("이번 합 사용"));
                Assert.That(Get<TMP_Text>(binder, "combatDetailsText").text, Does.Contain("이번 합 사용"));
                Click(Get<Button>(binder, "actMenuButton"));
                Assert.That(Get<GameObject>(binder, "actorListRoot").activeSelf, Is.True);
                Click(Get<Button>(binder, "skillMenuButton"));
                Assert.That(Get<GameObject>(binder, "actorListRoot").activeSelf, Is.False);
                Button[] cardButtons = Buttons(cards);
                int authoredIndex = System.Array.FindIndex(binder.ViewState.HandCards.ToArray(), card => card.Skill.Id.Value == 900);
                Assert.That(cardButtons[authoredIndex].transform.Find("FilmFace").GetComponent<Image>().sprite, Is.EqualTo(authoredRow));
                Assert.That(cardButtons.Where((button, index) => index != authoredIndex)
                    .All(button => button.transform.Find("FilmFace").GetComponent<Image>().sprite == null), Is.True);
                artFields.Update();
                artFields.FindProperty("skillArtwork").arraySize = 0;
                artFields.ApplyModifiedPropertiesWithoutUndo();
                typeof(FinalCombatUIBinder).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(binder, null);
                cardButtons = Buttons(cards);
                Button unavailable = cardButtons.Single(button => !button.interactable);
                Assert.That(unavailable.transform.Find("FilmFace/CostPlate/CardState").GetComponent<Text>().text,
                    Is.EqualTo("MP 부족"));
                int selectionIndex = System.Array.FindIndex(cardButtons, button => button.interactable);
                var card = binder.ViewState.HandCards[selectionIndex];
                long selectedCard = card.RuntimeId;
                Button firstCard = cardButtons[selectionIndex];
                yield return RenderAndCheck(binder, camera, new Vector2Int(1990, 1080), "SkillUnselected");
                yield return new WaitForSecondsRealtime(.55f);
                yield return CombatReferenceGameViewEvidence.Capture("SkillUnselected");
                Assert.That(firstCard.transform.Find("CardTitle").GetComponent<Text>().text,
                    Is.EqualTo(card.Skill.Name));
                Assert.That(firstCard.transform.Find("FilmFace/CostPlate/CardCost").GetComponent<Text>().text,
                    Is.EqualTo("MP 2"));
                Click(firstCard);
                Assert.That(binder.ViewState.SelectedCardRuntimeId, Is.EqualTo(selectedCard));
                Assert.That(Buttons(cards)[selectionIndex].transform.Find("FilmFace/CostPlate/CardState").GetComponent<Text>().text,
                    Is.EqualTo("선택됨"));
                Assert.That(session.CardExchangeIndex, Is.EqualTo(1));
                Assert.That(enemy.HP, Is.EqualTo(20));
                Assert.That(binder.transform.Find("CommittedFilmCardPresentation"), Is.Null,
                    "Selecting a card must not play a committed execution.");
                yield return new WaitForSecondsRealtime(0.2f);
                Assert.That(((RectTransform)Buttons(cards)[selectionIndex].transform).anchoredPosition.y,
                    Is.EqualTo(8f).Within(0.1f));
                yield return CombatReferenceGameViewEvidence.Capture("CardSelected");
                yield return null;
                Canvas.ForceUpdateCanvases();
                ScrollRect film = cards.GetComponentInParent<ScrollRect>();
                Assert.That(film.content.rect.width, Is.GreaterThan(film.viewport.rect.width));
                film.horizontalNormalizedPosition = 1;
                Canvas.ForceUpdateCanvases();
                Assert.That(film.horizontalNormalizedPosition, Is.GreaterThan(0.95f));
                film.horizontalNormalizedPosition = 0;

                bool itemArtChecked = false;
                foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900),
                             new Vector2Int(1920, 1080), new Vector2Int(1990, 1080), new Vector2Int(2560, 1440), new Vector2Int(3840, 2160),
                             new Vector2Int(1280, 1024), new Vector2Int(2560, 1080) })
                {
                    Click(Get<Button>(binder, "skillMenuButton"));
                    if (!binder.ViewState.SelectedCardRuntimeId.HasValue)
                        Click(Buttons(cards).First(button => button.interactable));
                    yield return RenderAndCheck(binder, camera, size, "Cards");
                    Click(Get<Button>(binder, "itemsMenuButton"));
                    Assert.That(Get<GameObject>(binder, "itemPanelRoot").activeSelf, Is.True);
                    Assert.That(Get<GameObject>(binder, "handoffListRoot").activeSelf, Is.False);
                    Assert.That(Buttons(Get<GameObject>(binder, "itemListRoot")).Length, Is.EqualTo(18));
                    Click(Buttons(Get<GameObject>(binder, "itemListRoot"))[0]);
                    Assert.That(Buttons(Get<GameObject>(binder, "itemListRoot"))[0].transform.Find("DiagonalItemPlate").GetComponent<Image>().sprite,
                        Is.EqualTo(authoredRow));
                    if (!itemArtChecked)
                    {
                        Assert.That(binder.transform.Find("ItemSelectionOverlay/SelectedItemArtwork").GetComponent<Image>().sprite,
                            Is.EqualTo(authoredRow));
                        artFields.Update();
                        artFields.FindProperty("itemArtwork").arraySize = 0;
                        artFields.ApplyModifiedPropertiesWithoutUndo();
                        typeof(FinalCombatUIBinder).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(binder, null);
                        itemArtChecked = true;
                    }
                    Assert.That(binder.ViewState.SelectedItem.ItemId, Is.EqualTo("test.0"));
                    Assert.That(binder.ViewState.SelectedCardRuntimeId, Is.Null);
                    Assert.That(Label(Buttons(Get<GameObject>(binder, "itemListRoot"))[0]), Is.EqualTo("검증 아이템 1"));
                    Assert.That(Buttons(Get<GameObject>(binder, "itemListRoot"))[0].transform.Find("ItemQuantity").GetComponent<Text>().text,
                        Is.EqualTo("x002"));
                    Assert.That(itemSource.UseCount, Is.Zero);
                    yield return RenderAndCheck(binder, camera, size, "Items");
                    if (size.x == 1990)
                    {
                        yield return new WaitForSecondsRealtime(.35f);
                        yield return CombatReferenceGameViewEvidence.Capture("Items");
                    }
                }
                Click(Buttons(Get<GameObject>(binder, "targetListRoot"))[0]);
                Assert.That(Get<Button>(binder, "confirmButton").interactable, Is.True);
                Click(Get<Button>(binder, "cancelButton"));
                Assert.That(binder.ViewState.SelectedItem, Is.Null);
                yield return new WaitForSecondsRealtime(0.75f);
                Assert.That(Get<GameObject>(binder, "itemPanelRoot").activeSelf, Is.False);
                binder.enabled = false;
                Assert.That(binder.IsBound, Is.False);
                binder.enabled = true;
                binder.Bind(session, machine, driver);
                Click(Buttons(Get<GameObject>(binder, "actorListRoot")).Single(b => Label(b) == "플레이어"));
                Click(Get<Button>(binder, "itemsMenuButton"));
                Assert.That(Get<GameObject>(binder, "itemPanelRoot").activeSelf, Is.True);
                Click(Get<Button>(binder, "cancelButton"));
                Click(Get<Button>(binder, "actMenuButton"));
                Click(Buttons(Get<GameObject>(binder, "actorListRoot")).Single(b => Label(b) == "플레이어"));
                Click(Buttons(Get<GameObject>(binder, "skillListRoot")).First(button => button.interactable));
                Click(Buttons(Get<GameObject>(binder, "targetListRoot"))[0]);
                CombatHandCard committed = binder.ViewState.HandCards.Single(value =>
                    value.RuntimeId == binder.ViewState.SelectedCardRuntimeId);
                Click(Get<Button>(binder, "confirmButton"));
                Assert.That(binder.GetComponentsInChildren<CombatUITransientPresentation>().Any(
                    effect => effect.name == "StandoffPresentation"), Is.False,
                    "Standoff must not remain over the action presentation.");
                Assert.That(binder.transform.Find("CommittedFilmCardPresentation"), Is.Null,
                    "An attack declaration is not committed while the enemy response is pending.");
                Assert.That(driver.PendingDecisionRequest.Kind, Is.EqualTo(CombatExchangeDecisionKind.Response));
                Assert.That(driver.SubmitNoResponse(driver.PendingDecisionRequest), Is.True);
                Assert.That(binder.transform.Find("ReferenceMenu").gameObject.activeSelf, Is.False,
                    "The command reel must leave with the decision UI during resolved action presentation.");
                Assert.That(binder.transform.Find("CommittedFilmCardPresentation"), Is.Not.Null,
                    "The real command path must produce the card transition.");
                typeof(CombatSession).GetMethod("NotifyCardCommitted", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(session, new object[] { ally, committed });
                Assert.That(binder.GetComponentsInChildren<CombatUITransientPresentation>().Count(
                    effect => effect.name == "CommittedFilmCardPresentation"), Is.EqualTo(1));
                float scale = Time.timeScale;
                Time.timeScale = 0f;
                try
                {
                    yield return new WaitForSecondsRealtime(0.12f);
                    CapturePresentation(camera, "CommittedCard");
                    yield return CombatReferenceGameViewEvidence.Capture("CommittedCard");
                    yield return new WaitForSecondsRealtime(1.3f);
                }
                finally { Time.timeScale = scale; }
                Assert.That(binder.GetComponentsInChildren<CombatUITransientPresentation>(), Is.Empty,
                    "UI effects must finish at zero time scale.");
                binder.Unbind();
                Assert.That(Get<GameObject>(binder, "itemPanelRoot").activeSelf, Is.False);
                Assert.That(HasCueSubscriber(director, binder), Is.False);
                RaiseCue(director, CombatFinalPresentationCueKind.HitReaction, ally, enemy);
                Assert.That(binder.GetComponentsInChildren<CombatUITransientPresentation>(), Is.Empty);

                GameObject completionOwner = new("CompletionOwner");
                completionOwner.SetActive(false);
                CombatUIRootController rootController = completionOwner.AddComponent<CombatUIRootController>();
                typeof(CombatUIRootController).GetField("finalCombatUI", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(rootController, binder);
                try
                {
                    foreach (CombatEndReason reason in new[] { CombatEndReason.Victory, CombatEndReason.Defeat, CombatEndReason.Escape })
                    {
                        // Also exercise completion before the router activates its destination.
                        if (reason == CombatEndReason.Defeat) canvasObject.SetActive(false);
                        typeof(CombatUIRootController).GetField("_activeSession", BindingFlags.Instance | BindingFlags.NonPublic)
                            .SetValue(rootController, session);
                        typeof(CombatUIRootController).GetMethod("HandleCombatEnded", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(rootController, new object[] { new CombatResult {
                                EndReason = reason, IsWin = reason == CombatEndReason.Victory,
                                EscapeSucceeded = reason == CombatEndReason.Escape } });
                        CombatUITransientPresentation completion = canvasObject.GetComponentsInChildren<CombatUITransientPresentation>(true)
                            .Single(effect => effect.name == "CombatCompletionPresentation");
                        if (reason == CombatEndReason.Defeat)
                        {
                            Assert.That(completion.gameObject.activeInHierarchy, Is.False);
                            canvasObject.SetActive(true);
                        }
                        Assert.That(binder.gameObject.activeSelf, Is.False);
                        Assert.That(completion.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                        Assert.That(completion.GetComponentsInChildren<Graphic>().All(graphic => !graphic.raycastTarget), Is.True);
                        Assert.That(completion.GetComponentInChildren<Text>().text,
                            Is.EqualTo(reason == CombatEndReason.Victory ? "VICTORY" : reason == CombatEndReason.Defeat ? "DEFEAT" : "ESCAPED"));
                        yield return new WaitForSecondsRealtime(0.12f);
                        CapturePresentation(camera, reason.ToString(), completion.transform.Find("CompletionBand") as RectTransform);
                        yield return CombatReferenceGameViewEvidence.Capture(reason.ToString());
                        yield return new WaitForSecondsRealtime(0.55f);
                        Assert.That(completion == null, Is.True);
                    }
                }
                finally { Object.Destroy(completionOwner); }
                Assert.That(itemSource.UseCount, Is.Zero);
                Assert.That(errors, Is.Empty, string.Join("\n", errors));
                Assert.That(PointerFailures, Is.Empty, string.Join("\n", PointerFailures));
            }
            finally
            {
                Application.logMessageReceived -= capture;
                Object.Destroy(canvasObject);
                Object.Destroy(cameraObject);
                Object.Destroy(driverObject);
                foreach (SkillDefinitionSO definition in definitions) Object.Destroy(definition);
            }
        }

        private static IEnumerator RenderAndCheck(FinalCombatUIBinder binder, Camera camera, Vector2Int size, string state)
        {
            RenderTexture target = new(size.x, size.y, 24);
            camera.targetTexture = target;
            yield return new WaitForSecondsRealtime(.8f);
            Canvas.ForceUpdateCanvases();
            foreach (string name in new[] { "ReferenceMenu", "FilmCardViewport", "CommandButtons", "TargetViewport",
                         "Status/PlayerStatus", "Status/EnemyStatus", "Status/TurnStatus", "Status/CombatGuidance" })
            {
                Rect rect = ScreenRect((RectTransform)binder.transform.Find(name), camera);
                Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(-1), name);
                Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(-1), name);
                Assert.That(rect.xMax, Is.LessThanOrEqualTo(size.x + 1), name);
                Assert.That(rect.yMax, Is.LessThanOrEqualTo(size.y + 1), name);
            }
            Rect film = ScreenRect((RectTransform)binder.transform.Find("FilmCardViewport"), camera);
            Rect commands = ScreenRect((RectTransform)binder.transform.Find("CommandButtons"), camera);
            Assert.That(film.Overlaps(commands), Is.False);
            Assert.That(film.height / size.y, Is.LessThan(0.28f));
            foreach (CombatReferenceGraphic sector in binder.transform.Find("ReferenceMenu").GetComponentsInChildren<CombatReferenceGraphic>())
                if (sector.name == "ReferenceSector")
                    Assert.That(sector.Raycast(film.center, camera), Is.False, "Fan sectors must not cover the film choices.");
            Rect player = ScreenRect((RectTransform)binder.transform.Find("Status/PlayerStatus"), camera);
            Rect enemy = ScreenRect((RectTransform)binder.transform.Find("Status/EnemyStatus"), camera);
            Rect guidance = ScreenRect((RectTransform)binder.transform.Find("Status/CombatGuidance"), camera);
            Assert.That(player.Overlaps(guidance) || enemy.Overlaps(guidance), Is.False);
            GameObject overlay = Get<GameObject>(binder, "itemPanelRoot");
            if (overlay.activeSelf)
            {
                Rect items = ScreenRect((RectTransform)overlay.transform.Find("ItemViewport"), camera);
                Assert.That(binder.transform.Find("ReferenceMenu").gameObject.activeSelf, Is.False);
                Assert.That(items.Overlaps(ScreenRect((RectTransform)binder.transform.Find("TargetViewport"), camera)),
                    Is.False, "Item target choices must not cover the slanted rows.");
                ScrollRect scroll = Get<GameObject>(binder, "itemListRoot").GetComponentInParent<ScrollRect>();
                Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
                // Pointer events belong to the actual Game View. Offscreen RenderTexture
                // coordinates can differ from RectTransformUtility's native event-camera space.
                camera.targetTexture = null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                film = ScreenRect((RectTransform)binder.transform.Find("FilmCardViewport"), camera);
                GraphicRaycaster raycaster = binder.GetComponentInParent<GraphicRaycaster>();
                List<RaycastResult> hits = new();
                raycaster.Raycast(new PointerEventData(EventSystem.current) { position = film.center }, hits);
                Assert.That(hits[0].gameObject, Is.EqualTo(overlay), "Dimmer must block the cards.");
                Button cancel = Get<Button>(binder, "cancelButton");
                hits.Clear();
                raycaster.Raycast(new PointerEventData(EventSystem.current)
                    { position = ScreenRect(cancel.GetComponent<RectTransform>(), camera).center }, hits);
                CheckPointer(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == cancel,
                    $"Cancel must remain above the dimmer at {size}. Hits: {string.Join(", ", hits.Select(hit => hit.gameObject.name + "/" + hit.gameObject.transform.parent?.name + "@" + hit.screenPosition))}; " +
                    $"target={cancel.targetGraphic.name}, raycast={cancel.targetGraphic.raycastTarget}, depth={cancel.targetGraphic.depth}, cull={cancel.targetGraphic.canvasRenderer.cull}, alpha={cancel.targetGraphic.color.a}; " +
                    $"buttonRect={ScreenRect((RectTransform)cancel.transform, camera)}, graphicRect={ScreenRect(cancel.targetGraphic.rectTransform, camera)}, " +
                    $"filter={cancel.targetGraphic.Raycast(ScreenRect((RectTransform)cancel.transform, camera).center, camera)}, " +
                    $"groups={string.Join(",", cancel.GetComponentsInParent<CanvasGroup>().Select(group => group.name + ":" + group.blocksRaycasts))}; " +
                    $"registered={GraphicRegistry.GetRaycastableGraphicsForCanvas(binder.GetComponentInParent<Canvas>()).Contains(cancel.targetGraphic)}, " +
                    $"world={cancel.targetGraphic.transform.position}, forward={cancel.targetGraphic.transform.forward}, camera={camera.transform.position}, near={camera.nearClipPlane}, far={camera.farClipPlane}; " +
                    $"rectangle={RectTransformUtility.RectangleContainsScreenPoint(cancel.targetGraphic.rectTransform, ScreenRect((RectTransform)cancel.transform, camera).center, raycaster.eventCamera, cancel.targetGraphic.raycastPadding)}, " +
                    $"padding={cancel.targetGraphic.raycastPadding}, eventCamera={raycaster.eventCamera?.name}, canvas={cancel.targetGraphic.canvas.name}, blocker={raycaster.blockingObjects}");
                Button item = Buttons(Get<GameObject>(binder, "itemListRoot"))[1];
                hits.Clear();
                raycaster.Raycast(new PointerEventData(EventSystem.current)
                    { position = ScreenRect((RectTransform)item.transform, camera).center }, hits);
                CheckPointer(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == item,
                    $"The slanted row must receive pointer input at {size}.");
                camera.targetTexture = target;
                yield return null;
                Canvas.ForceUpdateCanvases();
            }
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            Texture2D image = new(size.x, size.y, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Logs");
            Directory.CreateDirectory("Logs/CombatUI-2D-2");
            File.WriteAllBytes($"Logs/CombatUI-2D-2/{state}_{size.x}x{size.y}.png", image.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.Destroy(image);
            target.Release();
            Object.Destroy(target);
        }

        private static Rect ScreenRect(RectTransform rect, Camera camera)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void CheckPointer(bool passed, string message)
        {
            if (!passed) PointerFailures.Add(message);
        }
        private static void CapturePresentation(Camera camera, string state, RectTransform fullWidthBand = null)
        {
            RenderTexture target = new(1990, 1080, 24);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            if (fullWidthBand != null)
            {
                Rect bounds = ScreenRect(fullWidthBand, camera);
                Assert.That(bounds.width, Is.GreaterThan(1800f));
                Assert.That(bounds.height, Is.GreaterThan(180f));
            }
            camera.Render();
            RenderTexture.active = target;
            Texture2D image = new(1990, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1990, 1080), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Logs/CombatUI-2D-2");
            File.WriteAllBytes($"Logs/CombatUI-2D-2/{state}_1990x1080.png", image.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.Destroy(image);
            target.Release();
            Object.Destroy(target);
        }
        private static T Get<T>(FinalCombatUIBinder binder, string name) =>
            (T)typeof(FinalCombatUIBinder).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(binder);
        private static void RaiseCue(CombatDirector director, CombatFinalPresentationCueKind kind, ICombatant actor, ICombatant target) =>
            typeof(CombatDirector).GetMethod("RaiseFinalCue", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(director, new object[] { kind, actor, target, null });
        private static bool HasCueSubscriber(CombatDirector director, FinalCombatUIBinder binder) =>
            (typeof(CombatDirector).GetField("FinalPresentationCueRaised", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(director) as Delegate)?.GetInvocationList().Any(handler => ReferenceEquals(handler.Target, binder)) == true;
        private static Button[] Buttons(GameObject root) => root.GetComponentsInChildren<Button>().Where(b => b.gameObject.activeInHierarchy).ToArray();
        private static string Label(Button button) =>
            (button.transform.Find("CardTitle")?.GetComponent<Text>() ?? button.GetComponentInChildren<Text>()).text;
        private static void Click(Button button) { Assert.That(button.interactable, Is.True, Label(button)); button.onClick.Invoke(); }
        private sealed class ZeroRandom : ICombatRuleRandomSource { public int NextInclusive(int min, int max) => min; }
        private sealed class ItemSource : ICombatItemUseExecutor
        {
            public int UseCount;
            public IReadOnlyList<CombatItemOption> GetUsableItems(CombatSession session, ICombatant user) =>
                Enumerable.Range(0, 18).Select(i => new CombatItemOption($"test.{i}", $"검증 아이템 {i + 1}", i + 2, CombatItemTargetRule.Self)).ToArray();
            public IReadOnlyList<ICombatant> GetUsableTargets(CombatSession session, ICombatant user, CombatItemOption item) => new[] { user };
            public CombatItemUseResult TryUse(CombatSession session, CombatItemUseRequest request)
            { UseCount++; return null; }
        }
    }
}
#endif
