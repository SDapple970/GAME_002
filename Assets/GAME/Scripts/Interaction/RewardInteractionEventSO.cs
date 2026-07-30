using Game.Reward;
using Game.UI;
using UnityEngine;

namespace Game.Interaction
{
    [CreateAssetMenu(menuName = "GAME/Interaction/Reward Event", fileName = "RewardInteractionEvent")]
    public sealed class RewardInteractionEventSO : InteractionEventSO
    {
        [SerializeField] private string itemId = "item.test";
        [SerializeField] private string rewardSourceId;
        [SerializeField] private int amount = 1;
        [SerializeField] private string displayName = "아이템";
        [SerializeField] private bool addToInventoryIfAvailable = true;
        [SerializeField] private bool showPromptMessage = true;
        [SerializeField] private float messageSeconds = 1.5f;

        public override void Execute(InteractionContext context)
        {
            int safeAmount = Mathf.Max(1, amount);
            string safeName = string.IsNullOrEmpty(displayName) ? itemId : displayName;
            string message = $"{safeName} x{safeAmount} 획득";

            Debug.Log($"[RewardInteractionEvent] {message}", context.Target);

            RewardGrantResult grantResult = RewardGrantResult.Empty;
            if (addToInventoryIfAvailable)
            {
                RewardService rewardService = RewardService.Instance;
                if (rewardService != null)
                    grantResult = rewardService.GrantReward(new RewardGrantRequest(
                        RewardSourceType.Interaction,
                        ResolveSourceId(),
                        itemId: itemId,
                        itemCount: safeAmount));
                else
                    Debug.LogWarning($"[RewardInteractionEvent] RewardService is missing. sourceId='{ResolveSourceId()}'.", context.Target);
            }

            if (grantResult.DuplicateBlocked || (addToInventoryIfAvailable && !grantResult.HasAnyReward))
                return;

            RewardUIPanel rewardPanel = Object.FindFirstObjectByType<RewardUIPanel>();
            if (rewardPanel != null && rewardPanel.TryShowFieldRewardMessage(message))
                return;

            if (showPromptMessage && context.Controller != null)
                context.Controller.ShowTemporaryMessage(message, messageSeconds);
        }

        private string ResolveSourceId()
        {
            if (!string.IsNullOrWhiteSpace(rewardSourceId))
                return rewardSourceId.Trim();

            Debug.LogWarning(
                $"[RewardInteractionEvent] Using asset-name compatibility identity '{name}'. New production content must author rewardSourceId.",
                this);
            return $"compat-interaction:{name}";
        }
    }
}
