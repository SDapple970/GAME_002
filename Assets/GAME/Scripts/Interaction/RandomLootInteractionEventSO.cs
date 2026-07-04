using Game.NonCombat.Inventory;
using Game.Reward;
using Game.UI;
using UnityEngine;

namespace Game.Interaction
{
    [CreateAssetMenu(menuName = "GAME/Interaction/Random Loot Event", fileName = "RandomLootInteractionEvent")]
    public sealed class RandomLootInteractionEventSO : InteractionEventSO
    {
        [SerializeField] private RandomLootEntry[] entries;
        [SerializeField] private bool addToInventoryIfAvailable = true;
        [SerializeField] private bool showMessage = true;
        [SerializeField] private float messageSeconds = 1.5f;

        public override void Execute(InteractionContext context)
        {
            if (!TryPickEntry(out RandomLootEntry entry))
            {
                Debug.LogWarning("[RandomLootInteractionEventSO] No loot entry could be selected. Check entries and weights.", context.Target);
                return;
            }

            string message = BuildMessage(entry);

            if (!entry.isNothing && addToInventoryIfAvailable)
            {
                int amount = Mathf.Max(1, entry.amount);
                RewardService rewardService = RewardService.Instance;
                if (rewardService != null)
                    rewardService.Grant(new RewardGrantRequest(RewardSourceType.Interaction, name, itemId: entry.itemId, itemCount: amount));
                else if (InventoryService.Instance != null)
                    InventoryService.Instance.AddItem(entry.itemId, amount);
            }

            Debug.Log($"[RandomLootInteractionEventSO] {message}", context.Target);

            if (showMessage)
                ShowRewardMessage(context, message);
        }

        private bool TryPickEntry(out RandomLootEntry selected)
        {
            selected = null;

            if (entries == null || entries.Length == 0)
                return false;

            float totalWeight = 0f;
            for (int i = 0; i < entries.Length; i++)
            {
                RandomLootEntry entry = entries[i];
                if (entry != null)
                    totalWeight += Mathf.Max(0f, entry.weight);
            }

            if (totalWeight <= 0f)
                return false;

            float roll = Random.value * totalWeight;
            float cursor = 0f;
            for (int i = 0; i < entries.Length; i++)
            {
                RandomLootEntry entry = entries[i];
                if (entry == null)
                    continue;

                cursor += Mathf.Max(0f, entry.weight);
                if (roll <= cursor)
                {
                    selected = entry;
                    return true;
                }
            }

            selected = entries[entries.Length - 1];
            return selected != null;
        }

        private static string BuildMessage(RandomLootEntry entry)
        {
            if (entry == null || entry.isNothing)
                return "아무것도 찾지 못했다.";

            string itemName = string.IsNullOrEmpty(entry.displayName) ? entry.itemId : entry.displayName;
            int amount = Mathf.Max(1, entry.amount);
            return $"{itemName} x{amount} found.";
        }

        private void ShowRewardMessage(InteractionContext context, string message)
        {
            RewardUIPanel rewardPanel = Object.FindFirstObjectByType<RewardUIPanel>();
            if (rewardPanel != null && rewardPanel.TryShowFieldRewardMessage(message))
                return;

            if (context.Controller != null)
                context.Controller.ShowTemporaryMessage(message, messageSeconds);
        }
    }
}
