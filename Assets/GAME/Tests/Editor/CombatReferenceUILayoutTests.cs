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
using Game.Combat.Model;
using Game.Combat.UI;
using Game.EditorTools;
using NUnit.Framework;
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
        [Test]
        public void SavedPrefab_RetainsSingleOwnersAndOriginalButtonBindings()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProductionCombatUILayoutSetup.PrefabPath);
            Assert.DoesNotThrow(() => ProductionCombatUILayoutSetup.Validate(prefab));
            FinalCombatUIBinder binder = prefab.GetComponentInChildren<FinalCombatUIBinder>(true);
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
            string before = File.ReadAllText(ProductionCombatUILayoutSetup.PrefabPath);
            ProductionCombatUILayoutSetup.Install();
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
            yield return new EnterPlayMode();
            yield return RunScenario();
            yield return new ExitPlayMode();
        }

        private static IEnumerator RunScenario()
        {
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
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.18f, 0.22f, 0.24f);
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                FinalCombatUIBinder source = AssetDatabase.LoadAssetAtPath<GameObject>(ProductionCombatUILayoutSetup.PrefabPath)
                    .GetComponentInChildren<FinalCombatUIBinder>(true);
                FinalCombatUIBinder binder = Object.Instantiate(source, canvasObject.transform);
                binder.gameObject.SetActive(true);
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
                    ISkill skill = new SoSkill(definition);
                    ally.AddSkill(skill);
                    entries.Add(new CombatHandEntry(skill, CombatCardKind.Normal));
                }
                CombatSession session = new(StartReason.PlayerFirstHit, Side.Allies, new InspirationPool(10),
                    new Game.Combat.Environment.CombatEnvironment(), CombatFlowMode.StandoffClashChain,
                    new CombatRuntimeConfig(10, 10, 3, 0));
                session.Allies.Add(ally);
                session.Enemies.Add(enemy);
                CombatStateMachine machine = new(session);
                Assert.That(session.TryEnableHandValidation(ally, entries, 12, 0, 19), Is.True);
                CombatFlowOrchestrator driver = driverObject.AddComponent<CombatFlowOrchestrator>();
                ItemSource itemSource = new();
                driver.RegisterCombatItemUseExecutor(itemSource);
                Assert.That(driver.BindFinalExchange(session, machine, new ZeroRandom(), new AuthoredCombatClashSideInputProvider(session)), Is.True);
                binder.Bind(session, machine, driver);
                yield return null;
                Click(Buttons(Get<GameObject>(binder, "actorListRoot")).Single(b => Label(b) == "1"));
                yield return null;
                GameObject cards = Get<GameObject>(binder, "skillListRoot");
                Assert.That(Buttons(cards).Length, Is.EqualTo(12));
                long selectedCard = binder.ViewState.HandCards[0].RuntimeId;
                Click(Buttons(cards)[0]);
                Assert.That(binder.ViewState.SelectedCardRuntimeId, Is.EqualTo(selectedCard));
                Assert.That(session.CardExchangeIndex, Is.EqualTo(1));
                Assert.That(enemy.HP, Is.EqualTo(20));
                yield return null;
                Canvas.ForceUpdateCanvases();
                ScrollRect film = cards.GetComponentInParent<ScrollRect>();
                Assert.That(film.content.rect.width, Is.GreaterThan(film.viewport.rect.width));
                film.horizontalNormalizedPosition = 1;
                Canvas.ForceUpdateCanvases();
                Assert.That(film.horizontalNormalizedPosition, Is.GreaterThan(0.95f));
                film.horizontalNormalizedPosition = 0;

                foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(1280, 1024), new Vector2Int(2560, 1080) })
                {
                    Click(Get<Button>(binder, "skillMenuButton"));
                    if (!binder.ViewState.SelectedCardRuntimeId.HasValue) Click(Buttons(cards)[0]);
                    yield return RenderAndCheck(binder, camera, size, "Cards");
                    Click(Get<Button>(binder, "itemsMenuButton"));
                    Assert.That(Get<GameObject>(binder, "itemPanelRoot").activeSelf, Is.True);
                    Assert.That(Get<GameObject>(binder, "handoffListRoot").activeSelf, Is.False);
                    Assert.That(Buttons(Get<GameObject>(binder, "itemListRoot")).Length, Is.EqualTo(18));
                    Click(Buttons(Get<GameObject>(binder, "itemListRoot"))[0]);
                    Assert.That(binder.ViewState.SelectedItem.ItemId, Is.EqualTo("test.0"));
                    Assert.That(binder.ViewState.SelectedCardRuntimeId, Is.Null);
                    Assert.That(Label(Buttons(Get<GameObject>(binder, "itemListRoot"))[0]), Is.EqualTo("▶ ITEM 검증 아이템 1 x2"));
                    Assert.That(itemSource.UseCount, Is.Zero);
                    yield return RenderAndCheck(binder, camera, size, "Items");
                }
                Click(Buttons(Get<GameObject>(binder, "targetListRoot"))[0]);
                Assert.That(Get<Button>(binder, "confirmButton").interactable, Is.True);
                Click(Get<Button>(binder, "cancelButton"));
                Assert.That(binder.ViewState.SelectedItem, Is.Null);
                Assert.That(Get<GameObject>(binder, "itemPanelRoot").activeSelf, Is.False);
                binder.enabled = false;
                Assert.That(binder.IsBound, Is.False);
                binder.enabled = true;
                binder.Bind(session, machine, driver);
                Click(Buttons(Get<GameObject>(binder, "actorListRoot")).Single(b => Label(b) == "1"));
                Click(Get<Button>(binder, "itemsMenuButton"));
                Assert.That(Get<GameObject>(binder, "itemPanelRoot").activeSelf, Is.True);
                binder.Unbind();
                Assert.That(Get<GameObject>(binder, "itemPanelRoot").activeSelf, Is.False);
                Assert.That(itemSource.UseCount, Is.Zero);
                Assert.That(errors, Is.Empty, string.Join("\n", errors));
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
            yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (string name in new[] { "ReferenceMenu", "FilmCardViewport", "CommandButtons", "TargetViewport" })
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
            GameObject overlay = Get<GameObject>(binder, "itemPanelRoot");
            if (overlay.activeSelf)
            {
                Rect items = ScreenRect((RectTransform)overlay.transform.Find("ItemViewport"), camera);
                Rect menu = ScreenRect((RectTransform)binder.transform.Find("ReferenceMenu"), camera);
                Assert.That(items.Overlaps(menu), Is.False);
                Assert.That(items.yMin, Is.GreaterThanOrEqualTo(film.yMax));
                Rect reel = ScreenRect((RectTransform)overlay.transform.Find("ItemReel"), camera);
                Assert.That(reel.Overlaps(menu), Is.False);
                ScrollRect scroll = Get<GameObject>(binder, "itemListRoot").GetComponentInParent<ScrollRect>();
                Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
                GraphicRaycaster raycaster = binder.GetComponentInParent<GraphicRaycaster>();
                List<RaycastResult> hits = new();
                raycaster.Raycast(new PointerEventData(EventSystem.current) { position = film.center }, hits);
                Assert.That(hits[0].gameObject, Is.EqualTo(overlay), "Dimmer must block the cards.");
                Button cancel = Get<Button>(binder, "cancelButton");
                hits.Clear();
                raycaster.Raycast(new PointerEventData(EventSystem.current)
                    { position = ScreenRect(cancel.GetComponent<RectTransform>(), camera).center }, hits);
                Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.EqualTo(cancel), "Cancel must remain above the dimmer.");
            }
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            Texture2D image = new(size.x, size.y, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes($"Logs/CombatUI_{state}_{size.x}x{size.y}.png", image.EncodeToPNG());
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
        private static T Get<T>(FinalCombatUIBinder binder, string name) =>
            (T)typeof(FinalCombatUIBinder).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(binder);
        private static Button[] Buttons(GameObject root) => root.GetComponentsInChildren<Button>().Where(b => b.gameObject.activeInHierarchy).ToArray();
        private static string Label(Button button) => button.GetComponentInChildren<Text>().text;
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
