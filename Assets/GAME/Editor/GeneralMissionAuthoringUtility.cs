using System;
using System.Linq;
using Game.Quest;
using Game.UI;
using Game.World;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    public static class GeneralMissionAuthoringUtility
    {
        public const string Output = "Assets/GAME/Data/GeneralMissions";

        [MenuItem("GAME/General Missions/Create Unplaced Starter Kit")]
        public static void CreateStarterKit()
        {
            EnsureFolder(Output);
            QuestDefinitionSO[] quests =
            {
                CreateMission("CH01_General_Explore", "구역 탐사", GeneralMissionKind.Exploration,
                    Enumerable.Range(1, 100).Select(i => $"survey_{i:000}").ToArray()),
                CreateMission("CH01_General_Clear", "지정 구역 소탕", GeneralMissionKind.Extermination,
                    new[] { "encounter_01", "encounter_02", "encounter_03" }),
                CreateMission("CH01_General_Suppress", "억제 장치 가동", GeneralMissionKind.Suppressors,
                    Enumerable.Range(1, 6).Select(i => $"suppressor_{i:00}").ToArray()),
                CreateMission("CH01_General_Rescue", "요구조자 구출", GeneralMissionKind.Rescue, Array.Empty<string>())
            };
            CreatePrefab<GeneralMissionSuppressor>("Suppressor", go => go.AddComponent<BoxCollider2D>().isTrigger = true);
            CreatePrefab<GeneralMissionAreaTrigger>("SurveyCell", go => go.AddComponent<BoxCollider2D>().isTrigger = true);
            CreateExitPrefab();
            CreatePrefab<GeneralMissionEscortHazard>("EscortHazard", go => go.AddComponent<BoxCollider2D>().isTrigger = true);
            CreatePrefab<GeneralMissionEscortActor>("RescueEscort", go =>
            {
                Rigidbody2D body = go.AddComponent<Rigidbody2D>();
                body.gravityScale = 0; body.freezeRotation = true;
                go.AddComponent<CircleCollider2D>().radius = 0.3f;
                GameObject range = new("InteractionRange"); range.transform.SetParent(go.transform, false);
                range.AddComponent<CircleCollider2D>().isTrigger = true;
            });
            CreateBoardPrefab(quests);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GeneralMissions] Unplaced definitions, actors and board prefab ready at {Output}. Existing assets preserved.");
        }

        private static QuestDefinitionSO CreateMission(string id, string title, GeneralMissionKind kind, string[] targets)
        {
            string path = $"{Output}/{id}.asset";
            QuestDefinitionSO existing = AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(path);
            if (existing != null) return existing;
            QuestDefinitionSO quest = ScriptableObject.CreateInstance<QuestDefinitionSO>();
            SerializedObject data = new(quest);
            data.FindProperty("questId").stringValue = id;
            data.FindProperty("questTitle").stringValue = title;
            data.FindProperty("description").stringValue = "챕터1 반복 일반임무. 배치할 오브젝트의 고정 ID를 대상 목록과 일치시키세요.";
            data.FindProperty("repeatable").boolValue = true;
            data.FindProperty("missionDayCost").intValue = 1;
            data.FindProperty("retryPolicy").intValue = (int)QuestRetryPolicy.RestartFromBeginning;
            data.FindProperty("category").intValue = (int)QuestCategory.Side;
            SerializedProperty rules = data.FindProperty("generalMission");
            rules.FindPropertyRelative("kind").intValue = (int)kind;
            rules.FindPropertyRelative("objectiveId").stringValue = "mission";
            rules.FindPropertyRelative("rescueTargetId").stringValue = "rescue_target";
            rules.FindPropertyRelative("exitId").stringValue = "rescue_exit";
            rules.FindPropertyRelative("rescueMaxHp").intValue = 100;
            SerializedProperty ids = rules.FindPropertyRelative("targetIds"); ids.arraySize = targets.Length;
            for (int i = 0; i < targets.Length; i++) ids.GetArrayElementAtIndex(i).stringValue = targets[i];
            SerializedProperty objectives = data.FindProperty("objectives"); objectives.arraySize = 1;
            SerializedProperty objective = objectives.GetArrayElementAtIndex(0);
            objective.FindPropertyRelative("objectiveId").stringValue = "mission";
            objective.FindPropertyRelative("eventType").intValue = (int)(kind switch
            { GeneralMissionKind.Exploration => QuestEventType.Explore, GeneralMissionKind.Extermination => QuestEventType.ClearEncounter,
                GeneralMissionKind.Suppressors => QuestEventType.Interact, _ => QuestEventType.Escort });
            objective.FindPropertyRelative("requiredCount").intValue = kind == GeneralMissionKind.Exploration ? 90 :
                kind == GeneralMissionKind.Extermination ? targets.Length : kind == GeneralMissionKind.Suppressors ? 3 : 1;
            objective.FindPropertyRelative("description").stringValue = kind switch
            { GeneralMissionKind.Exploration => "지정 구역 90% 이상 탐사", GeneralMissionKind.Extermination => "지정 조우 모두 소탕",
                GeneralMissionKind.Suppressors => "선정된 억제 장치 3개 가동", _ => "요구조자와 HP 80% 이상으로 탈출" };
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(quest, path);
            return quest;
        }

        private static void CreatePrefab<T>(string name, Action<GameObject> setup) where T : Component
        {
            string path = $"{Output}/{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            GameObject go = new(name);
            try { setup(go); go.AddComponent<T>(); PrefabUtility.SaveAsPrefabAsset(go, path); }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static void CreateBoardPrefab(QuestDefinitionSO[] quests)
        {
            string path = $"{Output}/GeneralMissionBoard.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            GameObject root = new("GeneralMissionBoard", typeof(RectTransform));
            try
            {
                RectTransform rect = root.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(760, 400);
                GeneralMissionBoard board = root.AddComponent<GeneralMissionBoard>();
                SerializedObject boardData = new(board);
                SerializedProperty list = boardData.FindProperty("missions"); list.arraySize = quests.Length;
                for (int i = 0; i < quests.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = quests[i];
                boardData.ApplyModifiedPropertiesWithoutUndo();
                for (int i = 0; i < quests.Length; i++)
                {
                    GameObject row = new($"Mission_{i + 1}", typeof(RectTransform), typeof(Image), typeof(Button));
                    row.transform.SetParent(root.transform, false);
                    RectTransform rowRect = row.GetComponent<RectTransform>();
                    rowRect.anchorMin = rowRect.anchorMax = new Vector2(0.5f, 1);
                    rowRect.sizeDelta = new Vector2(720, 76); rowRect.anchoredPosition = new Vector2(0, -50 - i * 92);
                    row.GetComponent<Image>().color = new Color(0.1f, 0.14f, 0.22f, 0.95f);
                    GameObject label = new("Status", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(row.transform, false);
                    RectTransform labelRect = label.GetComponent<RectTransform>(); labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
                    labelRect.offsetMin = new Vector2(16, 8); labelRect.offsetMax = new Vector2(-16, -8);
                    TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
                    TMP_FontAsset projectFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/GAME/Fonts/DungGeunMo SDF.asset");
                    if (projectFont != null) text.font = projectFont;
                    text.fontSize = 20; text.raycastTarget = false; text.text = quests[i].QuestTitle;
                    GeneralMissionBoardRow view = row.AddComponent<GeneralMissionBoardRow>();
                    SerializedObject viewData = new(view); viewData.FindProperty("board").objectReferenceValue = board;
                    viewData.FindProperty("missionIndex").intValue = i; viewData.FindProperty("statusText").objectReferenceValue = text;
                    viewData.FindProperty("acceptButton").objectReferenceValue = row.GetComponent<Button>(); viewData.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void CreateExitPrefab()
        {
            string path = $"{Output}/RescueExit.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            GameObject go = new("RescueExit");
            try
            {
                go.AddComponent<BoxCollider2D>().isTrigger = true;
                GeneralMissionAreaTrigger area = go.AddComponent<GeneralMissionAreaTrigger>();
                SerializedObject data = new(area);
                data.FindProperty("role").intValue = (int)GeneralMissionAreaTrigger.AreaRole.RescueExit;
                data.FindProperty("areaId").stringValue = "rescue_exit";
                data.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [MenuItem("GAME/General Missions/Use Project Korean Font On Starter Board")]
        public static void UseProjectFontOnStarterBoard()
        {
            string path = $"{Output}/GeneralMissionBoard.prefab";
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/GAME/Fonts/DungGeunMo SDF.asset");
            if (font == null || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) text.font = font;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void EnsureFolder(string path)
        {
            int slash = path.LastIndexOf('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
