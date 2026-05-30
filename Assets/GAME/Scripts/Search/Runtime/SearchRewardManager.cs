using UnityEngine;

namespace Game.Search
{
    public sealed class SearchRewardManager : MonoBehaviour
    {
        public static SearchRewardManager Instance { get; private set; }

        [SerializeField] private int smallLootCount;
        [SerializeField] private int largeLootCount;
        [SerializeField] private int journalCount;
        [SerializeField] private int catCount;
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

        private static string FormatSigned(int amount)
        {
            return amount >= 0 ? $"+{amount}" : amount.ToString();
        }
    }
}
