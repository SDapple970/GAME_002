using Game.Reward;
using UnityEngine;

namespace Game.Interaction
{
    [CreateAssetMenu(menuName = "GAME/Interaction/Random Loot Event", fileName = "RandomLootInteractionEvent")]
    public sealed class RandomLootInteractionEventSO : InteractionEventSO
    {
        [SerializeField] private RandomLootEntry[] entries;
        [SerializeField] private string rewardSourceId;
        [SerializeField] private bool addToInventoryIfAvailable = true;
        [SerializeField] private bool showMessage = true;
        [SerializeField] private float messageSeconds = 1.5f;

        public override bool SupportsProductionExecution => true;

        public override void Execute(InteractionContext context)
        {
            if (!TryPickEntry(out RandomLootEntry entry, out _))
            {
                Debug.LogWarning("[RandomLootInteractionEventSO] No loot entry could be selected. Check entries and weights.", context.Target);
                return;
            }

            InteractionEventResult result = ApplyEntry(
                context.Target,
                entry,
                ResolveCompatibilitySourceId(),
                entry.itemId,
                showMessage);
            if (!string.IsNullOrWhiteSpace(result.Message))
                context.Controller?.ShowTemporaryMessage(result.Message, messageSeconds);
        }

        public override InteractionEventResult ExecuteProduction(InteractionExecutionContext context)
        {
            RandomLootEntry entry;
            string outcomeId;
            bool persistent = context.Request.UsePolicy == InteractionUsePolicy.PersistentOnce;
            if (persistent && context.Runtime.TryGetResolvedOutcome(
                    context.Request.InteractionId,
                    context.ActionId,
                    out string savedOutcomeId))
            {
                if (!TryResolveEntry(savedOutcomeId, out entry, out outcomeId))
                {
                    Debug.LogWarning(
                        $"[RandomLootInteractionEventSO] Saved outcome '{savedOutcomeId}' no longer exists for action '{context.ActionId}'.",
                        context.Request.Source);
                    return InteractionEventResult.Failed("interaction.loot.saved-outcome-missing");
                }
            }
            else
            {
                if (!TryPickEntry(out entry, out outcomeId))
                    return InteractionEventResult.Failed("interaction.loot.no-valid-entry");

                if (persistent)
                    context.Runtime.RememberResolvedOutcome(context.Request.InteractionId, context.ActionId, outcomeId);
            }

            string explicitSourceId = InteractionIdentity.Normalize(rewardSourceId);
            string sourceId = explicitSourceId ?? context.Request.InteractionId;
            if (sourceId == null)
                return InteractionEventResult.Failed("interaction.loot.invalid-source");

            // A non-empty legacy override keeps the old source/item action identity.
            string resolvedActionId = explicitSourceId != null && ActionId == null
                ? entry.itemId
                : $"{context.ActionId}:{outcomeId}";
            return ApplyEntry(context.Request.Source, entry, sourceId, resolvedActionId, showMessage);
        }

        private InteractionEventResult ApplyEntry(
            Object logContext,
            RandomLootEntry entry,
            string sourceId,
            string resolvedActionId,
            bool includeMessage)
        {
            string message = BuildMessage(entry);
            if (!entry.isNothing && addToInventoryIfAvailable)
            {
                RewardService rewardService = RewardService.Instance;
                if (rewardService == null)
                {
                    Debug.LogWarning($"[RandomLootInteractionEventSO] RewardService is missing. sourceId='{sourceId}'.", logContext);
                    return InteractionEventResult.Failed("interaction.loot.service-missing");
                }

                RewardGrantResult grantResult = rewardService.GrantReward(new RewardGrantRequest(
                    RewardSourceType.Loot,
                    sourceId,
                    itemId: entry.itemId,
                    itemCount: Mathf.Max(1, entry.amount),
                    actionId: resolvedActionId));
                if (grantResult.InvalidRequest)
                    return InteractionEventResult.Failed("interaction.loot.invalid-request");
                if (grantResult.DuplicateBlocked)
                    return InteractionEventResult.AcceptedResult(false, true, reward: grantResult);
                if (!grantResult.HasAnyReward)
                    return InteractionEventResult.Failed("interaction.loot.not-applied");

                Debug.Log($"[RandomLootInteractionEventSO] {message}", logContext);
                return InteractionEventResult.AcceptedResult(true, true, includeMessage ? message : null, grantResult);
            }

            if (entry.isNothing)
            {
                Debug.Log($"[RandomLootInteractionEventSO] {message}", logContext);
                return InteractionEventResult.AcceptedResult(true, true, includeMessage ? message : null);
            }

            return InteractionEventResult.NoEffect(includeMessage ? message : null);
        }

        private string ResolveCompatibilitySourceId()
        {
            if (!string.IsNullOrWhiteSpace(rewardSourceId))
                return rewardSourceId.Trim();

            Debug.LogWarning(
                $"[RandomLootInteractionEventSO] Using asset-name compatibility identity '{name}'. New production content must author interactionId.",
                this);
            return $"compat-loot:{name}";
        }

        private bool TryPickEntry(out RandomLootEntry selected, out string outcomeId)
        {
            selected = null;
            outcomeId = null;
            if (entries == null || entries.Length == 0)
                return false;

            float totalWeight = 0f;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i] != null)
                    totalWeight += Mathf.Max(0f, entries[i].weight);
            if (totalWeight <= 0f)
                return false;

            float roll = (float)new System.Random().NextDouble() * totalWeight;
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
                    outcomeId = ResolveEntryId(entry, i);
                    return true;
                }
            }

            for (int i = entries.Length - 1; i >= 0; i--)
            {
                if (entries[i] == null)
                    continue;
                selected = entries[i];
                outcomeId = ResolveEntryId(selected, i);
                return true;
            }

            return false;
        }

        private bool TryResolveEntry(string savedOutcomeId, out RandomLootEntry selected, out string outcomeId)
        {
            selected = null;
            outcomeId = null;
            if (entries == null)
                return false;

            for (int i = 0; i < entries.Length; i++)
            {
                RandomLootEntry candidate = entries[i];
                if (candidate == null || ResolveEntryId(candidate, i) != savedOutcomeId)
                    continue;
                selected = candidate;
                outcomeId = savedOutcomeId;
                return true;
            }

            return false;
        }

        private static string ResolveEntryId(RandomLootEntry entry, int index)
        {
            return entry?.StableId ?? $"entry:{index}";
        }

        private static string BuildMessage(RandomLootEntry entry)
        {
            if (entry == null || entry.isNothing)
                return "Nothing found.";

            string itemName = string.IsNullOrEmpty(entry.displayName) ? entry.itemId : entry.displayName;
            return $"{itemName} x{Mathf.Max(1, entry.amount)} found.";
        }

        private void OnValidate()
        {
            if (entries == null)
                return;

            System.Collections.Generic.HashSet<string> ids = new(System.StringComparer.Ordinal);
            for (int i = 0; i < entries.Length; i++)
            {
                string id = entries[i]?.StableId;
                if (id != null && !ids.Add(id))
                    Debug.LogWarning($"[RandomLootInteractionEventSO] Duplicate stable entryId '{id}'.", this);
            }
        }
    }
}
