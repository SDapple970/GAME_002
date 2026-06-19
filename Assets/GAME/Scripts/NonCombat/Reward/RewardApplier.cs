using Game.Combat.Model;
using Game.NonCombat.Inventory;
using Game.Reward;
using UnityEngine;

namespace Game.NonCombat.Reward
{
    public sealed class RewardApplier : MonoBehaviour
    {
        public static RewardApplier Instance { get; private set; }

        [SerializeField] private CurrencyWallet currencyWallet;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void ApplyCombatResult(CombatResult result)
        {
            RewardService rewardService = RewardService.Instance;
            if (rewardService != null)
            {
                rewardService.GrantCombatResult(result);
                return;
            }

            if (result == null || !result.IsWin) return;

            CurrencyWallet wallet = currencyWallet != null ? currencyWallet : CurrencyWallet.Instance;
            if (wallet != null)
                wallet.AddGold(result.TotalGold);
        }
    }
}
