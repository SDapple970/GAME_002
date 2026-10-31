#if UNITY_EDITOR
using System.Reflection;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.NonCombat.Progress;
using UnityEngine;

// Isolated editor playtest injection only; this is not a production loadout or owner.
[DefaultExecutionOrder(-10000)]
public sealed class AnandAngelPlaytestSetup : MonoBehaviour
{
    [SerializeField] private CombatEntryPoint entry;
    [SerializeField] private FinalCombatRuntimeConfigSO testOnlyConfig;
    [SerializeField] private SkillDefinitionSO basic;
    [SerializeField] private SkillDefinitionSO zornhau;
    private FinalCombatRuntimeConfigSO runtimeConfig;

    private void Awake()
    {
        if (entry == null || testOnlyConfig == null || basic == null || zornhau == null)
        {
            Debug.LogError("[AnandAngelPlaytest] Missing isolated test references; combat setup is unavailable.", this);
            return;
        }
        runtimeConfig = Instantiate(testOnlyConfig);
        typeof(FinalCombatRuntimeConfigSO).GetField("enableProductionHandBp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runtimeConfig, true);
        typeof(CombatEntryPoint).GetField("finalExchangeRuntimeConfig", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(entry, runtimeConfig);
    }

    private void Start()
    {
        var runtime = CharacterSkillSaveParticipant.Instance != null ? CharacterSkillSaveParticipant.Instance.Runtime : null;
        if (runtime == null || basic == null || zornhau == null)
        {
            Debug.LogError("[AnandAngelPlaytest] Character skill runtime is unavailable.", this);
            return;
        }
        foreach (var skill in new[] { basic, zornhau })
        {
            runtime.TryAcquire("char.anand_de_morte", skill.PersistentKey);
            runtime.TryEquip("char.anand_de_morte", skill.PersistentKey);
        }
    }

    private void OnDestroy()
    {
        if (runtimeConfig != null) Destroy(runtimeConfig);
    }
}
#endif
