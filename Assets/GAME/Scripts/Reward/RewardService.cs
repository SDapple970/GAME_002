using Game.Combat.Model;
using Game.Common.Identity;
using Game.NonCombat.Inventory;
using System;
using System.Collections.Generic;
using UnityEngine;
using Game.NonCombat.Save;
using Game.NonCombat.Progress;

namespace Game.Reward
{
    public sealed class RewardService : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer
    {
        private const string LegacyEmptyCombatSourceId = "legacy-empty-combat-result";

        public static RewardService Instance { get; private set; }

        [SerializeField] private CurrencyWallet currencyWallet;
        [SerializeField] private InventoryService inventoryService;
        [SerializeField] private CharacterProgressionService characterProgressionService;

        private readonly Dictionary<string, RewardGrantResult> _grantLedger = new();

        private bool _missingCurrencyWalletWarned;
        private bool _missingInventoryServiceWarned;
        private bool _missingProgressionWarned;
        private bool _duplicateRewardWarned;
        private bool _compatibilityIdentityWarned;
        private bool _invalidIdentityWarned;

        internal int CombatGrantLedgerCount => _grantLedger.Count;
        internal int GrantLedgerCount => _grantLedger.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public RewardResult GrantCombatResult(CombatResult result)
        {
            return result == null
                ? RewardResult.Empty
                : Grant(CreateCombatRewardRequest(result, null));
        }

        public RewardResult GrantQuestCompletion(string questId, int gold, int exp)
        {
            return Grant(new RewardGrantRequest(
                RewardSourceType.QuestCompletion,
                PrefixCompatibilityId("quest", questId),
                gold,
                exp));
        }

        public RewardResult GrantMissionCompletion(string missionId, int gold, int exp)
        {
            return Grant(new RewardGrantRequest(
                RewardSourceType.MissionCompletion,
                PrefixCompatibilityId("mission", missionId),
                gold,
                exp));
        }

        public RewardResult Grant(RewardGrantRequest request)
        {
            return new RewardResult(GrantReward(request));
        }

        public RewardGrantResult GrantReward(RewardGrantRequest request)
        {
            if (!TryResolveIdentity(request, out GameplayOutcomeIdentity identity))
            {
                WarnInvalidIdentity(request);
                return new RewardGrantResult(
                    request.SourceType,
                    request.SourceId,
                    Mathf.Max(0, request.Gold),
                    Mathf.Max(0, request.Exp),
                    0,
                    0,
                    NormalizeItemId(request.ItemId),
                    NormalizeItemCount(request.ItemId, request.ItemCount),
                    null,
                    0,
                    false,
                    HasRequestedReward(request),
                    true,
                    request.ActionId);
            }

            string ledgerKey = identity.CanonicalId;
            if (_grantLedger.TryGetValue(ledgerKey, out RewardGrantResult recorded))
            {
                WarnDuplicateReward(identity);
                return CreateDuplicateResult(recorded);
            }

            int requestedGold = Mathf.Max(0, request.Gold);
            int requestedExp = Mathf.Max(0, request.Exp);
            string requestedItemId = NormalizeItemId(request.ItemId);
            int requestedItemCount = NormalizeItemCount(requestedItemId, request.ItemCount);

            int appliedGold = 0;
            int appliedExp = 0;
            int appliedItemCount = 0;
            if (requestedGold > 0)
                appliedGold = GrantGold(requestedGold, request, identity.CanonicalId);

            if (requestedItemCount > 0)
                appliedItemCount = GrantItem(requestedItemId, requestedItemCount, request, identity.CanonicalId);

            string progressionTargetId = ResolveProgressionTarget(request.ProgressionTargetId);
            bool expSettled = requestedExp == 0;
            if (requestedExp > 0)
            {
                ExperienceApplyResult experience = GrantExperience(progressionTargetId, requestedExp, request, identity.CanonicalId);
                appliedExp = experience.AppliedExperience;
                expSettled = experience.Settled;
            }

            bool partialFailure = appliedGold != requestedGold ||
                                  appliedExp != requestedExp ||
                                  appliedItemCount != requestedItemCount;

            RewardGrantResult result = new RewardGrantResult(
                request.SourceType,
                identity.SourceId,
                requestedGold,
                requestedExp,
                appliedGold,
                appliedExp,
                requestedItemId,
                requestedItemCount,
                appliedItemCount > 0 ? requestedItemId : null,
                appliedItemCount,
                false,
                partialFailure,
                false,
                identity.ActionId,
                progressionTargetId,
                expSettled);

            // All sources use a permanently-consumed attempt. This is intentionally not
            // resumable: retrying a partial multi-channel grant could duplicate a channel
            // which already succeeded.
            _grantLedger[ledgerKey] = result;

            return result;
        }

        public static RewardGrantRequest CreateCombatRewardRequest(CombatResult result, string sourceId)
        {
            string resolvedSourceId = !string.IsNullOrWhiteSpace(result?.CompletionId)
                ? result.CompletionId
                : !string.IsNullOrWhiteSpace(sourceId)
                    ? sourceId
                    : LegacyEmptyCombatSourceId;

            if (!IsVictory(result))
                return new RewardGrantRequest(RewardSourceType.Combat, resolvedSourceId);

            return new RewardGrantRequest(
                RewardSourceType.Combat,
                resolvedSourceId,
                result.TotalGold,
                result.TotalExp);
        }

        internal void ResetCombatLedgerForTests()
        {
            _grantLedger.Clear();
            _duplicateRewardWarned = false;
            _compatibilityIdentityWarned = false;
            _invalidIdentityWarned = false;
        }

        public void CaptureSaveData(GameSaveData saveData)
        {
            if (saveData == null) return;
            saveData.reward ??= new RewardSaveData();
            saveData.reward.ledger.Clear();
            saveData.reward.combatLedger.Clear();
            List<KeyValuePair<string, RewardGrantResult>> ordered = new(_grantLedger);
            ordered.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
            foreach (KeyValuePair<string, RewardGrantResult> pair in ordered)
            {
                RewardGrantResult result = pair.Value;
                if (!TryCreateIdentity(result.SourceType, result.SourceId, result.ActionId, out _))
                    continue;

                RewardLedgerSaveData entry = new RewardLedgerSaveData
                {
                    sourceType = result.SourceType.ToString(),
                    sourceId = result.SourceId,
                    actionId = result.ActionId,
                    requestedGold = result.RequestedGold,
                    requestedExp = result.RequestedExp,
                    requestedItemId = result.RequestedItemId,
                    requestedItemCount = result.RequestedItemCount,
                    gold = result.Gold,
                    exp = result.Exp,
                    itemId = result.ItemId,
                    itemCount = result.ItemCount,
                    partialFailure = result.PartialFailure,
                    progressionTargetId = result.ProgressionTargetId,
                    expSettled = result.ExpSettled
                };
                saveData.reward.ledger.Add(entry);

                if (result.SourceType == RewardSourceType.Combat)
                    saveData.reward.combatLedger.Add(entry.Clone());
            }
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            _grantLedger.Clear();
            List<RewardLedgerSaveData> entries = saveData?.reward?.ledger;
            if (entries == null || entries.Count == 0)
                entries = saveData?.reward?.combatLedger;
            if (entries == null) return;

            for (int i = 0; i < entries.Count; i++)
            {
                RewardLedgerSaveData entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.sourceId) ||
                    !Enum.TryParse(entry.sourceType, out RewardSourceType sourceType) ||
                    !TryCreateIdentity(sourceType, entry.sourceId, entry.actionId, out GameplayOutcomeIdentity identity))
                    continue;

                int appliedGold = Mathf.Max(0, entry.gold);
                int appliedExp = Mathf.Max(0, entry.exp);
                string appliedItemId = NormalizeItemId(entry.itemId);
                int appliedItemCount = NormalizeItemCount(appliedItemId, entry.itemCount);
                int requestedGold = entry.requestedGold > 0 ? entry.requestedGold : appliedGold;
                int requestedExp = entry.requestedExp > 0 ? entry.requestedExp : appliedExp;
                string requestedItemId = NormalizeItemId(entry.requestedItemId) ?? appliedItemId;
                int requestedItemCount = entry.requestedItemCount > 0
                    ? entry.requestedItemCount
                    : appliedItemCount;
                bool partial = entry.partialFailure ||
                               requestedGold != appliedGold ||
                               requestedExp != appliedExp ||
                               requestedItemCount != appliedItemCount;
                _grantLedger[identity.CanonicalId] = new RewardGrantResult(
                    sourceType,
                    identity.SourceId,
                    requestedGold,
                    requestedExp,
                    appliedGold,
                    appliedExp,
                    requestedItemId,
                    requestedItemCount,
                    appliedItemId,
                    appliedItemCount,
                    false,
                    partial,
                    false,
                    identity.ActionId,
                    entry.progressionTargetId,
                    entry.expSettled);
            }

            ReconcilePendingExperience();
        }

        private static bool IsVictory(CombatResult result)
        {
            if (result == null)
                return false;

            return result.EndReason != CombatEndReason.None
                ? result.EndReason == CombatEndReason.Victory
                : result.IsWin;
        }

        private bool TryResolveIdentity(
            RewardGrantRequest request,
            out GameplayOutcomeIdentity identity)
        {
            string sourceId = request.SourceId;
            if (request.SourceType == RewardSourceType.Combat &&
                string.IsNullOrWhiteSpace(sourceId))
            {
                sourceId = LegacyEmptyCombatSourceId;
            }

            if (request.SourceType == RewardSourceType.Combat &&
                string.Equals(sourceId, LegacyEmptyCombatSourceId, StringComparison.Ordinal))
            {
                if (!_compatibilityIdentityWarned)
                {
                    _compatibilityIdentityWarned = true;
                    Debug.LogWarning(
                        "[RewardService] Combat reward used the legacy empty-ID compatibility identity. New production combat results require CompletionId.",
                        this);
                }
            }

            return TryCreateIdentity(request.SourceType, sourceId, request.ActionId, out identity);
        }

        private int GrantGold(int amount, RewardGrantRequest request, string sourceId)
        {
            CurrencyWallet wallet = currencyWallet != null ? currencyWallet : CurrencyWallet.Instance;
            if (wallet != null)
            {
                try
                {
                    return wallet.TryAddGold(amount).AppliedAmount;
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[RewardService] CurrencyWallet failed while applying combat-safe reward. source={request.SourceType}, sourceId={sourceId}, exception={exception}", this);
                    return 0;
                }
            }

            if (!_missingCurrencyWalletWarned)
            {
                _missingCurrencyWalletWarned = true;
                Debug.LogWarning($"[RewardService] CurrencyWallet is missing. Gold reward was not granted. source={request.SourceType}, sourceId={sourceId}", this);
            }

            return 0;
        }

        private int GrantItem(string itemId, int count, RewardGrantRequest request, string sourceId)
        {
            InventoryService inventory = inventoryService != null ? inventoryService : InventoryService.Instance;
            if (inventory != null)
            {
                try
                {
                    return inventory.TryAddItem(itemId, count).AppliedAmount;
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[RewardService] InventoryService failed while applying combat-safe reward. source={request.SourceType}, sourceId={sourceId}, exception={exception}", this);
                    return 0;
                }
            }

            if (!_missingInventoryServiceWarned)
            {
                _missingInventoryServiceWarned = true;
                Debug.LogWarning($"[RewardService] InventoryService is missing. Item reward was not granted. source={request.SourceType}, sourceId={sourceId}", this);
            }

            return 0;
        }

        public int ReconcilePendingExperience()
        {
            int settledCount = 0;
            List<string> keys = new(_grantLedger.Keys);
            foreach (string key in keys)
            {
                RewardGrantResult recorded = _grantLedger[key];
                int pending = Mathf.Max(0, recorded.RequestedExp - recorded.Exp);
                if (pending == 0 || recorded.ExpSettled) continue;
                string target = ResolveProgressionTarget(recorded.ProgressionTargetId);
                ExperienceApplyResult applied = GrantExperience(target, pending, default, key);
                if (!applied.Settled) continue;
                int totalApplied = recorded.Exp > int.MaxValue - applied.AppliedExperience ? int.MaxValue : recorded.Exp + applied.AppliedExperience;
                _grantLedger[key] = new RewardGrantResult(recorded.SourceType, recorded.SourceId, recorded.RequestedGold, recorded.RequestedExp,
                    recorded.Gold, totalApplied, recorded.RequestedItemId, recorded.RequestedItemCount, recorded.ItemId, recorded.ItemCount,
                    false, recorded.Gold != recorded.RequestedGold || totalApplied != recorded.RequestedExp || recorded.ItemCount != recorded.RequestedItemCount,
                    false, recorded.ActionId, target, true);
                settledCount++;
            }
            return settledCount;
        }

        private ExperienceApplyResult GrantExperience(string targetId, int amount, RewardGrantRequest request, string sourceId)
        {
            CharacterProgressionService progression = characterProgressionService != null ? characterProgressionService : CharacterProgressionService.Instance;
            if (progression == null || string.IsNullOrWhiteSpace(targetId))
            {
                WarnMissingProgressionOnce(amount, request, sourceId);
                return new ExperienceApplyResult(targetId, amount, 0, 0, 0, 0, 0, 0, ExperienceApplyStatus.Pending);
            }
            try { return progression.ApplyExperience(targetId, amount); }
            catch (Exception exception)
            {
                Debug.LogError($"[RewardService] Character progression failed. sourceId={sourceId}, target={targetId}, exception={exception}", this);
                return new ExperienceApplyResult(targetId, amount, 0, 0, 0, 0, 0, 0, ExperienceApplyStatus.Pending);
            }
        }

        private string ResolveProgressionTarget(string requestedTarget)
        {
            string normalized = CharacterProgressionService.NormalizeId(requestedTarget);
            if (normalized != null) return normalized;
            CharacterProgressionService progression = characterProgressionService != null ? characterProgressionService : CharacterProgressionService.Instance;
            return progression != null ? progression.DefaultRewardTargetId : null;
        }

        private void WarnMissingProgressionOnce(int exp, RewardGrantRequest request, string sourceId)
        {
            if (_missingProgressionWarned)
                return;

            _missingProgressionWarned = true;
            Debug.LogWarning($"[RewardService] EXP {exp} remains pending because the progression service or stable target is unavailable. source={request.SourceType}, sourceId={sourceId}", this);
        }

        private static bool TryCreateIdentity(
            RewardSourceType sourceType,
            string sourceId,
            string actionId,
            out GameplayOutcomeIdentity identity)
        {
            GameplayOutcomeSourceType outcomeType = sourceType switch
            {
                RewardSourceType.Combat => GameplayOutcomeSourceType.Combat,
                RewardSourceType.QuestCompletion => GameplayOutcomeSourceType.QuestCompletion,
                RewardSourceType.MissionCompletion => GameplayOutcomeSourceType.MissionCompletion,
                RewardSourceType.Interaction => GameplayOutcomeSourceType.Interaction,
                RewardSourceType.Story => GameplayOutcomeSourceType.Story,
                RewardSourceType.Choice => GameplayOutcomeSourceType.Choice,
                RewardSourceType.Loot => GameplayOutcomeSourceType.Loot,
                RewardSourceType.Tutorial => GameplayOutcomeSourceType.Tutorial,
                _ => GameplayOutcomeSourceType.Unknown
            };
            return GameplayOutcomeIdentity.TryCreate(outcomeType, sourceId, actionId, out identity);
        }

        private static RewardGrantResult CreateDuplicateResult(RewardGrantResult recorded)
        {
            return new RewardGrantResult(
                recorded.SourceType,
                recorded.SourceId,
                recorded.RequestedGold,
                recorded.RequestedExp,
                0,
                0,
                recorded.RequestedItemId,
                recorded.RequestedItemCount,
                null,
                0,
                true,
                recorded.PartialFailure,
                false,
                recorded.ActionId,
                recorded.ProgressionTargetId,
                recorded.ExpSettled);
        }

        private static string NormalizeItemId(string itemId)
        {
            return string.IsNullOrWhiteSpace(itemId) ? null : itemId.Trim();
        }

        private static int NormalizeItemCount(string itemId, int itemCount)
        {
            return string.IsNullOrWhiteSpace(itemId) ? 0 : Mathf.Max(0, itemCount);
        }

        private static bool HasRequestedReward(RewardGrantRequest request)
        {
            return request.Gold > 0 || request.Exp > 0 ||
                   (!string.IsNullOrWhiteSpace(request.ItemId) && request.ItemCount > 0);
        }

        private static string PrefixCompatibilityId(string prefix, string sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                return null;

            string trimmed = sourceId.Trim();
            return trimmed.StartsWith(prefix + ":", StringComparison.Ordinal)
                ? trimmed
                : $"{prefix}:{trimmed}";
        }

        private void WarnInvalidIdentity(RewardGrantRequest request)
        {
            if (_invalidIdentityWarned)
                return;

            _invalidIdentityWarned = true;
            Debug.LogWarning(
                $"[RewardService] Reward rejected because its production identity is invalid. sourceType={request.SourceType}, sourceId='{request.SourceId}', actionId='{request.ActionId}'.",
                this);
        }

        private void WarnDuplicateReward(GameplayOutcomeIdentity identity)
        {
            if (_duplicateRewardWarned)
                return;

            _duplicateRewardWarned = true;
            Debug.LogWarning($"[RewardService] Duplicate reward blocked. identity={identity.CanonicalId}", this);
        }
    }
}
