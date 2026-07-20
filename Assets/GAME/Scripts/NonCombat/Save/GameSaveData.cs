using System;
using System.Collections.Generic;

namespace Game.NonCombat.Save
{
    [Serializable]
    public sealed class GameSaveData
    {
        public SaveHeaderData header = new();
        public QuestSaveData quest = new();
        public InventorySaveData inventory = new();
        public CurrencySaveData currency = new();
        public PartySaveData party = new();
        public ProgressionSaveData progression = new();
        public DemoMissionSaveData demoMission = new();
        public FutureDailySaveData futureDaily = new();
        public StorySaveData story = new();
        public RewardSaveData reward = new();
        public WorldSaveData world = new();
        public PlayerLocationSaveData location = new();
    }

    [Serializable]
    public sealed class SaveHeaderData
    {
        public string formatId = GameSaveDataFormat.FormatId;
        public int schemaVersion = GameSaveDataFormat.CurrentSchemaVersion;
        public string savedAtUtc;
        public string activeSceneId;
        public string playerSpawnId;
        public string applicationVersion;
    }

    [Serializable]
    public sealed class QuestSaveData
    {
        public List<QuestStateSaveData> quests = new();
    }

    [Serializable]
    public sealed class QuestStateSaveData
    {
        public string questId;
        public bool completed;
        public string status;
        public List<QuestObjectiveSaveData> objectives = new();
        public List<string> processedEventIds = new();
    }

    [Serializable]
    public sealed class QuestObjectiveSaveData
    {
        public string objectiveId;
        public int progress;
        public int requiredCount;
    }

    [Serializable]
    public sealed class InventorySaveData
    {
        public List<SaveIntEntry> items = new();
    }

    [Serializable]
    public sealed class CurrencySaveData
    {
        public int gold;
    }

    [Serializable]
    public sealed class PartySaveData
    {
        public List<string> memberIds = new();
        public List<SaveIntEntry> memberLevels = new();
    }

    [Serializable]
    public sealed class ProgressionSaveData
    {
        public List<PersonaStatSaveData> personaStats = new();
        public List<string> completedObjectiveIds = new();
    }

    [Serializable]
    public sealed class PersonaStatSaveData
    {
        public string stat;
        public int level;
        public int xp;
    }

    [Serializable]
    public sealed class DemoMissionSaveData
    {
        public string missionId;
        public int enemyDefeatCount;
        public bool npcRescued;
        public bool completed;
    }

    [Serializable]
    public sealed class FutureDailySaveData
    {
        public int dayIndex;
        public int weekIndex;
        public string currentChapterId;
        public string currentDayPhase;
        public string calendarDateId;
        public string selectedMissionId;
        public string selectedMissionTargetFieldSceneName;
        public string selectedMissionTargetSpawnPointId;
        public List<string> selectedSupplyItemIds = new();
        public List<int> selectedSupplyItemCounts = new();
        public List<string> completedDailyActionIds = new();
        public List<string> completedSettlementIds = new();
    }

    [Serializable]
    public sealed class SaveIntEntry
    {
        public string id;
        public int value;
    }

    public static class GameSaveDataFormat
    {
        public const string FormatId = "GAME_002";
        public const int CurrentSchemaVersion = 2;
    }

    [Serializable]
    public sealed class StorySaveData
    {
        public int currentChapter = 1;
        public int mainProgress;
        public List<string> completedEventIds = new();
        public List<SaveBoolEntry> flags = new();
    }

    [Serializable]
    public sealed class SaveBoolEntry
    {
        public string id;
        public bool value;
    }

    [Serializable]
    public sealed class RewardSaveData
    {
        public List<RewardLedgerSaveData> combatLedger = new();
    }

    [Serializable]
    public sealed class RewardLedgerSaveData
    {
        public string sourceType;
        public string sourceId;
        public int gold;
        public int exp;
        public string itemId;
        public int itemCount;
    }

    [Serializable]
    public sealed class WorldSaveData
    {
        public List<string> clearedEncounterIds = new();
    }

    [Serializable]
    public sealed class PlayerLocationSaveData
    {
        public bool hasPositionFallback;
        public float positionX;
        public float positionY;
        public float positionZ;
    }
}
