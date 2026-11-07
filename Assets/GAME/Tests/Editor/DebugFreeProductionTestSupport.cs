using System;
using System.Linq;
using System.Reflection;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Debugging;
using Game.Combat.Model;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.Tests.Integration
{
    // Editor tests only. Never save a source scene or apply these instance changes to its prefab.
    internal static class DebugFreeProductionTestSupport
    {
        internal const string ConfigPath = "Assets/GAME/Data/Combat/Runtime/FinalCombatRuntimeConfig_AnandStart_PlaytestV1_Candidate.asset";

        internal static int RemoveDebug(Scene scene)
        {
            int count = 0;
            foreach (var component in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray())
                if (component is CombatBpValidationOptIn || component is CombatHandValidationOptIn || component is CombatClashSeriesValidationOptIn)
                {
                    Object.DestroyImmediate(component);
                    count++;
                }
            AssertClean(scene);
            return count;
        }

        internal static void AssertClean(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero, transform.name);
                    foreach (var component in transform.GetComponents<Component>())
                    {
                        Assert.That(component, Is.Not.Null, transform.name);
                        if (component is MonoBehaviour behaviour)
                            Assert.That(behaviour.GetType().Namespace?.Contains("Debugging") ?? false, Is.False, behaviour.GetType().FullName);
                        var iterator = new SerializedObject(component).GetIterator();
                        while (iterator.Next(true))
                            if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue == null)
                                Assert.That(iterator.objectReferenceInstanceIDValue, Is.Zero, transform.name + "." + iterator.propertyPath);
                    }
                }
        }

        internal static FinalCombatRuntimeConfigSO EnableCandidate(CombatEntryPoint entry)
        {
            var source = AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(ConfigPath);
            Assert.That(source.EnableProductionHandBp, Is.True, "The approved Production profile is active; fixtures still use an isolated copy.");
            var config = Object.Instantiate(source);
            config.name = "TestOnly.DebugFree." + source.name;
            Set(config, "enableProductionHandBp", true);
            Set(entry, "finalExchangeRuntimeConfig", config);
            return config;
        }

        internal static void Set(Object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(info, Is.Not.Null, target.GetType().Name + "." + field);
            info.SetValue(target, value);
        }

        internal static object Call(object target, string method, params object[] args)
        {
            var info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(info, Is.Not.Null, method);
            return info.Invoke(target, args);
        }
    }
}
