using Game.NonCombat.Inventory;
using Game.UI;
using UnityEngine;

namespace Game.Interaction
{
    [CreateAssetMenu(menuName = "GAME/Interaction/Reward Event", fileName = "RewardInteractionEvent")]
    public sealed class RewardInteractionEventSO : InteractionEventSO
    {
        [SerializeField] private string itemId = "item.test";
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

            if (addToInventoryIfAvailable && InventoryService.Instance != null)
                InventoryService.Instance.AddItem(itemId, safeAmount);

            RewardUIPanel rewardPanel = Object.FindFirstObjectByType<RewardUIPanel>();
            if (rewardPanel != null && rewardPanel.TryShowFieldRewardMessage(message))
                return;

            if (showPromptMessage && context.Controller != null)
                context.Controller.ShowTemporaryMessage(message, messageSeconds);
        }
    }
}
