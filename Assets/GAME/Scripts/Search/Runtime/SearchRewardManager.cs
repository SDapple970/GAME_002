using UnityEngine;
using Game.Reward;

namespace Game.Search
{
    public sealed class SearchRewardManager : MonoBehaviour
    {
        public static SearchRewardManager Instance { get; private set; }

        [SerializeField] private int smallLootCount;
        [SerializeField] private int largeLootCount;
        [SerializeField] private int journalCount;
        [SerializeField] private int catCount;
        [SerializeField] private int currency;
        [SerializeField] private int mentality;
        [SerializeField] private int stress;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void AddSmallLoot(int amount = 1)
        {
            smallLootCount += amount;
            Debug.Log($"[SearchRewardManager] Small loot +{amount}. total={smallLootCount}", this);
        }

        public void AddLargeLoot(int amount = 1)
        {
            largeLootCount += amount;
            Debug.Log($"[SearchRewardManager] Large loot +{amount}. total={largeLootCount}", this);
        }

        public void AddJournal(int amount = 1)
        {
            journalCount += amount;
            Debug.Log($"[SearchRewardManager] Journal +{amount}. total={journalCount}", this);
        }

        public void AddCat(int amount = 1)
        {
            catCount += amount;
            Debug.Log($"[SearchRewardManager] Cat +{amount}. total={catCount}", this);
        }

        public void ModifyMentality(int amount)
        {
            mentality += amount;
            Debug.Log($"[SearchRewardManager] Mentality {FormatSigned(amount)}. total={mentality}", this);
        }

        public void ModifyStress(int amount)
        {
            stress += amount;
            Debug.Log($"[SearchRewardManager] Stress {FormatSigned(amount)}. total={stress}", this);
        }

        public void AcceptReward(SearchRewardProposal proposal)
        {
            if (proposal == null)
            {
                Debug.LogWarning("[SearchRewardManager] Null reward proposal ignored.", this);
                return;
            }

            int amount = Mathf.Max(1, proposal.Amount);
            RewardService rewardService = RewardService.Instance;
            if (rewardService == null)
            {
                Debug.LogWarning("[SearchRewardManager] RewardService is missing. Search reward was not granted.", this);
                return;
            }

            RewardGrantRequest request = proposal.Kind == SearchRewardKind.Currency
                ? new RewardGrantRequest(
                    RewardSourceType.Loot,
                    proposal.RewardId,
                    gold: amount,
                    actionId: proposal.Kind.ToString())
                : new RewardGrantRequest(
                    RewardSourceType.Loot,
                    proposal.RewardId,
                    itemId: proposal.RewardId,
                    itemCount: amount,
                    actionId: proposal.Kind.ToString());
            RewardGrantResult grant = rewardService.GrantReward(request);
            if (grant.DuplicateBlocked || grant.InvalidRequest)
                return;
            int appliedAmount = proposal.Kind == SearchRewardKind.Currency
                ? grant.Gold
                : grant.ItemCount;
            if (appliedAmount <= 0)
                return;

            switch (proposal.Kind)
            {
                case SearchRewardKind.SmallLoot:
                    AddSmallLoot(appliedAmount);
                    break;
                case SearchRewardKind.LargeLoot:
                    AddLargeLoot(appliedAmount);
                    break;
                case SearchRewardKind.Journal:
                    AddJournal(appliedAmount);
                    break;
                case SearchRewardKind.Cat:
                    AddCat(appliedAmount);
                    break;
                case SearchRewardKind.Currency:
                    currency += grant.Gold;
                    Debug.Log($"[SearchRewardManager] Currency +{grant.Gold}. total={currency}", this);
                    break;
                case SearchRewardKind.Custom:
                    Debug.Log($"[SearchRewardManager] Custom reward accepted. id='{proposal.RewardId}' name='{proposal.RewardName}' amount={amount}", this);
                    break;
                default:
                    Debug.LogWarning($"[SearchRewardManager] Unsupported reward kind='{proposal.Kind}'.", this);
                    break;
            }
        }

        private static string FormatSigned(int amount)
        {
            return amount >= 0 ? $"+{amount}" : amount.ToString();
        }
    }
}
