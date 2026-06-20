using System;
using System.Collections.Generic;
using System.IO;
using Game.NonCombat.Chapter;
using Game.NonCombat.Inventory;
using Game.NonCombat.Progress;
using Game.Systems.Persona;
using UnityEngine;

namespace Game.NonCombat.Save
{
    public sealed class SaveManager : MonoBehaviour
    {
        [SerializeField] private StoryFlagDatabase storyFlags;
        [SerializeField] private CurrencyWallet currencyWallet;
        [SerializeField] private InventoryService inventoryService;
        [SerializeField] private NonCombatChapterProgressManager chapterProgressManager;
        [SerializeField] private PersonaStatusManager personaStatusManager;
        [SerializeField] private Transform player;

        private string SavePath => Path.Combine(Application.persistentDataPath, "game_save.json");

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.F5))
                Save();

            if (UnityEngine.Input.GetKeyDown(KeyCode.F6))
                Load();
        }

        public void Save()
        {
            SaveData data = BuildSaveData();
            string json = SaveSerializer.ToJson(data);
            File.WriteAllText(SavePath, json);
            Debug.Log($"[SaveManager] Saved: {SavePath}", this);
        }

        public void Load()
        {
            if (!File.Exists(SavePath))
            {
                Debug.LogWarning($"[SaveManager] Save file not found: {SavePath}", this);
                return;
            }

            string json = File.ReadAllText(SavePath);
            ApplySaveData(SaveSerializer.FromJson(json));
            Debug.Log($"[SaveManager] Loaded: {SavePath}", this);
        }

        private SaveData BuildSaveData()
        {
            SaveData data = new();

            StoryFlagDatabase flags = storyFlags != null ? storyFlags : StoryFlagDatabase.Instance;
            CurrencyWallet wallet = currencyWallet != null ? currencyWallet : CurrencyWallet.Instance;
            InventoryService inventory = inventoryService != null ? inventoryService : InventoryService.Instance;
            NonCombatChapterProgressManager chapter = chapterProgressManager != null ? chapterProgressManager : NonCombatChapterProgressManager.Instance;
            PersonaStatusManager persona = personaStatusManager != null ? personaStatusManager : PersonaStatusManager.Instance;

            data.currentChapterId = chapter != null ? chapter.CurrentChapterId : string.Empty;
            data.gold = wallet != null ? wallet.Gold : 0;
            data.completedObjectives = chapter != null ? chapter.ExportCompletedObjectives() : new List<string>();
            data.playerPosition = player != null ? player.position : Vector3.zero;

            if (flags != null)
            {
                foreach (KeyValuePair<string, bool> pair in flags.ExportFlags())
                    data.flags.Add(new BoolEntry { id = pair.Key, value = pair.Value });
            }

            if (inventory != null)
            {
                foreach (KeyValuePair<string, int> pair in inventory.ExportItems())
                    data.inventory.Add(new IntEntry { id = pair.Key, value = pair.Value });
            }

            if (persona != null)
            {
                foreach (PersonaStat stat in Enum.GetValues(typeof(PersonaStat)))
                {
                    data.personaStats.Add(new PersonaStatEntry
                    {
                        stat = stat.ToString(),
                        level = persona.GetLevel(stat),
                        xp = persona.GetXp(stat)
                    });
                }
            }

            return data;
        }

        private void ApplySaveData(SaveData data)
        {
            if (data == null) return;

            StoryFlagDatabase flags = storyFlags != null ? storyFlags : StoryFlagDatabase.Instance;
            CurrencyWallet wallet = currencyWallet != null ? currencyWallet : CurrencyWallet.Instance;
            InventoryService inventory = inventoryService != null ? inventoryService : InventoryService.Instance;
            NonCombatChapterProgressManager chapter = chapterProgressManager != null ? chapterProgressManager : NonCombatChapterProgressManager.Instance;
            PersonaStatusManager persona = personaStatusManager != null ? personaStatusManager : PersonaStatusManager.Instance;

            if (flags != null)
            {
                Dictionary<string, bool> flagMap = new();
                if (data.flags != null)
                {
                    for (int i = 0; i < data.flags.Count; i++)
                    {
                        BoolEntry entry = data.flags[i];
                        if (entry != null && !string.IsNullOrEmpty(entry.id))
                            flagMap[entry.id] = entry.value;
                    }
                }

                flags.ImportFlags(flagMap);
            }

            wallet?.SetGold(data.gold);

            if (inventory != null)
            {
                Dictionary<string, int> itemMap = new();
                if (data.inventory != null)
                {
                    for (int i = 0; i < data.inventory.Count; i++)
                    {
                        IntEntry entry = data.inventory[i];
                        if (entry != null && !string.IsNullOrEmpty(entry.id))
                            itemMap[entry.id] = entry.value;
                    }
                }

                inventory.ImportItems(itemMap);
            }

            if (chapter != null)
                chapter.Import(data.currentChapterId, data.completedObjectives);

            if (persona != null)
            {
                if (data.personaStats != null)
                {
                    for (int i = 0; i < data.personaStats.Count; i++)
                    {
                        PersonaStatEntry entry = data.personaStats[i];
                        if (entry != null && Enum.TryParse(entry.stat, out PersonaStat stat))
                            persona.SetStat(stat, entry.level, entry.xp);
                    }
                }
            }

            if (player != null)
                player.position = data.playerPosition;
        }
    }
}
