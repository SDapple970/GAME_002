using Game.Combat.Model;
using Game.Reward;
using UnityEngine;

namespace Game.NonCombat.Reward
{
    public sealed class RewardApplier : MonoBehaviour
    {
        public static RewardApplier Instance { get; private set; }

        private bool _missingRewardServiceWarned;

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

            if (_missingRewardServiceWarned)
                return;

            _missingRewardServiceWarned = true;
            Debug.LogWarning("[RewardApplier] RewardService is missing. Combat reward was not granted.", this);
        }
    }
}
