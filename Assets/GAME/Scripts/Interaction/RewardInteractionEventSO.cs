using Game.Reward;
using UnityEngine;

namespace Game.Interaction
{
    [CreateAssetMenu(menuName = "GAME/Interaction/Reward Event", fileName = "RewardInteractionEvent")]
    public sealed class RewardInteractionEventSO : InteractionEventSO
    {
        [SerializeField] private string itemId = "item.test";
        [SerializeField] private string rewardSourceId;
        [SerializeField] private int amount = 1;
        [SerializeField] private string displayName = "Item";
        [SerializeField] private bool addToInventoryIfAvailable = true;
        [SerializeField] private bool showPromptMessage = true;
        [SerializeField] private float messageSeconds = 1.5f;

        public override bool SupportsProductionExecution => true;

        public override void Execute(InteractionContext context)
        {
            InteractionEventResult result = Grant(
                context.Target,
                ResolveCompatibilitySourceId(),
                null,
                showPromptMessage);
            if (!string.IsNullOrWhiteSpace(result.Message))
                context.Controller?.ShowTemporaryMessage(result.Message, messageSeconds);
        }

        public override InteractionEventResult ExecuteProduction(InteractionExecutionContext context)
        {
            string explicitSourceId = InteractionIdentity.Normalize(rewardSourceId);
            string sourceId = explicitSourceId ?? context.Request.InteractionId;
            string resolvedActionId = explicitSourceId != null && ActionId == null
                ? null
                : context.ActionId;
            if (sourceId == null)
                return InteractionEventResult.Failed("interaction.reward.invalid-source");

            return Grant(context.Request.Source, sourceId, resolvedActionId, showPromptMessage);
        }

        private InteractionEventResult Grant(
            Object logContext,
            string sourceId,
            string resolvedActionId,
            bool includeMessage)
        {
            int safeAmount = Mathf.Max(1, amount);
            string safeName = string.IsNullOrEmpty(displayName) ? itemId : displayName;
            string message = $"{safeName} x{safeAmount} acquired.";

            if (!addToInventoryIfAvailable)
                return InteractionEventResult.NoEffect(includeMessage ? message : null);

            RewardService rewardService = RewardService.Instance;
            if (rewardService == null)
            {
                Debug.LogWarning($"[RewardInteractionEvent] RewardService is missing. sourceId='{sourceId}'.", logContext);
                return InteractionEventResult.Failed("interaction.reward.service-missing");
            }

            RewardGrantResult grantResult = rewardService.GrantReward(new RewardGrantRequest(
                RewardSourceType.Interaction,
                sourceId,
                itemId: itemId,
                itemCount: safeAmount,
                actionId: resolvedActionId));
            if (grantResult.InvalidRequest)
                return InteractionEventResult.Failed("interaction.reward.invalid-request");
            if (grantResult.DuplicateBlocked)
                return InteractionEventResult.AcceptedResult(false, true, reward: grantResult);
            if (!grantResult.HasAnyReward)
                return InteractionEventResult.Failed("interaction.reward.not-applied");

            Debug.Log($"[RewardInteractionEvent] {message}", logContext);
            return InteractionEventResult.AcceptedResult(
                true,
                true,
                includeMessage ? message : null,
                grantResult);
        }

        private string ResolveCompatibilitySourceId()
        {
            if (!string.IsNullOrWhiteSpace(rewardSourceId))
                return rewardSourceId.Trim();

            Debug.LogWarning(
                $"[RewardInteractionEvent] Using asset-name compatibility identity '{name}'. New production content must author interactionId.",
                this);
            return $"compat-interaction:{name}";
        }
    }
}
