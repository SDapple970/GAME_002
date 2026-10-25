using Game.Combat.Core;
using Game.Combat.Model;
using UnityEngine;

namespace Game.Combat.Debugging
{
    /// <summary>
    /// Attach only to a validation copy of CombatRuntime. All values and fallback choices here
    /// are fixture inputs, not authored game balance or a Production combat default.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatClashSeriesValidationOptIn : MonoBehaviour
    {
        [SerializeField] private CombatEntryPoint entryPoint;
        [SerializeField] private CombatFlowOrchestrator orchestrator;
        [SerializeField] private bool enableValidation;
        [SerializeField] private int attackerDurability;
        [SerializeField] private int responderDurability;
        [SerializeField] private int maxComparisons;
        [SerializeField] private float decisionDurationSeconds;

        private void OnEnable()
        {
            if (entryPoint == null)
                entryPoint = GetComponent<CombatEntryPoint>();
            if (orchestrator == null)
                orchestrator = GetComponent<CombatFlowOrchestrator>();
            if (entryPoint != null)
                entryPoint.OnCombatStarted += HandleCombatStarted;
        }

        private void OnDisable()
        {
            if (entryPoint != null)
                entryPoint.OnCombatStarted -= HandleCombatStarted;
        }

        private void HandleCombatStarted(CombatSession session)
        {
            if (!enableValidation || session?.FlowMode != CombatFlowMode.StandoffClashChain ||
                orchestrator == null)
                return;

            if (attackerDurability <= 0 || responderDurability <= 0 ||
                maxComparisons <= 0 || decisionDurationSeconds <= 0f)
            {
                Debug.LogError("[CombatClashSeriesValidationOptIn] Enter positive fixture values in the Inspector.", this);
                return;
            }

            CombatClashSeriesConfig config = new CombatClashSeriesConfig(
                attackerDurability, responderDurability, maxComparisons);
            if (!orchestrator.TryEnableClashSeriesValidation(
                    config, decisionDurationSeconds, SelectLastCommittedWinningSkill,
                    SelectFirstAvailableTimeoutAttack))
                Debug.LogError("[CombatClashSeriesValidationOptIn] Validation opt-in was rejected.", this);
        }

        private static ISkill SelectLastCommittedWinningSkill(CombatClashSeriesState series)
        {
            if (series == null || !series.FinalOutcome.HasValue)
                return null;

            bool attackerWon = series.FinalOutcome == CombatClashOutcome.AttackerWin ||
                               series.FinalOutcome == CombatClashOutcome.Unopposed;
            var skills = attackerWon
                ? series.CommittedAttackSkills : series.CommittedResponseSkills;
            return skills.Count > 0 ? skills[skills.Count - 1] : null;
        }

        private ISkill SelectFirstAvailableTimeoutAttack(CombatExchangeDecisionRequest request)
        {
            var skills = orchestrator.GetAvailableClashSeriesSkills(request);
            return skills.Count > 0 ? skills[0] : null;
        }
    }
}
