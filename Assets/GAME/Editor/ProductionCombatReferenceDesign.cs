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
    /// <summary>Authors only the canonical UI prefab's presentation, in source-video pixels.</summary>
    public static class ProductionCombatReferenceDesign
    {
        private static readonly Color Blue = new Color32(0, 61, 255, 255);

        [MenuItem("GAME/Production UI/Reconstruct Combat Video Design (2-D)")]
        public static void Install()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ProductionCombatUILayoutSetup.PrefabPath);
            try
            {
                FinalCombatUIBinder binder = root.GetComponentInChildren<FinalCombatUIBinder>(true);
                if (binder == null) throw new InvalidOperationException("Canonical combat Binder missing.");
                if (binder.transform.Find("ReferenceFilmBed") != null)
                {
                    bool repaired = false;
                    RectTransform existingTitle = (RectTransform)binder.transform.Find("ItemSelectionOverlay/ItemsTitleBand");
                    if (existingTitle != null && existingTitle.anchoredPosition != new Vector2(-70, -130))
                    {
                        existingTitle.anchoredPosition = new Vector2(-70, -130);
                        repaired = true;
                    }
                    Material reelMaterial = ReelMaterial();
                    foreach (string name in new[] { "ReferenceMenu/MenuReel", "ItemSelectionOverlay/ItemReel" })
                    {
                        Image image = binder.transform.Find(name).GetComponent<Image>();
                        if (image.material != reelMaterial) { image.material = reelMaterial; repaired = true; }
                    }
                    foreach (CombatReferenceGraphic graphic in binder.GetComponentsInChildren<CombatReferenceGraphic>(true))
                    {
                        if (graphic.GetComponent<CanvasRenderer>() != null) continue;
                        graphic.gameObject.AddComponent<CanvasRenderer>();
                        repaired = true;
                    }
                    foreach (string name in new[] { "Status/PlayerStatus/ReferenceBar", "Status/EnemyStatus/ReferenceBar" })
                    {
                        Transform plate = binder.transform.Find(name);
                        if (plate != null && plate.GetSiblingIndex() != 0) { plate.SetAsFirstSibling(); repaired = true; }
                    }
                    foreach (Button button in binder.transform.Find("ReferenceMenu").GetComponentsInChildren<Button>(true))
                    {
                        Transform sector = button.transform.Find("ReferenceSector");
                        if (sector != null && sector.GetSiblingIndex() != 0) { sector.SetAsFirstSibling(); repaired = true; }
                        if (button.colors.normalColor == Blue) continue;
                        SetButtonColors(button);
                        repaired = true;
                    }
                    foreach (Button button in binder.transform.Find("CommandButtons").GetComponentsInChildren<Button>(true))
                    {
                        if (!button.targetGraphic.raycastTarget) { button.targetGraphic.raycastTarget = true; repaired = true; }
                        if (button.colors.normalColor == Blue) continue;
                        SetButtonColors(button);
                        repaired = true;
                    }
                    if (repaired) PrefabUtility.SaveAsPrefabAsset(root, ProductionCombatUILayoutSetup.PrefabPath);
                    return;
                }
                SerializedObject fields = new(binder);
                Dictionary<Button, string> events = root.GetComponentsInChildren<Button>(true)
                    .ToDictionary(button => button, button => JsonUtility.ToJson(button.onClick));
                Dictionary<string, Object> bindings = new();
                SerializedProperty property = fields.GetIterator();
                while (property.NextVisible(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference)
                        bindings[property.propertyPath] = property.objectReferenceValue;
                RectTransform panel = (RectTransform)binder.transform;
                binder.GetComponentInParent<CanvasScaler>(true).referenceResolution = new Vector2(1990, 1080);
                RectTransform status = (RectTransform)panel.Find("Status");
                foreach (var (name, anchor, position, gradient) in new[] {
                    ("PlayerStatus", Vector2.up, new Vector2(0, -4), new Color32(6, 4, 64, 255)),
                    ("EnemyStatus", Vector2.one, new Vector2(0, -4), new Color32(22, 22, 22, 255)) })
                {
                    RectTransform plate = (RectTransform)status.Find(name);
                    Place(plate, anchor, position, new Vector2(536, 118), anchor);
                    plate.GetComponent<Image>().color = Color.clear;
                    Shape("ReferenceBar", plate, CombatReferenceGraphic.Geometry.SlantedPlate,
                        new Color32(22, 22, 22, 255), gradient, 0, 0, 0, 30).transform.SetAsFirstSibling();
                    bool player = name == "PlayerStatus";
                    string prefix = player ? "player" : "enemy";
                    Text hp = (Text)fields.FindProperty(prefix + "HpText").objectReferenceValue;
                    Text mp = (Text)fields.FindProperty(prefix + "MpText").objectReferenceValue;
                    Text posture = (Text)fields.FindProperty(prefix + "PostureText").objectReferenceValue;
                    PlaceText(hp, new Vector2(0, 1), new Vector2(24, -4), new Vector2(480, 42), 30, TextAnchor.MiddleLeft);
                    PlaceText(mp, new Vector2(0, 1), new Vector2(24, -46), new Vector2(480, 32), 23, TextAnchor.MiddleLeft);
                    PlaceText(posture, new Vector2(0, 1), new Vector2(24, -80), new Vector2(480, 30), 21, TextAnchor.MiddleLeft);
                    Image health = Image("HealthTrack", plate, null, Color.white);
                    Place(health.rectTransform, Vector2.zero, new Vector2(24, 1), new Vector2(474, 4), Vector2.zero);
                    Image fill = Image("HealthFill", plate, null, Blue);
                    Place(fill.rectTransform, Vector2.zero, new Vector2(24, 1), new Vector2(474, 4), Vector2.zero);
                }
                RectTransform turn = (RectTransform)status.Find("TurnStatus");
                turn.GetComponent<Image>().color = Color.clear;
                Place(turn, new Vector2(.5f, 1), new Vector2(0, -4), new Vector2(650, 52), new Vector2(.5f, 1));
                PlaceText((Text)fields.FindProperty("phaseText").objectReferenceValue, Vector2.up,
                    new Vector2(10, -2), new Vector2(440, 48), 28, TextAnchor.MiddleLeft);
                PlaceText((Text)fields.FindProperty("pressureText").objectReferenceValue, Vector2.one,
                    new Vector2(-10, -2), new Vector2(150, 48), 24, TextAnchor.MiddleRight);
                RectTransform guidance = (RectTransform)status.Find("CombatGuidance");
                guidance.GetComponent<Image>().color = Color.clear;
                Place(guidance, Vector2.one, new Vector2(-32, -150), new Vector2(490, 188), Vector2.one);
                TMP_Text context = (TMP_Text)fields.FindProperty("contextText").objectReferenceValue;
                TMP_Text details = (TMP_Text)fields.FindProperty("combatDetailsText").objectReferenceValue;
                Place(context.rectTransform, Vector2.up, Vector2.zero, new Vector2(490, 66), Vector2.up);
                Place(details.rectTransform, Vector2.up, new Vector2(0, -70), new Vector2(490, 118), Vector2.up);
                context.fontSize = context.fontSizeMax = 23;
                details.fontSize = details.fontSizeMax = 18;

                Image bed = Image("ReferenceFilmBed", panel, null, Color.black);
                Place(bed.rectTransform, Vector2.zero, Vector2.zero, new Vector2(1990, 200), Vector2.zero);
                bed.rectTransform.anchorMax = Vector2.right;
                bed.rectTransform.sizeDelta = new Vector2(0, 200);
                bed.transform.SetAsFirstSibling();
                RectTransform film = (RectTransform)panel.Find("FilmCardViewport");
                Place(film, Vector2.zero, new Vector2(180, 0), new Vector2(1190, 282), Vector2.zero);
                film.GetComponent<Image>().color = Color.clear;
                HorizontalLayoutGroup row = film.GetComponent<ScrollRect>().content.GetComponent<HorizontalLayoutGroup>();
                row.padding = new RectOffset(12, 12, 18, 10);
                row.spacing = 44;
                row.childAlignment = TextAnchor.LowerLeft;
                Place((RectTransform)panel.Find("ActorViewport"), Vector2.zero, new Vector2(80, 700), new Vector2(560, 70), Vector2.zero);
                Place((RectTransform)panel.Find("TargetViewport"), Vector2.right, new Vector2(-36, 294), new Vector2(760, 70), Vector2.right);
                Place((RectTransform)panel.Find("AuxiliaryViewport"), Vector2.right, new Vector2(-36, 374), new Vector2(760, 70), Vector2.right);

                RectTransform menu = (RectTransform)panel.Find("ReferenceMenu");
                Place(menu, Vector2.zero, new Vector2(438, 430), new Vector2(660, 660), new Vector2(.5f, .5f));
                foreach (var (name, start, end, rotation, offset) in new[] {
                    ("ACT", 145f, 170f, -24f, new Vector2(-176, 94)),
                    ("SKILL", 171f, 191f, 0f, new Vector2(-198, 0)),
                    ("ITEMS", 192f, 218f, 27f, new Vector2(-172, -104)) })
                {
                    Button button = menu.Find(name).GetComponent<Button>();
                    Place((RectTransform)button.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(660, 660), new Vector2(.5f, .5f));
                    button.image.color = Color.clear;
                    button.image.raycastTarget = false;
                    CombatReferenceGraphic sector = Shape("ReferenceSector", button.transform,
                        CombatReferenceGraphic.Geometry.Sector, Blue, Blue, start, end, .24f);
                    sector.raycastTarget = true;
                    sector.transform.SetAsFirstSibling();
                    button.targetGraphic = sector;
                    ColorBlock menuColors = button.colors;
                    menuColors.normalColor = menuColors.selectedColor = Blue;
                    menuColors.highlightedColor = new Color32(44, 92, 255, 255);
                    menuColors.pressedColor = new Color32(0, 35, 180, 255);
                    menuColors.disabledColor = Blue;
                    button.colors = menuColors;
                    Text label = button.GetComponentInChildren<Text>(true);
                    PlaceText(label, new Vector2(.5f, .5f), offset, new Vector2(240, 92), 70, TextAnchor.MiddleCenter);
                    label.rectTransform.pivot = new Vector2(.5f, 1f);
                    label.rectTransform.anchoredPosition += new Vector2(0, 46);
                    label.rectTransform.localEulerAngles = new Vector3(0, 0, rotation);
                    label.fontStyle = FontStyle.Bold;
                    Transform oldWedge = button.transform.Find("MenuWedge");
                    if (oldWedge != null) oldWedge.gameObject.SetActive(false);
                }
                Image reel = menu.Find("MenuReel").GetComponent<Image>();
                reel.color = Color.white;
                reel.material = ReelMaterial();
                Place(reel.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(154, 154), new Vector2(.5f, .5f));
                CombatReferenceGraphic reelBack = Shape("ReelBlue", menu, CombatReferenceGraphic.Geometry.Disc, Blue, Blue);
                Place(reelBack.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(148, 148), new Vector2(.5f, .5f));
                reelBack.transform.SetSiblingIndex(reel.transform.GetSiblingIndex());
                reel.transform.SetAsLastSibling();

                RectTransform commands = (RectTransform)panel.Find("CommandButtons");
                Place(commands, Vector2.right, new Vector2(-32, 32), new Vector2(510, 168), Vector2.right);
                foreach (Button button in commands.GetComponentsInChildren<Button>(true))
                {
                    Vector2 old = ((RectTransform)button.transform).anchoredPosition;
                    Place((RectTransform)button.transform, Vector2.zero,
                        new Vector2(old.x > 0 ? 260 : 0, old.y > 20 ? 88 : 12), new Vector2(246, 64), Vector2.zero);
                    button.image.color = Color.clear;
                    CombatReferenceGraphic graphic = Shape("ReferenceCommand", button.transform,
                        CombatReferenceGraphic.Geometry.SlantedPlate, Blue, Blue);
                    graphic.transform.SetAsFirstSibling();
                    button.targetGraphic = graphic;
                    graphic.raycastTarget = true;
                    ColorBlock commandColors = button.colors;
                    commandColors.normalColor = commandColors.selectedColor = Blue;
                    commandColors.highlightedColor = new Color32(44, 92, 255, 255);
                    commandColors.pressedColor = new Color32(0, 35, 180, 255);
                    commandColors.disabledColor = new Color32(10, 20, 70, 255);
                    button.colors = commandColors;
                }

                RectTransform items = (RectTransform)panel.Find("ItemSelectionOverlay");
                items.GetComponent<Image>().color = new Color(0, 0, 0, .56f);
                RectTransform viewport = (RectTransform)items.Find("ItemViewport");
                Place(viewport, Vector2.right, new Vector2(0, 160), new Vector2(1180, 560), Vector2.right);
                RectTransform content = (RectTransform)viewport.GetComponent<ScrollRect>().content;
                Object.DestroyImmediate(content.GetComponent<VerticalLayoutGroup>());
                content.gameObject.AddComponent<CombatReferenceItemLayout>();
                RectTransform itemReel = (RectTransform)items.Find("ItemReel");
                Place(itemReel, Vector2.zero, new Vector2(-140, -180), new Vector2(520, 520), Vector2.zero);
                itemReel.GetComponent<Image>().color = Color.white;
                itemReel.GetComponent<Image>().material = ReelMaterial();
                CombatReferenceGraphic itemBack = Shape("ItemReelBlue", items, CombatReferenceGraphic.Geometry.Disc, Blue, Blue);
                Place(itemBack.rectTransform, Vector2.zero, new Vector2(-132, -172), new Vector2(504, 504), Vector2.zero);
                itemBack.transform.SetSiblingIndex(itemReel.GetSiblingIndex());
                CombatReferenceGraphic diagonal = Shape("ItemDiagonal", items,
                    CombatReferenceGraphic.Geometry.SlantedPlate, Blue, Blue, skew: 0);
                Place(diagonal.rectTransform, Vector2.zero, new Vector2(640, -60), new Vector2(1500, 160), Vector2.zero);
                diagonal.rectTransform.localEulerAngles = new Vector3(0, 0, 24);
                diagonal.transform.SetSiblingIndex(viewport.GetSiblingIndex());
                CombatReferenceGraphic titleBand = Shape("ItemsTitleBand", items,
                    CombatReferenceGraphic.Geometry.SlantedPlate, Blue, Blue, skew: 0);
                Place(titleBand.rectTransform, Vector2.up, new Vector2(-70, -130), new Vector2(700, 150), Vector2.up);
                titleBand.rectTransform.localEulerAngles = new Vector3(0, 0, 24);
                Text title = Object.Instantiate(menu.Find("ITEMS").GetComponentInChildren<Text>(true), titleBand.transform);
                title.name = "ItemsTitle";
                title.text = "ITEMS";
                Stretch(title.rectTransform);
                title.rectTransform.localRotation = Quaternion.identity;
                title.fontSize = title.resizeTextMaxSize = 115;
                title.alignment = TextAnchor.MiddleCenter;

                CombatReferenceGraphic medallion = Shape("ExchangeMedallion", panel,
                    CombatReferenceGraphic.Geometry.Disc, new Color32(32, 0, 238, 255), new Color32(32, 0, 238, 255));
                Place(medallion.rectTransform, Vector2.zero, new Vector2(-62, -20), new Vector2(270, 270), Vector2.zero);
                Text number = Object.Instantiate((Text)fields.FindProperty("phaseText").objectReferenceValue, medallion.transform);
                number.name = "ExchangeNumber";
                number.text = "1";
                Stretch(number.rectTransform);
                number.rectTransform.localEulerAngles = new Vector3(0, 0, 12);
                number.fontSize = number.resizeTextMaxSize = 180;
                number.fontStyle = FontStyle.Bold;
                number.alignment = TextAnchor.MiddleCenter;
                // Items cover the film-bed medallion, while the real command controls stay reachable.
                medallion.transform.SetSiblingIndex(items.GetSiblingIndex());

                fields.Update();
                foreach (var pair in bindings)
                    if (fields.FindProperty(pair.Key).objectReferenceValue != pair.Value)
                        throw new InvalidOperationException("Existing serialized binding changed: " + pair.Key);
                foreach (var pair in events)
                    if (JsonUtility.ToJson(pair.Key.onClick) != pair.Value)
                        throw new InvalidOperationException("UnityEvent changed: " + pair.Key.name);
                ProductionCombatUILayoutSetup.Validate(root);
                if (PrefabUtility.SaveAsPrefabAsset(root, ProductionCombatUILayoutSetup.PrefabPath) == null)
                    throw new InvalidOperationException("Could not save 2-D design.");
                Debug.Log("[CombatUI-2D] Reconstructed canonical prefab; original bindings and UnityEvents retained.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static CombatReferenceGraphic Shape(string name, Transform parent, CombatReferenceGraphic.Geometry shape,
            Color color, Color gradient, float start = 145, float end = 168, float inner = .24f, float skew = 24)
        {
            GameObject child = new(name, typeof(RectTransform), typeof(CombatReferenceGraphic));
            child.transform.SetParent(parent, false);
            CombatReferenceGraphic graphic = child.GetComponent<CombatReferenceGraphic>();
            Stretch(graphic.rectTransform);
            graphic.Configure(shape, color, start, end, inner, skew, gradient);
            graphic.raycastTarget = false;
            return graphic;
        }

        private static Material ReelMaterial()
        {
            const string path = "Assets/GAME/Prefabs/UI/Combat/CombatReferenceReel.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find("UI/CombatReferenceSilhouette");
            if (shader == null) throw new InvalidOperationException("Reference reel silhouette shader is not imported.");
            material = new Material(shader) { name = "CombatReferenceReel" };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void SetButtonColors(Button button)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = colors.selectedColor = colors.disabledColor = Blue;
            colors.highlightedColor = new Color32(44, 92, 255, 255);
            colors.pressedColor = new Color32(0, 35, 180, 255);
            button.colors = colors;
        }

        private static Image Image(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject child = new(name, typeof(RectTransform), typeof(Image));
            child.transform.SetParent(parent, false);
            Image image = child.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void PlaceText(Text text, Vector2 anchor, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            Place(text.rectTransform, anchor, position, size, new Vector2(anchor.x, 1));
            text.color = Color.white;
            text.fontSize = text.resizeTextMaxSize = fontSize;
            text.resizeTextMinSize = 16;
            text.alignment = alignment;
            text.raycastTarget = false;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
#endif
