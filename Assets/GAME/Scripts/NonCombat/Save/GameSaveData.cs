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
        public ExplorationSaveData exploration = new();
        public PlayerLocationSaveData location = new();
    }

    [Serializable]
    public sealed class ExplorationSaveData
    {
        public int shining;
        public int hunger;
        public List<PersistentConditionSaveData> conditions = new();
    }

    [Serializable]
    public sealed class PersistentConditionSaveData
    {
        public string ownerId;
        public string conditionId;
        public int category;
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
        public int activeGroupIndex;
        public string failureReasonId;
        public int attempt;
        public List<QuestObjectiveSaveData> objectives = new();
        public List<string> processedEventIds = new();
        public List<string> retiredEventIds = new();
        public List<string> revealedObjectiveIds = new();
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
        public string leaderCharacterId;
        public List<string> selectedCombatMemberIds = new();
    }

    [Serializable]
    public sealed class ProgressionSaveData
    {
        public List<PersonaStatSaveData> personaStats = new();
        public List<CharacterProgressionStateSaveData> characters = new();
        public List<string> completedObjectiveIds = new();
    }

    [Serializable]
    public sealed class CharacterProgressionStateSaveData
    {
        public string characterId;
        public int level;
        public int experience;
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
        public List<string> appliedQuestDayCostIds = new();
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
        public const int CurrentSchemaVersion = 7;
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
        public List<RewardLedgerSaveData> ledger = new();
        public List<RewardLedgerSaveData> combatLedger = new();
    }

    [Serializable]
    public sealed class RewardLedgerSaveData
    {
        public string sourceType;
        public string sourceId;
        public string actionId;
        public int requestedGold;
        public int requestedExp;
        public string requestedItemId;
        public int requestedItemCount;
        public int gold;
        public int exp;
        public string itemId;
        public int itemCount;
        public bool partialFailure;
        public string progressionTargetId;
        public bool expSettled;

        public RewardLedgerSaveData Clone()
        {
            return (RewardLedgerSaveData)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class WorldSaveData
    {
        public List<string> clearedEncounterIds = new();
        public List<InteractionStateSaveData> interactions = new();
    }

    [Serializable]
    public sealed class InteractionStateSaveData
    {
        public string interactionId;
        public bool consumed;
        public List<InteractionOutcomeSaveData> resolvedOutcomes = new();
    }

    [Serializable]
    public sealed class InteractionOutcomeSaveData
    {
        public string actionId;
        public string outcomeId;
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
