using System;
using Game.Search;
using Game.Story;
using UnityEngine;

namespace Game.Search.Data
{
    public enum SearchEffectType
    {
        None,
        AddSmallLoot,
        AddLargeLoot,
        AddJournal,
        AddCat,
        ModifyMentality,
        ModifyStress,
        AddBuff,
        AddDebuff,
        RemoveDebuff,
        RevealMapArea,
        RevealBossInfo,
        StartBattle,
        IncreaseEnemyAlert,
        AddStoryFlagInt,
        SetStoryFlagBool
    }

    [Serializable]
    public sealed class SearchEffect
    {
        [SerializeField] private SearchEffectType type;
        [SerializeField] private string key;
        [SerializeField] private bool boolValue;
        [SerializeField] private int intValue;
        [SerializeField] private float floatValue;
        [SerializeField] private string messageOverride;

        public SearchEffectType Type => type;
        public string Key => key;
        public bool BoolValue => boolValue;
        public int IntValue => intValue;
        public float FloatValue => floatValue;
        public string MessageOverride => messageOverride;

        public void Apply()
        {
            switch (type)
            {
                case SearchEffectType.None:
                    LogPlaceholder("No effect.");
                    return;
                case SearchEffectType.AddStoryFlagInt:
                    AddStoryFlagInt();
                    return;
                case SearchEffectType.SetStoryFlagBool:
                    SetStoryFlagBool();
                    return;
                case SearchEffectType.ModifyMentality:
                    ModifyMentality();
                    return;
                case SearchEffectType.ModifyStress:
                    ModifyStress();
                    return;
                case SearchEffectType.AddSmallLoot:
                    AddSmallLoot();
                    return;
                case SearchEffectType.AddLargeLoot:
                    AddLargeLoot();
                    return;
                case SearchEffectType.AddJournal:
                    AddJournal();
                    return;
                case SearchEffectType.AddCat:
                    AddCat();
                    return;
                case SearchEffectType.AddBuff:
                case SearchEffectType.AddDebuff:
                case SearchEffectType.RemoveDebuff:
                case SearchEffectType.RevealMapArea:
                case SearchEffectType.RevealBossInfo:
                case SearchEffectType.StartBattle:
                case SearchEffectType.IncreaseEnemyAlert:
                    LogPlaceholder("System integration pending.");
                    return;
                default:
                    Debug.LogWarning($"[SearchEffect] Unsupported effect type='{type}'.");
                    return;
            }
        }

        private void AddStoryFlagInt()
        {
            if (!CanUseStoryFlag()) return;

            StoryFlagManager.Instance.AddInt(key, intValue);
            LogPlaceholder($"Story int flag added. key='{key}' amount={intValue}.");
        }

        private void SetStoryFlagBool()
        {
            if (!CanUseStoryFlag()) return;

            StoryFlagManager.Instance.SetBool(key, boolValue);
            LogPlaceholder($"Story bool flag set. key='{key}' value={boolValue}.");
        }

        private void AddSmallLoot()
        {
            if (SearchRewardManager.Instance == null)
            {
                LogRewardManagerMissing();
                return;
            }

            SearchRewardManager.Instance.AddSmallLoot(Mathf.Max(1, intValue <= 0 ? 1 : intValue));
        }

        private void AddLargeLoot()
        {
            if (SearchRewardManager.Instance == null)
            {
                LogRewardManagerMissing();
                return;
            }

            SearchRewardManager.Instance.AddLargeLoot(Mathf.Max(1, intValue <= 0 ? 1 : intValue));
        }

        private void AddJournal()
        {
            if (SearchRewardManager.Instance == null)
            {
                LogRewardManagerMissing();
                return;
            }

            SearchRewardManager.Instance.AddJournal(1);
        }

        private void AddCat()
        {
            if (SearchRewardManager.Instance == null)
            {
                LogRewardManagerMissing();
                return;
            }

            SearchRewardManager.Instance.AddCat(1);
        }

        private void ModifyMentality()
        {
            if (SearchRewardManager.Instance == null)
            {
                LogRewardManagerMissing();
                return;
            }

            SearchRewardManager.Instance.ModifyMentality(intValue);
        }

        private void ModifyStress()
        {
            if (SearchRewardManager.Instance == null)
            {
                LogRewardManagerMissing();
                return;
            }

            SearchRewardManager.Instance.ModifyStress(intValue);
        }

        private bool CanUseStoryFlag()
        {
            if (StoryFlagManager.Instance == null)
            {
                Debug.LogWarning($"[SearchEffect] StoryFlagManager missing for type='{type}' key='{key}'.");
                return false;
            }

            if (!string.IsNullOrEmpty(key)) return true;

            Debug.LogWarning($"[SearchEffect] Empty story flag key ignored for type='{type}'.");
            return false;
        }

        private void LogPlaceholder(string detail)
        {
            string message = string.IsNullOrEmpty(messageOverride) ? detail : messageOverride;
            Debug.Log($"[SearchEffect] type='{type}' key='{key}' int={intValue} float={floatValue} bool={boolValue}. {message}");
        }

        private void LogRewardManagerMissing()
        {
            Debug.LogWarning($"[SearchEffect] SearchRewardManager missing for type='{type}'. Reward was not recorded.");
        }
    }
}
