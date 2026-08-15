using Game.NonCombat.Party;
using Game.Reward;
using Game.World.Exploration;
using UnityEngine;

namespace Game.Interaction
{
    public enum ExplorationOutcomeType
    {
        Item = 0,
        Currency = 10,
        Shining = 20,
        Hunger = 30,
        Disease = 40,
        Quirk = 50
    }

    [CreateAssetMenu(menuName = "GAME/Interaction/Exploration Outcome Event", fileName = "ExplorationOutcomeInteractionEvent")]
    public sealed class ExplorationOutcomeInteractionEventSO : InteractionEventSO
    {
        [SerializeField] private ExplorationOutcomeType outcomeType;
        [SerializeField] private int amount = 1;
        [SerializeField] private string itemId;
        [SerializeField] private PersistentConditionDefinitionSO condition;
        [SerializeField] private string targetCharacterId;

        public override bool SupportsProductionExecution => true;
        public override void Execute(InteractionContext context) { }

        public override InteractionEventResult ExecuteProduction(InteractionExecutionContext context)
        {
            if (amount == 0 || (outcomeType != ExplorationOutcomeType.Hunger && amount < 0))
                return InteractionEventResult.Failed("interaction.exploration.invalid-amount");
            return outcomeType switch
            {
                ExplorationOutcomeType.Item => GrantReward(context, 0, itemId, amount),
                ExplorationOutcomeType.Currency => GrantReward(context, amount, null, 0),
                ExplorationOutcomeType.Shining => ChangeResource(true, amount),
                ExplorationOutcomeType.Hunger => ChangeResource(false, amount),
                ExplorationOutcomeType.Disease => ApplyCondition(PersistentConditionCategory.Disease),
                ExplorationOutcomeType.Quirk => ApplyCondition(PersistentConditionCategory.Quirk),
                _ => InteractionEventResult.Failed("interaction.exploration.unsupported")
            };
        }

        private InteractionEventResult GrantReward(InteractionExecutionContext context, int gold, string rewardItemId, int itemCount)
        {
            RewardService service = RewardService.Instance;
            if (service == null) return InteractionEventResult.Failed("interaction.exploration.reward-service-missing");
            RewardGrantResult result = service.GrantReward(new RewardGrantRequest(
                RewardSourceType.Interaction, context.Request.InteractionId, gold: gold,
                itemId: rewardItemId, itemCount: itemCount, actionId: context.ActionId));
            if (result.InvalidRequest) return InteractionEventResult.Failed("interaction.exploration.reward-invalid");
            return InteractionEventResult.AcceptedResult(result.HasAnyReward, true, reward: result);
        }

        private static InteractionEventResult ChangeResource(bool shining, int value)
        {
            ExplorationResourceRuntime resources = ExplorationResourceRuntime.Instance;
            if (resources == null) return InteractionEventResult.Failed("interaction.exploration.resource-service-missing");
            bool changed = shining ? resources.TryAddShining(value) : resources.TryChangeHunger(value);
            return changed ? InteractionEventResult.AcceptedResult(true, true) : InteractionEventResult.Failed("interaction.exploration.resource-rejected");
        }

        private InteractionEventResult ApplyCondition(PersistentConditionCategory expectedCategory)
        {
            PersistentConditionRuntime runtime = PersistentConditionRuntime.Instance;
            if (runtime == null) return InteractionEventResult.Failed("interaction.exploration.condition-service-missing");
            if (condition == null || condition.Category != expectedCategory) return InteractionEventResult.Failed("interaction.exploration.condition-invalid");
            string ownerId = string.IsNullOrWhiteSpace(targetCharacterId) ? PartyRuntime.Instance?.LeaderCharacterId : targetCharacterId.Trim();
            PersistentConditionMutationStatus status = runtime.TryAcquire(ownerId, condition);
            if (status == PersistentConditionMutationStatus.AlreadyAcquired) return InteractionEventResult.NoEffect("interaction.exploration.condition-duplicate");
            return status == PersistentConditionMutationStatus.Success
                ? InteractionEventResult.AcceptedResult(true, true)
                : InteractionEventResult.Failed("interaction.exploration.condition-rejected");
        }
    }
}
