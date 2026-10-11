using System.Collections.Generic;
using System.Text;
using Game.Combat.Data;
using Game.Combat.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI
{
    /// <summary>
    /// Read-only FinalExchange status presentation. Runtime state remains owned by combat core.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FinalCombatStatusListPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject statusRoot;
        [SerializeField] private Text statusText;

        public void Render(CombatSession session)
        {
            if (session == null || session.FlowMode != CombatFlowMode.StandoffClashChain)
            {
                Clear();
                return;
            }

            var builder = new StringBuilder();
            AppendCombatants(builder, session, session.Allies, "ALLY");
            AppendCombatants(builder, session, session.Enemies, "ENEMY");

            bool hasStatuses = builder.Length > 0;
            if (statusText != null)
                statusText.text = hasStatuses ? builder.ToString() : string.Empty;

            if (statusRoot != null && statusRoot.activeSelf != hasStatuses)
                statusRoot.SetActive(hasStatuses);
        }

        public void Clear()
        {
            if (statusText != null)
                statusText.text = string.Empty;

            if (statusRoot != null && statusRoot.activeSelf)
                statusRoot.SetActive(false);
        }

        private static void AppendCombatants(
            StringBuilder builder,
            CombatSession session,
            IReadOnlyList<ICombatant> combatants,
            string sideLabel)
        {
            if (combatants == null)
                return;

            for (int i = 0; i < combatants.Count; i++)
            {
                ICombatant combatant = combatants[i];
                if (combatant == null ||
                    !session.TryGetCombatState(combatant, out CombatantCombatState state))
                {
                    continue;
                }

                IReadOnlyList<CombatStatusSnapshot> statuses = state.StatusSnapshots;
                for (int statusIndex = 0; statusIndex < statuses.Count; statusIndex++)
                {
                    CombatStatusSnapshot status = statuses[statusIndex];
                    if (status == null || status.IsExpired)
                        continue;

                    if (builder.Length > 0)
                        builder.Append('\n');

                    AppendStatus(builder, sideLabel, combatant, status);
                }
            }
        }

        private static void AppendStatus(
            StringBuilder builder,
            string sideLabel,
            ICombatant combatant,
            CombatStatusSnapshot status)
        {
            string id = string.IsNullOrWhiteSpace(status.StatusId) ? "UNKNOWN" : status.StatusId;
            string displayName = string.IsNullOrWhiteSpace(status.DisplayName) ? id : status.DisplayName;
            string polarity = status.Polarity == CombatStatusPolarity.Buff ? "BUFF" : "DEBUFF";

            builder.Append(sideLabel)
                .Append(' ')
                .Append(combatant.Id.Value)
                .Append(" | ")
                .Append(polarity)
                .Append(" | ")
                .Append(displayName)
                .Append(" [")
                .Append(id)
                .Append("] x")
                .Append(Mathf.Max(1, status.StackCount))
                .Append(" | ")
                .Append(FormatDuration(status))
                .Append(" | ACTIVE");
        }

        private static string FormatDuration(CombatStatusSnapshot status)
        {
            if (status.DurationKind != CombatStatusDurationKind.Explicit)
                return "UNTIL REMOVED";

            return status.RemainingDuration > 0
                ? $"EX {status.RemainingDuration}"
                : "PERSIST";
        }
    }
}
