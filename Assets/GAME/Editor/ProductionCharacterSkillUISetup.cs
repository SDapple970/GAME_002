using System;
using System.Linq;
using Game.Combat.Core;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>Creates authored assets once, then adds scoped scene-instance overrides. Never rewrites the shared UI prefab.</summary>
    public static class ProductionCharacterSkillUISetup
    {
        public const string ScenePath = "Assets/GAME/Scenes/Dungeon_1_Production.unity";
        public const string PanelPath = "Assets/GAME/Prefabs/UI/CharacterSkillPanel.prefab";
        public const string RowPath = "Assets/GAME/Prefabs/UI/CharacterSkillRow.prefab";
        public const string CharacterButtonPath = "Assets/GAME/Prefabs/UI/CharacterSkillCharacterButton.prefab";
        public const string EntryButtonPath = "Assets/GAME/Prefabs/UI/CharacterSkillEntryButton.prefab";
        private static readonly Color Background = new(.08f, .10f, .14f, 1);
        private static readonly Color Surface = new(.14f, .17f, .23f, 1);
        private static readonly Color Accent = new(.28f, .54f, .48f, 1);
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/GAME/Fonts/DungGeunMo SDF.asset");

        [MenuItem("GAME/Production/Install Character Skill UI")]
        public static void BuildAndConnect()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Install the UI outside Play Mode.");
            if (Font == null) throw new InvalidOperationException("The existing Production TMP font is missing.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.isDirty) throw new InvalidOperationException("Save the current scene before installing Production UI.");
            EnsurePrefabs();
            if (scene.path != ScenePath)
            {
                if (scene.isDirty) throw new InvalidOperationException("Save the current scene before opening Production.");
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            Connect(scene);
            Debug.Log("[CharacterSkillUISetup] Production scene and reusable UI prefabs validated and saved.");
        }

        public static void EnsurePrefabs()
        {
            CreateOnce(RowPath, BuildRow);
            CreateOnce(CharacterButtonPath, () => ButtonObject("CharacterSkillCharacterButton", null, "캐릭터", 250, 60).gameObject);
            CreateOnce(EntryButtonPath, () =>
            {
                GameObject go = ButtonObject("CharacterSkillEntryButton", null, "스킬", 210, 64).gameObject;
                RectTransform rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
                rect.anchoredPosition = new Vector2(-40, -40);
                return go;
            });
            CreateOnce(PanelPath, BuildPanel);
            ValidatePanel(AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath));
        }

        private static void CreateOnce(string path, Func<GameObject> create)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            GameObject root = create();
            try
            {
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new InvalidOperationException($"Could not save {path}.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject BuildRow()
        {
            GameObject row = Rect("CharacterSkillRow", null).gameObject;
            Paint(row, Surface, false);
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 10, 10);
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            Layout(row, 96, -1, 0);
            TMP_Text name = Text("Name", row.transform, "필름 이름", 27);
            Layout(name.gameObject, 70, 180, 1);
            TMP_Text status = Text("Status", row.transform, "보유", 23);
            Layout(status.gameObject, 70, 142, 0);
            Button button = ButtonObject("Action", row.transform, "장착", 104, 58);
            CharacterSkillRowView view = row.AddComponent<CharacterSkillRowView>();
            Assign(view, "nameLabel", name, "statusLabel", status, "actionButton", button,
                "actionLabel", button.GetComponentInChildren<TMP_Text>());
            return row;
        }

        private static GameObject BuildPanel()
        {
            RectTransform root = Rect("CharacterSkillPanel", null);
            Stretch(root);
            Canvas canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            Paint(root.gameObject, new Color(.025f, .035f, .05f, .97f), true);
            RectTransform panel = Rect("Panel", root);
            Stretch(panel, new Vector2(60, 48), new Vector2(-60, -48));
            Paint(panel.gameObject, Background, false);

            TMP_Text title = Text("Title", panel, "스킬 관리", 42);
            Place((RectTransform)title.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -24), new Vector2(480, 64));
            Button close = ButtonObject("Close", panel, "닫기", 150, 64);
            Place((RectTransform)close.transform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-32, -24), new Vector2(150, 64));
            TMP_Text selected = Text("SelectedCharacter", panel, "선택한 캐릭터 없음", 27);
            Place((RectTransform)selected.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), new Vector2(0, -98), new Vector2(-64, 44));

            RectTransform characters = Scroll("Characters", panel, true, out RectTransform characterContent);
            Place(characters, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), new Vector2(0, -148), new Vector2(-64, 80));
            TMP_Text characterEmpty = Text("CharacterEmpty", characters, "보유한 캐릭터가 없습니다.", 27);
            Stretch((RectTransform)characterEmpty.transform, new Vector2(16, 0), new Vector2(-16, 0));

            RectTransform columns = Rect("Columns", panel);
            Stretch(columns, new Vector2(32, 120), new Vector2(-32, -252));
            HorizontalLayoutGroup group = columns.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 24; group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = group.childForceExpandHeight = true;
            RectTransform films = Column("SharedFilms", columns, "공용 필름", "획득한 필름이 없습니다.", out TMP_Text filmEmpty);
            RectTransform equipped = Column("EquippedFilms", columns, "장착한 필름", "장착한 필름이 없습니다.", out TMP_Text equippedEmpty);
            RectTransform unique = Column("UniqueSkills", columns, "고유 스킬", "등록된 고유 스킬이 없습니다.", out TMP_Text uniqueEmpty);

            TMP_Text next = Text("NextUnlock", panel, "다음 해금 정보 없음", 25);
            Place((RectTransform)next.transform, Vector2.zero, new Vector2(1, 0), new Vector2(.5f, 0), new Vector2(0, 70), new Vector2(-64, 38));
            TMP_Text feedback = Text("Feedback", panel, "캐릭터를 선택하고 필름을 관리하세요.", 27);
            feedback.color = new Color(.65f, .85f, .79f);
            Place((RectTransform)feedback.transform, Vector2.zero, new Vector2(1, 0), new Vector2(.5f, 0), new Vector2(0, 20), new Vector2(-64, 42));
            CharacterSkillPanelView view = root.gameObject.AddComponent<CharacterSkillPanelView>();
            Assign(view, "selectedCharacterLabel", selected, "feedbackLabel", feedback, "characterEmptyLabel", characterEmpty,
                "filmEmptyLabel", filmEmpty, "equippedEmptyLabel", equippedEmpty, "uniqueEmptyLabel", uniqueEmpty,
                "nextUnlockLabel", next, "characterContent", characterContent, "filmContent", films, "equippedContent", equipped,
                "uniqueContent", unique, "rowPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(RowPath).GetComponent<CharacterSkillRowView>(),
                "characterButtonPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(CharacterButtonPath).GetComponent<Button>(), "closeButton", close);
            UnityEventTools.AddPersistentListener(close.onClick, view.RequestClose);
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        private static RectTransform Column(string name, Transform parent, string title, string emptyText, out TMP_Text empty)
        {
            RectTransform column = Rect(name, parent);
            Paint(column.gameObject, Surface, false);
            LayoutElement sizing = column.gameObject.AddComponent<LayoutElement>();
            sizing.minWidth = 0; sizing.flexibleWidth = 1;
            TMP_Text heading = Text("Heading", column, title, 31);
            Place((RectTransform)heading.transform, new Vector2(0, 1), Vector2.one, new Vector2(.5f, 1), new Vector2(0, -16), new Vector2(-32, 52));
            RectTransform scroll = Scroll("Scroll", column, false, out RectTransform content);
            Stretch(scroll, new Vector2(12, 12), new Vector2(-12, -80));
            empty = Text("Empty", column, emptyText, 26);
            empty.alignment = TextAlignmentOptions.Center;
            Stretch((RectTransform)empty.transform, new Vector2(24, 24), new Vector2(-24, -100));
            return content;
        }

        private static RectTransform Scroll(string name, Transform parent, bool horizontal, out RectTransform content)
        {
            RectTransform root = Rect(name, parent);
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = horizontal; scroll.vertical = !horizontal;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            RectTransform viewport = Rect("Viewport", root);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            Paint(viewport.gameObject, new Color(0, 0, 0, .08f), true);
            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = horizontal ? new Vector2(0, 1) : Vector2.one;
            content.pivot = new Vector2(0, 1); content.sizeDelta = Vector2.zero;
            HorizontalOrVerticalLayoutGroup group = horizontal ? content.gameObject.AddComponent<HorizontalLayoutGroup>() : content.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = 12; group.padding = new RectOffset(4, 4, 4, 4);
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = !horizontal; group.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = horizontal ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = horizontal ? ContentSizeFitter.FitMode.Unconstrained : ContentSizeFitter.FitMode.PreferredSize;
            if (horizontal) content.sizeDelta = new Vector2(0, 72);
            scroll.viewport = viewport; scroll.content = content;
            return root;
        }

        private static Button ButtonObject(string name, Transform parent, string label, float width, float height)
        {
            RectTransform rect = Rect(name, parent);
            rect.sizeDelta = new Vector2(width, height);
            Image image = Paint(rect.gameObject, Accent, true);
            Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            Layout(rect.gameObject, height, width, 0);
            TMP_Text text = Text("Label", rect, label, 27); text.alignment = TextAlignmentOptions.Center;
            Stretch((RectTransform)text.transform, new Vector2(8, 4), new Vector2(-8, -4));
            return button;
        }

        private static TMP_Text Text(string name, Transform parent, string value, float size)
        {
            RectTransform rect = Rect(name, parent);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Font; text.text = value; text.fontSize = size;
            text.enableAutoSizing = true; text.fontSizeMin = size - 4; text.fontSizeMax = size;
            text.color = new Color(.9f, .93f, .96f); text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            GameObject go = new(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image Paint(GameObject go, Color color, bool raycast)
        {
            Image image = go.AddComponent<Image>(); image.color = color; image.raycastTarget = raycast;
            return image;
        }

        private static void Layout(GameObject go, float height, float width, float flexible)
        {
            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = height; layout.preferredWidth = width;
            layout.minWidth = width < 0 ? 0 : width; layout.flexibleWidth = flexible;
        }

        private static void Stretch(RectTransform rect, Vector2 min = default, Vector2 max = default)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = min; rect.offsetMax = max;
        }

        private static void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }

        public static void Assign(UnityEngine.Object target, params object[] fields)
        {
            SerializedObject serialized = new(target);
            for (int i = 0; i < fields.Length; i += 2)
            {
                SerializedProperty property = serialized.FindProperty((string)fields[i]);
                if (property == null) throw new InvalidOperationException($"Missing serialized field {fields[i]} on {target}.");
                property.objectReferenceValue = (UnityEngine.Object)fields[i + 1];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        private static void Connect(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            GameUIRootController owner = Single<GameUIRootController>(roots);
            UIScreenRouter router = Single<UIScreenRouter>(roots);
            CombatEntryPoint entry = Single<CombatEntryPoint>(roots);
            if (roots.SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Count() != 1)
                throw new InvalidOperationException("Production must retain exactly one EventSystem.");
            GameObject field = (GameObject)new SerializedObject(owner).FindProperty("fieldRoot").objectReferenceValue;
            Canvas canvas = field.GetComponentsInChildren<Canvas>(true).Single(item => item.name == "InteractionPromptHost");
            CharacterSkillUIHost[] hosts = owner.GetComponents<CharacterSkillUIHost>();
            if (hosts.Length > 1) throw new InvalidOperationException("Duplicate CharacterSkillUIHost.");
            if (hosts.Length == 1)
            {
                ValidateScene(scene);
                return; // Existing authored layout/references are never overwritten on repeat installation.
            }
            if (owner.HasCharacterSkillRoot || owner.GetComponentsInChildren<CharacterSkillPanelView>(true).Length != 0 ||
                canvas.transform.Find("CharacterSkillEntryButton") != null)
                throw new InvalidOperationException("Partial/custom skill UI found. Reconnect it explicitly; installation will not overwrite it.");

            if (!owner.ValidateRootGraph(false)) throw new InvalidOperationException("Fix the existing global root graph before installing UI.");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Install Production Character Skill UI");
            try
            {
                GameObject panel = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath), owner.transform);
                Undo.RegisterCreatedObjectUndo(panel, "Add skill panel");
                GameObject button = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EntryButtonPath), canvas.transform);
                Undo.RegisterCreatedObjectUndo(button, "Add skill entry");
                if (canvas.GetComponent<GraphicRaycaster>() == null) Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);
                CharacterSkillUIHost host = Undo.AddComponent<CharacterSkillUIHost>(owner.gameObject);
                Undo.RecordObject(owner, "Connect routed skill root");
                Assign(owner, "characterSkillRoot", panel);
                Assign(host, "router", router, "view", panel.GetComponent<CharacterSkillPanelView>(), "openButton", button.GetComponent<Button>(), "entryPoint", entry);
                UnityEventTools.AddPersistentListener(button.GetComponent<Button>().onClick, router.OpenCharacterSkills);
                PrefabUtility.RecordPrefabInstancePropertyModifications(button.GetComponent<Button>());
                ValidateScene(scene);
                Undo.FlushUndoRecordObjects();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Production scene save failed.");
                Undo.CollapseUndoOperations(group);
            }
            catch
            {
                Undo.FlushUndoRecordObjects();
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        private static T Single<T>(GameObject[] roots) where T : Component => roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();

        public static void ValidatePanel(GameObject panel)
        {
            if (panel == null || panel.GetComponent<CharacterSkillPanelView>() == null || panel.GetComponent<Canvas>() == null ||
                panel.GetComponent<CanvasScaler>() == null || panel.GetComponent<GraphicRaycaster>() == null ||
                panel.GetComponentsInChildren<ScrollRect>(true).Length != 4 || panel.GetComponentsInChildren<EventSystem>(true).Length != 0)
                throw new InvalidOperationException("Character skill panel prefab structure is incomplete.");
            SerializedObject view = new(panel.GetComponent<CharacterSkillPanelView>());
            SerializedProperty field = view.GetIterator();
            while (field.NextVisible(true))
                if (field.propertyType == SerializedPropertyType.ObjectReference && field.objectReferenceValue == null)
                    throw new InvalidOperationException($"Panel reference is missing: {field.propertyPath}.");
        }

        public static void ValidateScene(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            GameUIRootController owner = Single<GameUIRootController>(roots);
            UIScreenRouter router = Single<UIScreenRouter>(roots);
            CharacterSkillUIHost host = Single<CharacterSkillUIHost>(roots);
            CharacterSkillPanelView view = Single<CharacterSkillPanelView>(roots);
            if (!owner.HasCharacterSkillRoot || !owner.ValidateRootGraph(false) || !view.transform.IsChildOf(owner.transform))
                throw new InvalidOperationException("Skill root is not routed independently under the Production UI owner.");
            SerializedObject data = new(host);
            if (data.FindProperty("router").objectReferenceValue != router || data.FindProperty("view").objectReferenceValue != view ||
                data.FindProperty("entryPoint").objectReferenceValue != Single<CombatEntryPoint>(roots))
                throw new InvalidOperationException("Host is not connected to the existing Production owners.");
            Button entry = (Button)data.FindProperty("openButton").objectReferenceValue;
            if (entry == null || entry.onClick.GetPersistentEventCount() != 1 || entry.onClick.GetPersistentTarget(0) != router ||
                entry.onClick.GetPersistentMethodName(0) != nameof(UIScreenRouter.OpenCharacterSkills))
                throw new InvalidOperationException("Serialized entry-button navigation is missing.");
            ValidatePanel(view.gameObject);
        }
    }
}
