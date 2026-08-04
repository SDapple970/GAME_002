using System;
using System.Collections.Generic;
using Game.Common.Identity;
using Game.Quest;
using Game.Reward;
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
                if (version <= 2)
                    MigrateVersion2(data);
                if (version <= 3)
                    MigrateVersion3(data);
                if (version <= 4)
                    MigrateVersion4(data);
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

            if (!GameSaveDataValidator.TryValidateCollectionSizes(data, out error))
                return false;

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

        private static void MigrateVersion2(GameSaveData data)
        {
            data.header ??= new SaveHeaderData();
            data.reward ??= new RewardSaveData();
            data.reward.ledger ??= new List<RewardLedgerSaveData>();
            if (data.reward.combatLedger != null)
            {
                for (int i = 0; i < data.reward.combatLedger.Count; i++)
                {
                    RewardLedgerSaveData oldEntry = data.reward.combatLedger[i];
                    if (oldEntry == null)
                        continue;

                    RewardLedgerSaveData migrated = oldEntry.Clone();
                    migrated.sourceType = RewardSourceType.Combat.ToString();
                    migrated.requestedGold = Mathf.Max(migrated.requestedGold, migrated.gold);
                    migrated.requestedExp = Mathf.Max(migrated.requestedExp, migrated.exp);
                    migrated.requestedItemId = string.IsNullOrWhiteSpace(migrated.requestedItemId)
                        ? migrated.itemId
                        : migrated.requestedItemId;
                    migrated.requestedItemCount = Mathf.Max(migrated.requestedItemCount, migrated.itemCount);
                    migrated.exp = 0;
                    migrated.partialFailure = migrated.partialFailure || migrated.requestedExp > 0;
                    data.reward.ledger.Add(migrated);
                }
            }
            data.header.schemaVersion = GameSaveDataFormat.CurrentSchemaVersion;
        }

        private static void MigrateVersion3(GameSaveData data)
        {
            data.header ??= new SaveHeaderData();
            if (data.quest?.quests != null)
            {
                for (int i = 0; i < data.quest.quests.Count; i++)
                {
                    QuestStateSaveData quest = data.quest.quests[i];
                    if (quest == null)
                        continue;

                    quest.activeGroupIndex = 0;
                    quest.attempt = string.Equals(quest.status, QuestStatus.Inactive.ToString(), StringComparison.Ordinal)
                        ? 0
                        : 1;
                    quest.failureReasonId = null;
                }
            }
            data.header.schemaVersion = GameSaveDataFormat.CurrentSchemaVersion;
        }

        private static void MigrateVersion4(GameSaveData data)
        {
            data.header ??= new SaveHeaderData();
            data.world ??= new WorldSaveData();
            data.world.interactions ??= new List<InteractionStateSaveData>();
            data.header.schemaVersion = GameSaveDataFormat.CurrentSchemaVersion;
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
        internal const int MaxGeneralIdEntries = 256;
        // Valid canonical identities are never truncated. Reject an implausibly large
        // external snapshot before normalization so load failure is explicit instead.
        internal const int MaxTotalIdentityRecords = 100000;

        internal static bool TryValidateCollectionSizes(GameSaveData data, out string error)
        {
            long identityRecords = Math.Max(
                data?.reward?.ledger?.Count ?? 0,
                data?.reward?.combatLedger?.Count ?? 0);
            if (data?.quest?.quests != null)
            {
                for (int i = 0; i < data.quest.quests.Count; i++)
                {
                    identityRecords += data.quest.quests[i]?.processedEventIds?.Count ?? 0;
                    identityRecords += data.quest.quests[i]?.retiredEventIds?.Count ?? 0;
                }
            }

            identityRecords += data?.world?.interactions?.Count ?? 0;
            if (data?.world?.interactions != null)
            {
                for (int i = 0; i < data.world.interactions.Count; i++)
                    identityRecords += data.world.interactions[i]?.resolvedOutcomes?.Count ?? 0;
            }

            if (identityRecords > MaxTotalIdentityRecords)
            {
                error = $"Save contains {identityRecords} identity records; maximum supported is {MaxTotalIdentityRecords}.";
                return false;
            }

            error = null;
            return true;
        }

        internal static void Normalize(GameSaveData data)
        {
            if (data == null) return;
            data.header ??= new SaveHeaderData(); data.quest ??= new QuestSaveData(); data.inventory ??= new InventorySaveData();
            data.currency ??= new CurrencySaveData(); data.party ??= new PartySaveData(); data.progression ??= new ProgressionSaveData();
            data.demoMission ??= new DemoMissionSaveData(); data.futureDaily ??= new FutureDailySaveData(); data.story ??= new StorySaveData();
            data.reward ??= new RewardSaveData(); data.world ??= new WorldSaveData(); data.location ??= new PlayerLocationSaveData();
            data.currency.gold = Mathf.Max(0, data.currency.gold);
            NormalizeIntEntries(data.inventory.items);
            NormalizeStrings(data.story.completedEventIds, MaxGeneralIdEntries);
            NormalizeStrings(data.world.clearedEncounterIds, MaxGeneralIdEntries);
            NormalizeInteractionStates(data.world);
            NormalizeRewardLedger(data.reward);
            if (data.quest.quests != null)
                foreach (QuestStateSaveData quest in data.quest.quests)
                    if (quest != null) NormalizeQuestState(quest);
        }

        private static void NormalizeQuestState(QuestStateSaveData quest)
        {
            NormalizeStrings(quest.processedEventIds);
            NormalizeStrings(quest.retiredEventIds);
            NormalizeStrings(quest.revealedObjectiveIds);
            quest.activeGroupIndex = Mathf.Max(0, quest.activeGroupIndex);
            quest.attempt = Mathf.Max(0, quest.attempt);
            quest.failureReasonId = NormalizeId(quest.failureReasonId);

            if (!Enum.TryParse(quest.status, out QuestStatus status) ||
                !Enum.IsDefined(typeof(QuestStatus), status))
            {
                status = quest.completed ? QuestStatus.Completed : QuestStatus.Inactive;
            }

            quest.status = status.ToString();
            quest.completed = status == QuestStatus.Completed;
            if (status != QuestStatus.Failed)
                quest.failureReasonId = null;
            if (status != QuestStatus.Inactive)
                quest.attempt = Mathf.Max(1, quest.attempt);
        }

        private static void NormalizeRewardLedger(RewardSaveData reward)
        {
            reward.ledger ??= new List<RewardLedgerSaveData>();
            reward.combatLedger ??= new List<RewardLedgerSaveData>();
            Dictionary<string, RewardLedgerSaveData> unique = new(StringComparer.Ordinal);
            for (int i = 0; i < reward.ledger.Count; i++)
            {
                RewardLedgerSaveData entry = reward.ledger[i];
                if (entry == null ||
                    !Enum.TryParse(entry.sourceType, out RewardSourceType sourceType) ||
                    !TryMapSourceType(sourceType, out GameplayOutcomeSourceType outcomeType) ||
                    !GameplayOutcomeIdentity.TryCreate(outcomeType, entry.sourceId, entry.actionId, out GameplayOutcomeIdentity identity))
                {
                    continue;
                }

                entry.sourceId = identity.SourceId;
                entry.actionId = identity.ActionId;
                entry.requestedGold = Mathf.Max(0, entry.requestedGold);
                entry.requestedExp = Mathf.Max(0, entry.requestedExp);
                entry.requestedItemId = NormalizeId(entry.requestedItemId);
                entry.requestedItemCount = entry.requestedItemId == null ? 0 : Mathf.Max(0, entry.requestedItemCount);
                entry.gold = Mathf.Max(0, entry.gold);
                entry.exp = Mathf.Max(0, entry.exp);
                entry.itemId = NormalizeId(entry.itemId);
                entry.itemCount = entry.itemId == null ? 0 : Mathf.Max(0, entry.itemCount);
                unique.TryAdd(identity.CanonicalId, entry);
            }

            reward.ledger.Clear();
            reward.ledger.AddRange(unique.Values);
            reward.ledger.Sort(CompareLedgerEntries);
        }

        private static void NormalizeInteractionStates(WorldSaveData world)
        {
            world.interactions ??= new List<InteractionStateSaveData>();
            Dictionary<string, InteractionStateSaveData> unique = new(StringComparer.Ordinal);
            for (int i = 0; i < world.interactions.Count; i++)
            {
                InteractionStateSaveData entry = world.interactions[i];
                string id = NormalizeId(entry?.interactionId);
                if (id == null)
                    continue;

                if (!unique.TryGetValue(id, out InteractionStateSaveData normalized))
                {
                    normalized = new InteractionStateSaveData { interactionId = id };
                    unique.Add(id, normalized);
                }

                normalized.consumed |= entry.consumed;
                MergeOutcomes(normalized.resolvedOutcomes, entry.resolvedOutcomes);
            }

            world.interactions.Clear();
            world.interactions.AddRange(unique.Values);
            world.interactions.Sort((left, right) => string.CompareOrdinal(left.interactionId, right.interactionId));
        }

        private static void MergeOutcomes(
            List<InteractionOutcomeSaveData> target,
            List<InteractionOutcomeSaveData> source)
        {
            Dictionary<string, string> outcomes = new(StringComparer.Ordinal);
            for (int i = 0; i < target.Count; i++)
            {
                string actionId = NormalizeId(target[i]?.actionId);
                string outcomeId = NormalizeId(target[i]?.outcomeId);
                if (actionId != null && outcomeId != null)
                    outcomes[actionId] = outcomeId;
            }

            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    string actionId = NormalizeId(source[i]?.actionId);
                    string outcomeId = NormalizeId(source[i]?.outcomeId);
                    if (actionId == null || outcomeId == null)
                        continue;

                    if (!outcomes.TryGetValue(actionId, out string current) ||
                        string.CompareOrdinal(outcomeId, current) < 0)
                    {
                        outcomes[actionId] = outcomeId;
                    }
                }
            }

            target.Clear();
            foreach (KeyValuePair<string, string> pair in outcomes)
                target.Add(new InteractionOutcomeSaveData { actionId = pair.Key, outcomeId = pair.Value });
            target.Sort((left, right) => string.CompareOrdinal(left.actionId, right.actionId));
        }

        private static int CompareLedgerEntries(RewardLedgerSaveData left, RewardLedgerSaveData right)
        {
            int source = string.CompareOrdinal(left?.sourceType, right?.sourceType);
            if (source != 0) return source;
            int id = string.CompareOrdinal(left?.sourceId, right?.sourceId);
            return id != 0 ? id : string.CompareOrdinal(left?.actionId, right?.actionId);
        }

        private static bool TryMapSourceType(
            RewardSourceType sourceType,
            out GameplayOutcomeSourceType outcomeType)
        {
            outcomeType = sourceType switch
            {
                RewardSourceType.Combat => GameplayOutcomeSourceType.Combat,
                RewardSourceType.QuestCompletion => GameplayOutcomeSourceType.QuestCompletion,
                RewardSourceType.MissionCompletion => GameplayOutcomeSourceType.MissionCompletion,
                RewardSourceType.Interaction => GameplayOutcomeSourceType.Interaction,
                RewardSourceType.Story => GameplayOutcomeSourceType.Story,
                RewardSourceType.Choice => GameplayOutcomeSourceType.Choice,
                RewardSourceType.Loot => GameplayOutcomeSourceType.Loot,
                RewardSourceType.Tutorial => GameplayOutcomeSourceType.Tutorial,
                _ => GameplayOutcomeSourceType.Unknown
            };
            return outcomeType != GameplayOutcomeSourceType.Unknown;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

        private static void NormalizeStrings(List<string> values)
        {
            if (values == null) return;
            HashSet<string> unique = new(StringComparer.Ordinal);
            values.RemoveAll(value => string.IsNullOrWhiteSpace(value) || !unique.Add(value));
            values.Sort(StringComparer.Ordinal);
        }
    }
}
