using System;
using Game.Search;
using Game.Story;
using Game.Story.Core;
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
            ApplyImmediate();
        }

        public bool TryCreateRewardProposal(out SearchRewardProposal proposal)
        {
            proposal = null;

            switch (type)
            {
                case SearchEffectType.AddSmallLoot:
                    proposal = CreateProposal(SearchRewardKind.SmallLoot, "소량 아이템", DefaultAmount(), "작은 보상을 획득할 수 있습니다.");
                    return true;
                case SearchEffectType.AddLargeLoot:
                    proposal = CreateProposal(SearchRewardKind.LargeLoot, "대량 아이템", DefaultAmount(), "큰 보상을 획득할 수 있습니다.");
                    return true;
                case SearchEffectType.AddJournal:
                    proposal = CreateProposal(SearchRewardKind.Journal, "일지", 1, "새로운 일지를 획득할 수 있습니다.");
                    return true;
                case SearchEffectType.AddCat:
                    proposal = CreateProposal(SearchRewardKind.Cat, "고양이", 1, "고양이를 발견했습니다.");
                    return true;
                default:
                    return false;
            }
        }

        public void ApplyImmediate()
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
                case SearchEffectType.AddLargeLoot:
                case SearchEffectType.AddJournal:
                case SearchEffectType.AddCat:
                    LogPlaceholder("Reward proposal effect. Apply through ItemAcquisitionHUD.");
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

        private SearchRewardProposal CreateProposal(SearchRewardKind kind, string defaultName, int amount, string defaultDescription)
        {
            string description = string.IsNullOrEmpty(messageOverride) ? defaultDescription : messageOverride;
            return new SearchRewardProposal(key, defaultName, description, null, kind, amount);
        }

        private int DefaultAmount()
        {
            return Mathf.Max(1, intValue <= 0 ? 1 : intValue);
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
