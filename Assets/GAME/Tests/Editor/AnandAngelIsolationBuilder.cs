using System;
using System.IO;
using System.Linq;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Integration;
using Game.Core;
using Game.Enemies;
using Game.NonCombat.Progress;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Creates only new isolated assets. It refuses to overwrite any existing asset.
public static class AnandAngelIsolationBuilder
{
    private const string Folder = "Assets/GAME/Tests/PlayMode";
    private const string ScenePath = Folder + "/AnandAngel_IsolatedPlaytest.unity";
    private const string PrefabPath = Folder + "/Enemy_Angel_Playtest_Candidate.prefab";
    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing asset: " + path);
    private static void Ref(Object obj, string field, Object value) { var so = new SerializedObject(obj); var p = so.FindProperty(field) ?? throw new InvalidOperationException(obj.name + ": missing " + field); p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Int(Object obj, string field, int value) { var so = new SerializedObject(obj); var p = so.FindProperty(field) ?? throw new InvalidOperationException(obj.name + ": missing " + field); p.intValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static T One<T>(Scene s) where T : Component => s.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true)).Single();

    public static void Create()
    {
        if (File.Exists(ScenePath) || File.Exists(PrefabPath)) throw new InvalidOperationException("Existing isolated asset will not be overwritten.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/GAME/Tests", "PlayMode");
        var source = EditorSceneManager.OpenScene("Assets/GAME/Scenes/Dungeon_Template.unity", OpenSceneMode.Additive);
        if (!EditorSceneManager.SaveScene(source, ScenePath, true)) throw new InvalidOperationException("Unable to create scene copy.");
        EditorSceneManager.CloseScene(source, true);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var roots = scene.GetRootGameObjects();
        var encounter = roots.SelectMany(x => x.GetComponentsInChildren<CombatEncounterGroup>(true)).Single(x => x.name == "TestEncounter_01");
        var other = roots.SelectMany(x => x.GetComponentsInChildren<CombatEncounterGroup>(true)).Single(x => x.name == "TestEncounter_02");
        other.gameObject.SetActive(false);
        var oldEnemy = encounter.GetComponentsInChildren<CombatHpComponent>(true).Single().gameObject;
        var oldPatrol = oldEnemy.GetComponent<FieldEnemyPatrolAI2D>();
        if (oldPatrol == null) throw new InvalidOperationException("Template enemy patrol missing.");
        var trigger = encounter.GetComponentInChildren<CombatEncounterTrigger2D>(true);
        var players = roots.SelectMany(x => x.GetComponentsInChildren<Transform>(true)).Where(x => x.CompareTag("Player")).ToArray();
        Debug.Log("[AnandAngelPlaytest] Player-tagged objects: " + string.Join(", ", players.Select(x => x.name + " hp=" + (x.GetComponent<CombatHpComponent>() != null))));
        var player = players.First(x => x.GetComponent<CombatHpComponent>() != null).gameObject;
        var prefabRoot = Object.Instantiate(oldEnemy);
        prefabRoot.name = "Enemy_Angel_Playtest_Candidate";
        prefabRoot.SetActive(false);
        foreach (var name in new[] { "PatrolLeft", "PatrolRight" })
        {
            var point = prefabRoot.transform.Find(name);
            if (point != null) Object.DestroyImmediate(point.gameObject);
        }
        var patrol = prefabRoot.GetComponent<FieldEnemyPatrolAI2D>();
        Ref(patrol, "player", null); Ref(patrol, "leftPoint", null); Ref(patrol, "rightPoint", null);
        var prefabTrigger = prefabRoot.GetComponentInChildren<CombatEncounterTrigger2D>(true);
        var prefabTriggerData = new SerializedObject(prefabTrigger); prefabTriggerData.FindProperty("encounterId").stringValue = ""; prefabTriggerData.ApplyModifiedPropertiesWithoutUndo();
        var hp = prefabRoot.GetComponent<CombatHpComponent>(); hp.MaxHP = 14; hp.HP = 14;
        var loadout = prefabRoot.GetComponent<CombatSkillLoadoutComponent>() ?? prefabRoot.AddComponent<CombatSkillLoadoutComponent>();
        var ls = new SerializedObject(loadout); var ids = ls.FindProperty("skillIds"); ids.arraySize = 1; ids.GetArrayElementAtIndex(0).intValue = 10003; ls.ApplyModifiedPropertiesWithoutUndo();
        var enemySource = prefabRoot.GetComponent<EnemySourceComponent>() ?? prefabRoot.AddComponent<EnemySourceComponent>();
        Ref(enemySource, "definition", Load<EnemyDefinitionSO>("Assets/GAME/Data/Enemy/Enemy_Angel.asset"));
        var renderer = prefabRoot.GetComponentInChildren<SpriteRenderer>(true) ?? prefabRoot.AddComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/GAME/Image/Enemy/angel/idle_angel-Sheet.png").OfType<Sprite>().FirstOrDefault() ?? throw new InvalidOperationException("Angel sprite unavailable.");
        var animator = prefabRoot.GetComponentInChildren<Animator>(true) ?? prefabRoot.AddComponent<Animator>();
        animator.runtimeAnimatorController = Load<RuntimeAnimatorController>("Assets/GAME/Image/Enemy/angel/AngelAnimatorController.controller");
        foreach (var t in prefabRoot.GetComponentsInChildren<Transform>(true)) if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0) throw new InvalidOperationException("Missing script on prefab: " + t.name);
        var prefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath) ?? throw new InvalidOperationException("Prefab save failed.");
        Object.DestroyImmediate(prefabRoot);
        var angel = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        angel.transform.SetParent(oldEnemy.transform.parent, false); angel.transform.localPosition = oldEnemy.transform.localPosition; angel.transform.localRotation = oldEnemy.transform.localRotation; angel.transform.localScale = oldEnemy.transform.localScale;
        var oldData = new SerializedObject(oldPatrol); var newPatrol = angel.GetComponent<FieldEnemyPatrolAI2D>();
        foreach (var side in new[] { "leftPoint", "rightPoint" })
        {
            var oldPoint = oldData.FindProperty(side).objectReferenceValue as Transform;
            if (oldPoint == null) throw new InvalidOperationException("Template patrol point missing: " + side);
            var point = new GameObject("TestOnly_" + side).transform; point.SetParent(encounter.transform); point.position = oldPoint.position; Ref(newPatrol, side, point);
        }
        Ref(newPatrol, "player", player.transform);
        if (new SerializedObject(newPatrol).FindProperty("leftPoint").objectReferenceValue == null || new SerializedObject(newPatrol).FindProperty("rightPoint").objectReferenceValue == null) throw new InvalidOperationException("Scene patrol points missing.");
        oldEnemy.SetActive(false); angel.SetActive(true);
        trigger = angel.GetComponentInChildren<CombatEncounterTrigger2D>(true);
        Ref(trigger, "enemyObject", angel); Ref(trigger, "encounterGroup", encounter);
        var triggerData = new SerializedObject(trigger); triggerData.FindProperty("encounterId").stringValue = "testonly.anand_angel"; triggerData.ApplyModifiedPropertiesWithoutUndo();
        var mode = new SerializedObject(encounter); mode.FindProperty("encounterId").stringValue = "testonly.anand_angel"; mode.FindProperty("useEncounterFlowMode").boolValue = true; mode.FindProperty("encounterFlowMode").intValue = 1; mode.ApplyModifiedPropertiesWithoutUndo();
        var e = new SerializedObject(encounter); var members = e.FindProperty("enemies"); members.arraySize = 1; members.GetArrayElementAtIndex(0).objectReferenceValue = angel; e.ApplyModifiedPropertiesWithoutUndo();
        var entry = One<CombatEntryPoint>(scene);
        Ref(trigger, "entryPoint", entry);
        var basic = Load<SkillDefinitionSO>("Assets/GAME/Data/Skill/Skill_AnandBasic_PlaytestV1.asset");
        var zorn = Load<SkillDefinitionSO>("Assets/GAME/Data/Skill/Skill_Zornhau_PlaytestV1.asset");
        var prophecy = Load<SkillDefinitionSO>("Assets/GAME/Data/Skill/Skill_AngelTheProphecy_PlaytestV1.asset");
        var es = new SerializedObject(entry); var defs = es.FindProperty("skillDefinitions"); foreach (var skill in new[] { basic, zorn, prophecy }) { int i = defs.arraySize; defs.InsertArrayElementAtIndex(i); defs.GetArrayElementAtIndex(i).objectReferenceValue = skill; } es.ApplyModifiedPropertiesWithoutUndo();
        var boot = roots.SelectMany(x => x.GetComponentsInChildren<RuntimeBootstrapper>(true)).SingleOrDefault();
        if (boot == null) boot = roots.Single(x => x.name == "Runtime").AddComponent<RuntimeBootstrapper>();
        Ref(boot, "characterStartDefinition", Load<CharacterStartDefinitionSO>("Assets/GAME/Data/Character/CharacterStart_AnandDeMorte.asset"));
        var playerHp = player.GetComponent<CombatHpComponent>(); if (playerHp == null) throw new InvalidOperationException("Player HP missing."); playerHp.MaxHP = 320; playerHp.HP = 320; PrefabUtility.RecordPrefabInstancePropertyModifications(playerHp);
        var setup = new GameObject("TestOnly_AnandAngelSetup").AddComponent<AnandAngelPlaytestSetup>();
        Ref(setup, "entry", entry); Ref(setup, "basic", basic); Ref(setup, "zornhau", zorn);
        Ref(setup, "testOnlyConfig", Load<FinalCombatRuntimeConfigSO>("Assets/GAME/Tests/Editor/Fixtures/CombatPlaytestV1/AnandHand_C2_TestOnly.asset"));
        foreach (var root in scene.GetRootGameObjects()) foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0) throw new InvalidOperationException("Missing script in scene: " + t.name);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Scene save failed.");
        Debug.Log("[AnandAngelPlaytest] Created isolated scene and prefab.");
    }

    public static void RepairPlayerHp()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var hp = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<CombatHpComponent>(true)).Single(x => x.name == "PlayerRoot" && x.CompareTag("Player"));
        hp.MaxHP = 320; hp.HP = 320; PrefabUtility.RecordPrefabInstancePropertyModifications(hp);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Scene save failed.");
    }

    public static void RepairIsolation()
    {
        var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            foreach (var name in new[] { "PatrolLeft", "PatrolRight" })
            {
                var point = prefab.transform.Find(name);
                if (point != null) Object.DestroyImmediate(point.gameObject);
            }
            var trigger = prefab.GetComponentInChildren<CombatEncounterTrigger2D>(true);
            var data = new SerializedObject(trigger); data.FindProperty("encounterId").stringValue = ""; data.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath) == null) throw new InvalidOperationException("Prefab save failed.");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var groups = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<CombatEncounterGroup>(true));
        var group = groups.Single(x => x.name == "TestEncounter_01");
        var angel = group.GetComponentsInChildren<EnemySourceComponent>(true).Single(x => x.gameObject.activeSelf).gameObject;
        var old = group.GetComponentsInChildren<FieldEnemyPatrolAI2D>(true).Single(x => !x.gameObject.activeSelf);
        var oldData = new SerializedObject(old); var patrol = angel.GetComponent<FieldEnemyPatrolAI2D>();
        foreach (var side in new[] { "leftPoint", "rightPoint" })
        {
            var sourcePoint = oldData.FindProperty(side).objectReferenceValue as Transform;
            if (sourcePoint == null) throw new InvalidOperationException("Template patrol point missing: " + side);
            var point = new GameObject("TestOnly_" + side).transform; point.SetParent(group.transform); point.position = sourcePoint.position; Ref(patrol, side, point);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(patrol);
        var contact = angel.GetComponentInChildren<CombatEncounterTrigger2D>(true);
        Ref(contact, "entryPoint", One<CombatEntryPoint>(scene)); Ref(contact, "enemyObject", angel); Ref(contact, "encounterGroup", group);
        var contactData = new SerializedObject(contact); contactData.FindProperty("encounterId").stringValue = "testonly.anand_angel"; contactData.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(contact);
        var groupData = new SerializedObject(group); groupData.FindProperty("encounterId").stringValue = "testonly.anand_angel"; groupData.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Scene save failed.");
    }
}
