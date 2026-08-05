using System;
using Game.NonCombat.Save;
using UnityEngine;

namespace Game.Systems.Persona
{
    /// <summary>Canonical save bridge for the separate social/persona progression domain.</summary>
    public sealed class PersonaSaveAdapter : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer
    {
        [SerializeField] private PersonaStatusManager personaStatusManager;

        public void CaptureSaveData(GameSaveData saveData)
        {
            PersonaStatusManager persona = personaStatusManager != null ? personaStatusManager : PersonaStatusManager.Instance;
            if (saveData == null || persona == null) return;
            saveData.progression ??= new ProgressionSaveData();
            saveData.progression.personaStats.Clear();
            foreach (PersonaStat stat in Enum.GetValues(typeof(PersonaStat)))
                saveData.progression.personaStats.Add(new PersonaStatSaveData { stat = stat.ToString(), level = persona.GetLevel(stat), xp = persona.GetXp(stat) });
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            PersonaStatusManager persona = personaStatusManager != null ? personaStatusManager : PersonaStatusManager.Instance;
            if (persona == null || saveData?.progression?.personaStats == null) return;
            foreach (PersonaStatSaveData entry in saveData.progression.personaStats)
                if (entry != null && Enum.TryParse(entry.stat, out PersonaStat stat)) persona.SetStat(stat, entry.level, entry.xp);
        }
    }
}
