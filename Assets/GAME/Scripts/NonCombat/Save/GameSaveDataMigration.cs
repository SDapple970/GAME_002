using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.NonCombat.Save
{
    internal static class GameSaveDataMigrator
    {
        internal static bool TryMigrate(string json, out GameSaveData data, out bool legacy, out string error)
        {
            data = null;
            legacy = false;
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Save JSON is empty.";
                return false;
            }

            bool canonical = json.Contains("\"header\"") || json.Contains("\"formatId\"");
            if (canonical)
            {
                if (!SaveSerializer.TryFromGameSaveJson(json, out data))
                {
                    error = "Canonical save JSON could not be parsed.";
                    return false;
                }

                int version = data.header != null ? data.header.schemaVersion : 1;
                if (version > GameSaveDataFormat.CurrentSchemaVersion)
                {
                    error = $"Unsupported future schema version {version}.";
                    return false;
                }

                if (version <= 1)
                    MigrateVersion1(data);
            }
            else
            {
                SaveData old;
                try { old = JsonUtility.FromJson<SaveData>(json); }
                catch (Exception exception) { error = $"Legacy save JSON could not be parsed: {exception.Message}"; return false; }
                if (old == null)
                {
                    error = "Legacy save JSON produced no data.";
                    return false;
                }

                data = FromLegacy(old);
                legacy = true;
            }

            GameSaveDataValidator.Normalize(data);
            return GameSaveDataValidator.TryValidate(data, out error);
        }

        private static void MigrateVersion1(GameSaveData data)
        {
            data.header ??= new SaveHeaderData();
            data.header.formatId = GameSaveDataFormat.FormatId;
            data.header.schemaVersion = GameSaveDataFormat.CurrentSchemaVersion;
            if (data.quest?.quests != null)
            {
                foreach (QuestStateSaveData quest in data.quest.quests)
                {
                    if (quest == null || !string.IsNullOrWhiteSpace(quest.status)) continue;
                    bool hasProgress = quest.objectives != null && quest.objectives.Exists(item => item != null && item.progress > 0);
                    quest.status = quest.completed ? "Completed" : hasProgress ? "Active" : "Inactive";
                }
            }
        }

        private static GameSaveData FromLegacy(SaveData old)
        {
            GameSaveData data = new();
            data.header.activeSceneId = string.Empty;
            data.currency.gold = Mathf.Max(0, old.gold);
            data.futureDaily.currentChapterId = old.currentChapterId;
            data.location.hasPositionFallback = true;
            data.location.positionX = old.playerPosition.x;
            data.location.positionY = old.playerPosition.y;
            data.location.positionZ = old.playerPosition.z;

            if (old.inventory != null)
                foreach (IntEntry entry in old.inventory)
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.id) && entry.value > 0)
                        data.inventory.items.Add(new SaveIntEntry { id = entry.id, value = entry.value });
            if (old.flags != null)
                foreach (BoolEntry entry in old.flags)
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.id))
                        data.story.flags.Add(new SaveBoolEntry { id = entry.id, value = entry.value });
            if (old.personaStats != null)
                foreach (PersonaStatEntry entry in old.personaStats)
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.stat))
                        data.progression.personaStats.Add(new PersonaStatSaveData { stat = entry.stat, level = entry.level, xp = entry.xp });
            if (old.completedObjectives != null)
                data.progression.completedObjectiveIds.AddRange(old.completedObjectives);
            return data;
        }
    }

    internal static class GameSaveDataValidator
    {
        internal const int MaxLedgerEntries = 256;

        internal static void Normalize(GameSaveData data)
        {
            if (data == null) return;
            data.header ??= new SaveHeaderData(); data.quest ??= new QuestSaveData(); data.inventory ??= new InventorySaveData();
            data.currency ??= new CurrencySaveData(); data.party ??= new PartySaveData(); data.progression ??= new ProgressionSaveData();
            data.demoMission ??= new DemoMissionSaveData(); data.futureDaily ??= new FutureDailySaveData(); data.story ??= new StorySaveData();
            data.reward ??= new RewardSaveData(); data.world ??= new WorldSaveData(); data.location ??= new PlayerLocationSaveData();
            data.currency.gold = Mathf.Max(0, data.currency.gold);
            NormalizeIntEntries(data.inventory.items);
            NormalizeStrings(data.story.completedEventIds, MaxLedgerEntries);
            NormalizeStrings(data.world.clearedEncounterIds, MaxLedgerEntries);
            if (data.quest.quests != null)
                foreach (QuestStateSaveData quest in data.quest.quests)
                    if (quest != null) NormalizeStrings(quest.processedEventIds, MaxLedgerEntries);
        }

        internal static bool TryValidate(GameSaveData data, out string error)
        {
            error = null;
            if (data?.header == null) { error = "Missing save header."; return false; }
            if (data.header.formatId != GameSaveDataFormat.FormatId) { error = $"Unsupported format '{data.header.formatId}'."; return false; }
            if (data.header.schemaVersion != GameSaveDataFormat.CurrentSchemaVersion) { error = $"Unsupported schema {data.header.schemaVersion}."; return false; }
            if (data.location != null && (!float.IsFinite(data.location.positionX) || !float.IsFinite(data.location.positionY) || !float.IsFinite(data.location.positionZ)))
            { error = "Player location contains a non-finite value."; return false; }
            return true;
        }

        private static void NormalizeIntEntries(List<SaveIntEntry> entries)
        {
            if (entries == null) return;
            Dictionary<string, long> merged = new(StringComparer.Ordinal);
            foreach (SaveIntEntry entry in entries)
                if (entry != null && !string.IsNullOrWhiteSpace(entry.id) && entry.value > 0)
                    merged[entry.id] = Math.Min(int.MaxValue, merged.GetValueOrDefault(entry.id) + (long)entry.value);
            entries.Clear();
            foreach (KeyValuePair<string, long> pair in merged)
                entries.Add(new SaveIntEntry { id = pair.Key, value = (int)pair.Value });
            entries.Sort((left, right) => string.CompareOrdinal(left.id, right.id));
        }

        private static void NormalizeStrings(List<string> values, int maximum)
        {
            if (values == null) return;
            HashSet<string> unique = new(StringComparer.Ordinal);
            values.RemoveAll(value => string.IsNullOrWhiteSpace(value) || !unique.Add(value));
            values.Sort(StringComparer.Ordinal);
            if (values.Count > maximum) values.RemoveRange(maximum, values.Count - maximum);
        }
    }
}
