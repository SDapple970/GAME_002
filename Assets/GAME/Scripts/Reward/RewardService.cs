using Game.Combat.Model;
using Game.NonCombat.Inventory;
using UnityEngine;

namespace Game.Reward
{
    public sealed class RewardService : MonoBehaviour
    {
        public static RewardService Instance { get; private set; }

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

        public RewardResult GrantCombatResult(CombatResult result)
        {
            if (result == null || !result.IsWin)
                return RewardResult.Empty;

            int grantedGold = Mathf.Max(0, result.TotalGold);
            int grantedExp = Mathf.Max(0, result.TotalExp);

            CurrencyWallet wallet = currencyWallet != null ? currencyWallet : CurrencyWallet.Instance;
            if (wallet != null && grantedGold > 0)
                wallet.AddGold(grantedGold);

            if (grantedExp > 0)
                Debug.Log($"[RewardService] Combat EXP {grantedExp} received. CharacterProgressionService is not implemented yet.", this);

            return new RewardResult(grantedGold, grantedExp);
        }
    }
}
