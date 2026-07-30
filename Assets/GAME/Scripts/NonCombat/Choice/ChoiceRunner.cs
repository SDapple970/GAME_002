using Game.NonCombat.Chapter;
using Game.NonCombat.Inventory;
using Game.NonCombat.Progress;
using Game.Reward;
using Game.Systems.Persona;
using UnityEngine;

namespace Game.NonCombat.Choice
{
    public sealed class ChoiceRunner : MonoBehaviour
    {
        public static ChoiceRunner Instance { get; private set; }

        [SerializeField] private StoryFlagDatabase storyFlags;
        [SerializeField] private CurrencyWallet currencyWallet;
        [SerializeField] private InventoryService inventoryService;
        [SerializeField] private NonCombatChapterProgressManager chapterProgressManager;
        [SerializeField] private PersonaStatusManager personaStatusManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public bool AreConditionsMet(NonCombatChoiceCondition[] conditions)
        {
            if (conditions == null || conditions.Length == 0) return true;

            for (int i = 0; i < conditions.Length; i++)
            {
                if (!IsConditionMet(conditions[i]))
                    return false;
            }

            return true;
        }

        public bool IsConditionMet(NonCombatChoiceCondition condition)
        {
            if (condition == null) return true;

            StoryFlagDatabase flags = storyFlags != null ? storyFlags : StoryFlagDatabase.Instance;
            CurrencyWallet wallet = currencyWallet != null ? currencyWallet : CurrencyWallet.Instance;
            InventoryService inventory = inventoryService != null ? inventoryService : InventoryService.Instance;
            NonCombatChapterProgressManager chapter = chapterProgressManager != null ? chapterProgressManager : NonCombatChapterProgressManager.Instance;
            PersonaStatusManager persona = personaStatusManager != null ? personaStatusManager : PersonaStatusManager.Instance;

            switch (condition.Type)
            {
                case ChoiceConditionType.Always:
                    return true;
                case ChoiceConditionType.HasFlag:
                    return flags != null && flags.HasFlag(condition.Id);
                case ChoiceConditionType.MissingFlag:
                    return flags == null || !flags.HasFlag(condition.Id);
                case ChoiceConditionType.PersonaStatAtLeast:
                    return persona != null && persona.GetLevel(condition.PersonaStat) >= condition.Amount;
                case ChoiceConditionType.GoldAtLeast:
                    return wallet != null && wallet.Gold >= condition.Amount;
                case ChoiceConditionType.HasItem:
                    return inventory != null && inventory.GetCount(condition.Id) >= Mathf.Max(1, condition.Amount);
                case ChoiceConditionType.ChapterAtLeast:
                    return chapter != null && string.CompareOrdinal(chapter.CurrentChapterId, condition.Id) >= 0;
                default:
                    return false;
            }
        }

        public void ApplyOutcomes(ChoiceOutcome[] outcomes)
        {
            ApplyOutcomes(outcomes, null);
        }

        public void ApplyOutcomes(ChoiceOutcome[] outcomes, string sourceId)
        {
            if (outcomes == null) return;

            for (int i = 0; i < outcomes.Length; i++)
                ApplyOutcome(outcomes[i], sourceId, i);
        }

        public void ApplyOutcome(ChoiceOutcome outcome)
        {
            ApplyOutcome(outcome, null, 0);
        }

        private void ApplyOutcome(ChoiceOutcome outcome, string sourceId, int outcomeIndex)
        {
            if (outcome == null) return;

            StoryFlagDatabase flags = storyFlags != null ? storyFlags : StoryFlagDatabase.Instance;
            CurrencyWallet wallet = currencyWallet != null ? currencyWallet : CurrencyWallet.Instance;
            InventoryService inventory = inventoryService != null ? inventoryService : InventoryService.Instance;
            NonCombatChapterProgressManager chapter = chapterProgressManager != null ? chapterProgressManager : NonCombatChapterProgressManager.Instance;
            PersonaStatusManager persona = personaStatusManager != null ? personaStatusManager : PersonaStatusManager.Instance;

            switch (outcome.Type)
            {
                case ChoiceOutcomeType.SetFlag:
                    flags?.SetFlag(outcome.Id, true);
                    break;
                case ChoiceOutcomeType.ClearFlag:
                    flags?.SetFlag(outcome.Id, false);
                    break;
                case ChoiceOutcomeType.AddPersonaExp:
                    persona?.AddXp(outcome.PersonaStat, outcome.Amount);
                    break;
                case ChoiceOutcomeType.AddGold:
                    GrantChoiceReward(
                        sourceId,
                        outcomeIndex,
                        gold: Mathf.Max(0, outcome.Amount));
                    break;
                case ChoiceOutcomeType.RemoveGold:
                    wallet?.TrySpendGold(outcome.Amount);
                    break;
                case ChoiceOutcomeType.AddItem:
                    GrantChoiceReward(
                        sourceId,
                        outcomeIndex,
                        itemId: outcome.Id,
                        itemCount: Mathf.Max(1, outcome.Amount));
                    break;
                case ChoiceOutcomeType.RemoveItem:
                    inventory?.TryRemoveItem(outcome.Id, Mathf.Max(1, outcome.Amount));
                    break;
                case ChoiceOutcomeType.SetChapter:
                    chapter?.SetChapter(outcome.Id);
                    break;
                case ChoiceOutcomeType.CompleteObjective:
                    chapter?.CompleteObjective(outcome.Id);
                    break;
            }
        }

        private void GrantChoiceReward(
            string sourceId,
            int outcomeIndex,
            int gold = 0,
            string itemId = null,
            int itemCount = 0)
        {
            RewardService service = RewardService.Instance;
            if (service == null)
            {
                Debug.LogWarning("[ChoiceRunner] RewardService is missing. Choice reward was not granted.", this);
                return;
            }

            string resolvedSourceId = string.IsNullOrWhiteSpace(sourceId)
                ? $"compat-choice:{name}"
                : sourceId.Trim();
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                Debug.LogWarning(
                    $"[ChoiceRunner] Using compatibility choice identity '{resolvedSourceId}'. New production choice calls must pass an authored source ID.",
                    this);
            }

            service.GrantReward(new RewardGrantRequest(
                RewardSourceType.Choice,
                resolvedSourceId,
                gold: gold,
                itemId: itemId,
                itemCount: itemCount,
                actionId: $"outcome:{outcomeIndex}"));
        }
    }
}
