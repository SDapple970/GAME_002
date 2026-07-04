using Game.Combat.Model;
using Game.NonCombat.Inventory;
using UnityEngine;

namespace Game.Reward
{
    public sealed class RewardService : MonoBehaviour
    {
        public static RewardService Instance { get; private set; }

        [SerializeField] private CurrencyWallet currencyWallet;
        [SerializeField] private InventoryService inventoryService;

        private bool _missingCurrencyWalletWarned;
        private bool _missingInventoryServiceWarned;
        private bool _missingProgressionWarned;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public RewardResult GrantCombatResult(CombatResult result)
        {
            if (result == null || !result.IsWin)
                return RewardResult.Empty;

            return Grant(new RewardGrantRequest(
                RewardSourceType.Combat,
                "combat",
                result.TotalGold,
                result.TotalExp));
        }

        public RewardResult GrantQuestCompletion(string questId, int gold, int exp)
        {
            return Grant(new RewardGrantRequest(
                RewardSourceType.QuestCompletion,
                questId,
                gold,
                exp));
        }

        public RewardResult GrantMissionCompletion(string missionId, int gold, int exp)
        {
            return Grant(new RewardGrantRequest(
                RewardSourceType.MissionCompletion,
                missionId,
                gold,
                exp));
        }

        public RewardResult Grant(RewardGrantRequest request)
        {
            int grantedGold = Mathf.Max(0, request.Gold);
            int grantedExp = Mathf.Max(0, request.Exp);
            string grantedItemId = string.IsNullOrWhiteSpace(request.ItemId) ? null : request.ItemId;
            int grantedItemCount = grantedItemId != null ? Mathf.Max(0, request.ItemCount) : 0;

            if (grantedGold <= 0 && grantedExp <= 0 && grantedItemCount <= 0)
                return RewardResult.Empty;

            if (grantedGold > 0 && !GrantGold(grantedGold, request))
                grantedGold = 0;

            if (grantedItemCount > 0 && !GrantItem(grantedItemId, grantedItemCount, request))
                grantedItemCount = 0;

            if (grantedExp > 0)
                WarnMissingProgressionOnce(grantedExp, request);

            return new RewardResult(grantedGold, grantedExp, grantedItemId, grantedItemCount);
        }

        private bool GrantGold(int amount, RewardGrantRequest request)
        {
            CurrencyWallet wallet = currencyWallet != null ? currencyWallet : CurrencyWallet.Instance;
            if (wallet != null)
            {
                wallet.AddGold(amount);
                return true;
            }

            if (!_missingCurrencyWalletWarned)
            {
                _missingCurrencyWalletWarned = true;
                Debug.LogWarning($"[RewardService] CurrencyWallet is missing. Gold reward was not granted. source={request.SourceType}, sourceId={request.SourceId}", this);
            }

            return false;
        }

        private bool GrantItem(string itemId, int count, RewardGrantRequest request)
        {
            InventoryService inventory = inventoryService != null ? inventoryService : InventoryService.Instance;
            if (inventory != null)
            {
                inventory.AddItem(itemId, count);
                return true;
            }

            if (!_missingInventoryServiceWarned)
            {
                _missingInventoryServiceWarned = true;
                Debug.LogWarning($"[RewardService] InventoryService is missing. Item reward was not granted. source={request.SourceType}, sourceId={request.SourceId}", this);
            }

            return false;
        }

        private void WarnMissingProgressionOnce(int exp, RewardGrantRequest request)
        {
            if (_missingProgressionWarned)
                return;

            _missingProgressionWarned = true;
            Debug.Log($"[RewardService] EXP {exp} received from {request.SourceType}. CharacterProgressionService is not implemented yet.", this);
        }
    }
}
