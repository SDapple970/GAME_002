#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Combat.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>Installs presentation into the existing combat prefab without replacing its owners or buttons.</summary>
    public static class ProductionCombatUILayoutSetup
    {
        public const string PrefabPath = "Assets/GAME/Prefabs/UI/ProductionDungeonUI.prefab";
        private const string ArtRoot = "Assets/GAME/Image/ui/ui/";

        [MenuItem("GAME/Production UI/Install Combat Reference Layout")]
        public static void Install()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                FinalCombatUIBinder binder = root.GetComponentInChildren<FinalCombatUIBinder>(true);
                if (binder == null) throw new InvalidOperationException("Canonical FinalCombatUIBinder is missing.");
                if (binder.transform.Find("ReferenceMenu") != null)
                {
                    Validate(root);
                    return;
                }
                SerializedObject fields = new SerializedObject(binder);
                string[] retained = { "panelRoot", "actorListRoot", "skillListRoot", "targetListRoot", "handoffListRoot",
                    "optionButtonPrefab", "confirmButton", "cancelButton", "noResponseButton", "continueButton",
                    "handoffButton", "endButton", "phaseText", "contextText", "playerHpText", "enemyHpText",
                    "playerMpText", "enemyMpText", "pressureText", "playerPostureText", "enemyPostureText", "displayFont" };
                Dictionary<string, Object> before = retained.ToDictionary(name => name,
                    name => fields.FindProperty(name).objectReferenceValue);
                Dictionary<Button, string> events = root.GetComponentsInChildren<Button>(true)
                    .ToDictionary(button => button, button => JsonUtility.ToJson(button.onClick));
                RectTransform panel = binder.GetComponent<RectTransform>();
                Font font = (Font)before["displayFont"];
                Button template = (Button)before["optionButtonPrefab"];
                Sprite wedge = SpriteAt("selecy.aseprite");
                Sprite reel = SpriteAt("film_reel.png");
                Sprite frame = SpriteAt("the box.png");
                CanvasScaler scaler = binder.GetComponentInParent<CanvasScaler>();
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                scaler.referenceResolution = new Vector2(1920, 1080);
                Image panelImage = panel.GetComponent<Image>();
                panelImage.color = Color.clear;

                RectTransform actor = ((GameObject)before["actorListRoot"]).GetComponent<RectTransform>();
                RectTransform skills = ((GameObject)before["skillListRoot"]).GetComponent<RectTransform>();
                RectTransform targets = ((GameObject)before["targetListRoot"]).GetComponent<RectTransform>();
                RectTransform auxiliary = ((GameObject)before["handoffListRoot"]).GetComponent<RectTransform>();
                RectTransform actorsViewport = Scroll(panel, "ActorViewport", actor, false);
                Place(actorsViewport, new Vector2(0, 0), new Vector2(360, 550), new Vector2(1060, 88), Vector2.zero);
                RectTransform filmViewport = Scroll(panel, "FilmCardViewport", skills, false);
                Place(filmViewport, Vector2.zero, new Vector2(28, 28), new Vector2(1350, 254), Vector2.zero);
                RectTransform auxiliaryViewport = Scroll(panel, "AuxiliaryViewport", auxiliary, false);
                Place(auxiliaryViewport, Vector2.zero, new Vector2(360, 310), new Vector2(1060, 92), Vector2.zero);

                RectTransform itemPanel = Rect("ItemSelectionOverlay", panel);
                Stretch(itemPanel);
                Image dimmer = itemPanel.gameObject.AddComponent<Image>();
                dimmer.color = new Color(0, 0, 0, 0.65f);
                dimmer.raycastTarget = true;
                RectTransform itemContent = Rect("ItemListRoot", itemPanel);
                itemContent.gameObject.AddComponent<VerticalLayoutGroup>();
                RectTransform itemViewport = Scroll(itemPanel, "ItemViewport", itemContent, true);
                Place(itemViewport, new Vector2(1, 0.5f), new Vector2(-28, -20), new Vector2(820, 390), new Vector2(1, 0.5f));
                Image itemReel = Rect("ItemReel", itemPanel).gameObject.AddComponent<Image>();
                itemReel.sprite = reel;
                itemReel.preserveAspect = true;
                itemReel.raycastTarget = false;
                Place(itemReel.rectTransform, Vector2.zero, new Vector2(80, 660), new Vector2(170, 170), Vector2.zero);
                itemPanel.gameObject.SetActive(false);

                RectTransform menu = Rect("ReferenceMenu", panel);
                Place(menu, Vector2.zero, new Vector2(28, 316), new Vector2(290, 268), Vector2.zero);
                Button act = MenuButton("ACT", menu, template, wedge, font, 210);
                Button skill = MenuButton("SKILL", menu, template, wedge, font, 120);
                Button items = MenuButton("ITEMS", menu, template, wedge, font, 30);
                Image menuReel = Rect("MenuReel", menu).gameObject.AddComponent<Image>();
                menuReel.sprite = reel;
                menuReel.preserveAspect = true;
                menuReel.raycastTarget = false;
                Place(menuReel.rectTransform, Vector2.zero, new Vector2(214, 110), new Vector2(76, 76), Vector2.zero);

                // Existing targets and commands stay above the dimmer so item confirmation remains reachable.
                RectTransform targetViewport = Scroll(panel, "TargetViewport", targets, false);
                Place(targetViewport, Vector2.zero, new Vector2(360, 420), new Vector2(1060, 88), Vector2.zero);
                RectTransform commands = Rect("CommandButtons", panel);
                Place(commands, new Vector2(1, 0), new Vector2(-28, 28), new Vector2(436, 254), new Vector2(1, 0));
                GridLayoutGroup grid = commands.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(210, 68);
                grid.spacing = new Vector2(12, 12);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 2;
                grid.childAlignment = TextAnchor.MiddleCenter;
                foreach (string name in new[] { "confirmButton", "cancelButton", "noResponseButton", "continueButton", "handoffButton", "endButton" })
                    ((Button)before[name]).transform.SetParent(commands, false);

                PlaceText(panel, (Text)before["phaseText"], new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(600, 44), TextAnchor.MiddleCenter, 28);
                PlaceText(panel, (Text)before["pressureText"], new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(350, 36), TextAnchor.MiddleCenter, 22);
                PlaceText(panel, (Text)before["playerHpText"], new Vector2(0, 1), new Vector2(28, -40), new Vector2(460, 34), TextAnchor.MiddleLeft, 22);
                PlaceText(panel, (Text)before["playerMpText"], new Vector2(0, 1), new Vector2(28, -78), new Vector2(460, 34), TextAnchor.MiddleLeft, 22);
                PlaceText(panel, (Text)before["playerPostureText"], new Vector2(0, 1), new Vector2(28, -120), new Vector2(460, 60), TextAnchor.MiddleLeft, 19);
                PlaceText(panel, (Text)before["enemyHpText"], Vector2.one, new Vector2(-28, -40), new Vector2(460, 34), TextAnchor.MiddleRight, 22);
                PlaceText(panel, (Text)before["enemyMpText"], Vector2.one, new Vector2(-28, -78), new Vector2(460, 34), TextAnchor.MiddleRight, 22);
                PlaceText(panel, (Text)before["enemyPostureText"], Vector2.one, new Vector2(-28, -120), new Vector2(460, 60), TextAnchor.MiddleRight, 19);
                TMP_Text context = (TMP_Text)before["contextText"];
                context.transform.SetParent(panel, false);
                Place(context.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -210), new Vector2(850, 210), new Vector2(0.5f, 0.5f));
                context.fontSize = 21;
                context.enableAutoSizing = true;
                context.fontSizeMin = 16;
                context.fontSizeMax = 21;
                context.raycastTarget = false;
                fields.Update();
                fields.FindProperty("actMenuButton").objectReferenceValue = act;
                fields.FindProperty("skillMenuButton").objectReferenceValue = skill;
                fields.FindProperty("itemsMenuButton").objectReferenceValue = items;
                fields.FindProperty("itemPanelRoot").objectReferenceValue = itemPanel.gameObject;
                fields.FindProperty("itemListRoot").objectReferenceValue = itemContent.gameObject;
                fields.FindProperty("cardFrameSprite").objectReferenceValue = frame;
                fields.ApplyModifiedPropertiesWithoutUndo();
                foreach (string name in retained)
                    if (fields.FindProperty(name).objectReferenceValue != before[name])
                        throw new InvalidOperationException($"Existing binding changed: {name}");
                foreach (KeyValuePair<Button, string> pair in events)
                    if (JsonUtility.ToJson(pair.Key.onClick) != pair.Value)
                        throw new InvalidOperationException($"Existing UnityEvent changed: {pair.Key.name}");
                Validate(root);
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new InvalidOperationException("Could not save combat UI prefab.");
                Debug.Log("[CombatUILayout] Saved existing prefab; retained all original Binder references and Button UnityEvents.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void Validate(GameObject root)
        {
            FinalCombatUIBinder[] binders = root.GetComponentsInChildren<FinalCombatUIBinder>(true);
            if (binders.Length != 1) throw new InvalidOperationException("Expected exactly one FinalCombatUIBinder.");
            SerializedObject fields = new SerializedObject(binders[0]);
            foreach (string name in new[] { "actMenuButton", "skillMenuButton", "itemsMenuButton", "itemPanelRoot", "itemListRoot", "cardFrameSprite" })
                if (fields.FindProperty(name).objectReferenceValue == null)
                    throw new InvalidOperationException($"Reference layout binding missing: {name}");
            if (root.GetComponentsInChildren<CombatUIRootController>(true).Length != 1)
                throw new InvalidOperationException("Expected existing single CombatUIRootController.");
        }

        private static Sprite SpriteAt(string file)
        {
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(ArtRoot + file).OfType<Sprite>().ToArray();
            if (sprites.Length != 1) throw new InvalidOperationException($"Expected one imported Sprite: {file}; found {sprites.Length}.");
            Debug.Log($"[CombatUILayout] Sprite {file} -> {sprites[0].name}");
            return sprites[0];
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static RectTransform Scroll(Transform parent, string name, RectTransform content, bool vertical)
        {
            RectTransform viewport = Rect(name, parent);
            Image hitSurface = viewport.gameObject.AddComponent<Image>();
            hitSurface.color = new Color(0, 0, 0, 0.01f);
            viewport.gameObject.AddComponent<RectMask2D>();
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = !vertical;
            scroll.vertical = vertical;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 45;
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0, vertical ? 1 : 0);
            content.anchorMax = new Vector2(vertical ? 1 : 0, 1);
            content.pivot = new Vector2(0, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            HorizontalOrVerticalLayoutGroup layout = content.GetComponent<HorizontalOrVerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 14;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = vertical;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = vertical ? ContentSizeFitter.FitMode.Unconstrained : ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = vertical ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            return viewport;
        }

        private static Button MenuButton(string name, Transform parent, Button template, Sprite sprite, Font font, float y)
        {
            Button button = Object.Instantiate(template, parent);
            button.name = name;
            Place(button.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0, y), new Vector2(240, 76), Vector2.zero);
            Image image = button.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            Text text = button.GetComponentInChildren<Text>(true);
            text.text = name;
            text.font = font;
            text.fontSize = 36;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            Stretch(text.rectTransform);
            return button;
        }

        private static void PlaceText(Transform parent, Text text, Vector2 anchor, Vector2 position, Vector2 size, TextAnchor alignment, int fontSize)
        {
            text.transform.SetParent(parent, false);
            Place(text.rectTransform, anchor, position, size, new Vector2(anchor.x, 0.5f));
            text.alignment = alignment;
            text.fontSize = fontSize;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;
        }
    }
}
#endif
