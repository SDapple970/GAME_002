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
    }

    [Serializable]
    public sealed class SaveHeaderData
    {
        public int schemaVersion = 1;
        public string savedAtUtc;
        public string activeSceneId;
        public string playerSpawnId;
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
        public List<QuestObjectiveSaveData> objectives = new();
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
        public string calendarDateId;
        public List<string> completedDailyActionIds = new();
    }

    [Serializable]
    public sealed class SaveIntEntry
    {
        public string id;
        public int value;
    }
}
