using Game.Combat.Model;
using Game.NonCombat.Inventory;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Game.NonCombat.Save;

namespace Game.Reward
{
    public sealed class RewardService : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer
    {
        private const string LegacyEmptyCombatSourceId = "combat:legacy-empty";

        public static RewardService Instance { get; private set; }

        [SerializeField] private CurrencyWallet currencyWallet;
        [SerializeField] private InventoryService inventoryService;

        private readonly Dictionary<string, RewardGrantResult> _combatGrantLedger = new();

        private bool _missingCurrencyWalletWarned;
        private bool _missingInventoryServiceWarned;
        private bool _missingProgressionWarned;
        private bool _duplicateCombatRewardWarned;

        internal int CombatGrantLedgerCount => _combatGrantLedger.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

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
                questId,
                gold,
                exp));
        }

        public RewardResult GrantMissionCompletion(string missionId, int gold, int exp)
        {
            return Grant(new RewardGrantRequest(
                RewardSourceType.MissionCompletion,
                missionId,
                gold,
                exp));
        }

        public RewardResult Grant(RewardGrantRequest request)
        {
            return new RewardResult(GrantReward(request));
        }

        public RewardGrantResult GrantReward(RewardGrantRequest request)
        {
            string sourceId = ResolveRequestSourceId(request);
            if (request.SourceType == RewardSourceType.Combat &&
                _combatGrantLedger.TryGetValue(sourceId, out RewardGrantResult recorded))
            {
                WarnDuplicateCombatReward(sourceId);
                return new RewardGrantResult(
                    recorded.SourceType,
                    recorded.SourceId,
                    recorded.Gold,
                    recorded.Exp,
                    recorded.ItemId,
                    recorded.ItemCount,
                    true);
            }

            int requestedGold = Mathf.Max(0, request.Gold);
            int acceptedExp = Mathf.Max(0, request.Exp);
            string requestedItemId = string.IsNullOrWhiteSpace(request.ItemId) ? null : request.ItemId;
            int requestedItemCount = requestedItemId != null ? Mathf.Max(0, request.ItemCount) : 0;

            int appliedGold = 0;
            int appliedItemCount = 0;
            if (requestedGold > 0 && GrantGold(requestedGold, request, sourceId))
                appliedGold = requestedGold;

            if (requestedItemCount > 0 && GrantItem(requestedItemId, requestedItemCount, request, sourceId))
                appliedItemCount = requestedItemCount;

            if (acceptedExp > 0)
                WarnMissingProgressionOnce(acceptedExp, request, sourceId);

            RewardGrantResult result = new RewardGrantResult(
                request.SourceType,
                sourceId,
                appliedGold,
                acceptedExp,
                appliedItemCount > 0 ? requestedItemId : null,
                appliedItemCount,
                false);

            // A combat request is consumed after its one application attempt. Retrying a
            // partial attempt could duplicate a channel that already succeeded.
            if (request.SourceType == RewardSourceType.Combat)
                _combatGrantLedger[sourceId] = result;

            return result;
        }

        public static RewardGrantRequest CreateCombatRewardRequest(CombatResult result, string sourceId)
        {
            string resolvedSourceId = !string.IsNullOrWhiteSpace(result?.CompletionId)
                ? result.CompletionId
                : !string.IsNullOrWhiteSpace(sourceId)
                    ? sourceId
                    : result != null
                        ? $"combat:{RuntimeHelpers.GetHashCode(result)}"
                        : "combat:null";

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
            _combatGrantLedger.Clear();
            _duplicateCombatRewardWarned = false;
        }

        public void CaptureSaveData(GameSaveData saveData)
        {
            if (saveData == null) return;
            saveData.reward ??= new RewardSaveData();
            saveData.reward.combatLedger.Clear();
            foreach (KeyValuePair<string, RewardGrantResult> pair in _combatGrantLedger)
            {
                RewardGrantResult result = pair.Value;
                if (string.IsNullOrWhiteSpace(pair.Key)) continue;
                saveData.reward.combatLedger.Add(new RewardLedgerSaveData
                {
                    sourceType = result.SourceType.ToString(), sourceId = pair.Key, gold = result.Gold,
                    exp = result.Exp, itemId = result.ItemId, itemCount = result.ItemCount
                });
            }
            saveData.reward.combatLedger.Sort((left, right) => string.CompareOrdinal(left.sourceId, right.sourceId));
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            _combatGrantLedger.Clear();
            if (saveData?.reward?.combatLedger == null) return;
            for (int i = 0; i < saveData.reward.combatLedger.Count; i++)
            {
                RewardLedgerSaveData entry = saveData.reward.combatLedger[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.sourceId) ||
                    !Enum.TryParse(entry.sourceType, out RewardSourceType sourceType) || sourceType != RewardSourceType.Combat)
                    continue;
                _combatGrantLedger[entry.sourceId] = new RewardGrantResult(sourceType, entry.sourceId,
                    Mathf.Max(0, entry.gold), Mathf.Max(0, entry.exp),
                    string.IsNullOrWhiteSpace(entry.itemId) ? null : entry.itemId, Mathf.Max(0, entry.itemCount), false);
            }
        }

        private static bool IsVictory(CombatResult result)
        {
            if (result == null)
                return false;

            return result.EndReason != CombatEndReason.None
                ? result.EndReason == CombatEndReason.Victory
                : result.IsWin;
        }

        private static string ResolveRequestSourceId(RewardGrantRequest request)
        {
            if (request.SourceType == RewardSourceType.Combat && string.IsNullOrWhiteSpace(request.SourceId))
                return LegacyEmptyCombatSourceId;

            return request.SourceId;
        }

        private bool GrantGold(int amount, RewardGrantRequest request, string sourceId)
        {
            CurrencyWallet wallet = currencyWallet != null ? currencyWallet : CurrencyWallet.Instance;
            if (wallet != null)
            {
                try
                {
                    wallet.AddGold(amount);
                    return true;
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[RewardService] CurrencyWallet failed while applying combat-safe reward. source={request.SourceType}, sourceId={sourceId}, exception={exception}", this);
                    return false;
                }
            }

            if (!_missingCurrencyWalletWarned)
            {
                _missingCurrencyWalletWarned = true;
                Debug.LogWarning($"[RewardService] CurrencyWallet is missing. Gold reward was not granted. source={request.SourceType}, sourceId={sourceId}", this);
            }

            return false;
        }

        private bool GrantItem(string itemId, int count, RewardGrantRequest request, string sourceId)
        {
            InventoryService inventory = inventoryService != null ? inventoryService : InventoryService.Instance;
            if (inventory != null)
            {
                try
                {
                    inventory.AddItem(itemId, count);
                    return true;
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[RewardService] InventoryService failed while applying combat-safe reward. source={request.SourceType}, sourceId={sourceId}, exception={exception}", this);
                    return false;
                }
            }

            if (!_missingInventoryServiceWarned)
            {
                _missingInventoryServiceWarned = true;
                Debug.LogWarning($"[RewardService] InventoryService is missing. Item reward was not granted. source={request.SourceType}, sourceId={sourceId}", this);
            }

            return false;
        }

        private void WarnMissingProgressionOnce(int exp, RewardGrantRequest request, string sourceId)
        {
            if (_missingProgressionWarned)
                return;

            _missingProgressionWarned = true;
            Debug.Log($"[RewardService] EXP {exp} received from {request.SourceType} ({sourceId}). CharacterProgressionService is not implemented yet.", this);
        }

        private void WarnDuplicateCombatReward(string sourceId)
        {
            if (_duplicateCombatRewardWarned)
                return;

            _duplicateCombatRewardWarned = true;
            Debug.LogWarning($"[RewardService] Duplicate combat reward blocked. sourceId={sourceId}", this);
        }
    }
}
