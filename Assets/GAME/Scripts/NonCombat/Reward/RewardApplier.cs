using Game.Combat.Model;
using Game.NonCombat.Inventory;
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
            if (result == null || !result.IsWin) return;

            CurrencyWallet wallet = currencyWallet != null ? currencyWallet : CurrencyWallet.Instance;
            if (wallet != null)
                wallet.AddGold(result.TotalGold);

            if (result.TotalExp > 0)
                Debug.Log($"[RewardApplier] TotalExp {result.TotalExp} received. TODO: connect character level system.", this);
        }
    }
}
