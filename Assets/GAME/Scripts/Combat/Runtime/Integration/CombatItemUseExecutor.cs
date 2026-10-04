using System;
using System.Collections.Generic;
using Game.Combat.Data;
using Game.Combat.Model;
using Game.NonCombat.Inventory;

namespace Game.Combat.Integration
{
    /// <summary>
    /// Bridges immutable combat-item authoring to the inventory quantity owner. It has no
    /// phase authority; FinalExchange validates and advances the command separately.
    /// </summary>
    public class CombatItemUseExecutor : ICombatItemUseExecutor
    {
        private readonly InventoryService _inventory;
        private readonly CombatItemCatalogSO _catalog;

        public CombatItemUseExecutor(InventoryService inventory, CombatItemCatalogSO catalog)
        {
            _inventory = inventory;
            _catalog = catalog;
        }

        public IReadOnlyList<CombatItemOption> GetUsableItems(CombatSession session, ICombatant user)
        {
            if (_inventory == null || _catalog == null || !IsValidUser(session, user))
                return Array.Empty<CombatItemOption>();

            List<CombatItemOption> options = new List<CombatItemOption>();
            IReadOnlyList<CombatItemDefinitionSO> definitions = _catalog.Items;
            for (int i = 0; i < definitions.Count; i++)
            {
                CombatItemDefinitionSO definition = definitions[i];
                if (definition == null || string.IsNullOrEmpty(definition.ItemId) ||
                    _inventory.GetCount(definition.ItemId) <= 0 ||
                    !CanApplyToAnyTarget(session, definition, user))
                {
                    continue;
                }

                options.Add(new CombatItemOption(
                    definition.ItemId,
                    definition.DisplayName,
                    _inventory.GetCount(definition.ItemId),
                    definition.TargetRule));
            }

            return options.AsReadOnly();
        }

        public CombatItemUseResult TryUse(CombatSession session, CombatItemUseRequest request)
        {
            string itemId = request?.ItemId;
            if (_inventory == null || _catalog == null || request == null ||
                string.IsNullOrWhiteSpace(itemId) || !IsValidUser(session, request.User))
            {
                return CombatItemUseResult.Failure(CombatItemUseStatus.InvalidRequest, itemId);
            }

            if (!_catalog.TryGet(itemId, out CombatItemDefinitionSO definition))
                return CombatItemUseResult.Failure(CombatItemUseStatus.MissingDefinition, itemId);

            if (_inventory.GetCount(definition.ItemId) <= 0)
                return CombatItemUseResult.Failure(CombatItemUseStatus.InsufficientInventory, definition.ItemId);

            if (!IsValidTarget(session, definition, request.User, request.Target))
                return CombatItemUseResult.Failure(CombatItemUseStatus.InvalidTarget, definition.ItemId);

            CombatantCombatState targetState = session.GetCombatState(request.Target);
            if (!CanApplyEffect(targetState, definition))
                return CombatItemUseResult.Failure(CombatItemUseStatus.EffectNotApplicable, definition.ItemId);

            InventoryMutationResult removal = _inventory.TryRemoveItemDetailed(definition.ItemId, 1);
            if (removal.Status != InventoryMutationStatus.Success)
                return CombatItemUseResult.Failure(CombatItemUseStatus.InventoryMutationFailed, definition.ItemId);

            bool effectApplied;
            int hpRecovered;
            CombatStatusApplicationResult statusApplication;
            try
            {
                effectApplied = TryApplyEffect(
                    targetState,
                    definition,
                    out hpRecovered,
                    out statusApplication);
            }
            catch (Exception)
            {
                effectApplied = false;
                hpRecovered = 0;
                statusApplication = null;
            }

            if (effectApplied)
            {
                return new CombatItemUseResult(
                    CombatItemUseStatus.Success,
                    definition.ItemId,
                    true,
                    true,
                    hpRecovered,
                    statusApplication);
            }

            InventoryMutationResult rollback = _inventory.TryAddItem(definition.ItemId, 1);
            return new CombatItemUseResult(
                CombatItemUseStatus.EffectFailed,
                definition.ItemId,
                rollback.Status != InventoryMutationStatus.Success,
                false,
                0,
                null);
        }

        public IReadOnlyList<ICombatant> GetUsableTargets(
            CombatSession session,
            ICombatant user,
            CombatItemOption item)
        {
            if (_catalog == null || item == null || !IsValidUser(session, user) ||
                !_catalog.TryGet(item.ItemId, out CombatItemDefinitionSO definition))
            {
                return Array.Empty<ICombatant>();
            }

            if (definition.TargetRule == CombatItemTargetRule.Self)
            {
                return IsValidTarget(session, definition, user, user) &&
                       session.TryGetCombatState(user, out CombatantCombatState selfState) &&
                       CanApplyEffect(selfState, definition)
                    ? new[] { user }
                    : Array.Empty<ICombatant>();
            }

            List<ICombatant> targets = new List<ICombatant>();
            IReadOnlyList<ICombatant> allies = session.GetSide(Side.Allies);
            for (int i = 0; i < allies.Count; i++)
            {
                ICombatant target = allies[i];
                if (IsValidTarget(session, definition, user, target) &&
                    session.TryGetCombatState(target, out CombatantCombatState state) &&
                    CanApplyEffect(state, definition))
                {
                    targets.Add(target);
                }
            }

            return targets.AsReadOnly();
        }

        protected virtual bool TryApplyEffect(
            CombatantCombatState targetState,
            CombatItemDefinitionSO definition,
            out int hpRecovered,
            out CombatStatusApplicationResult statusApplication)
        {
            hpRecovered = 0;
            statusApplication = null;
            if (targetState == null || definition == null)
                return false;

            if (definition.EffectKind == CombatItemEffectKind.RestoreHp)
            {
                int hpBefore = targetState.CurrentHp;
                targetState.RestoreHp(definition.HpRecovery);
                hpRecovered = targetState.CurrentHp - hpBefore;
                return hpRecovered > 0;
            }

            statusApplication = targetState.ApplyStatus(definition.AppliedStatus);
            return statusApplication != null && statusApplication.Changed;
        }

        private static bool IsValidUser(CombatSession session, ICombatant user)
        {
            return user != null && user.Side == Side.Allies && user.HP > 0 &&
                   IsCurrentRosterMember(session, user) &&
                   session.TryGetCombatState(user, out CombatantCombatState state) &&
                   !state.IsPanicked;
        }

        private static bool IsValidTarget(
            CombatSession session,
            CombatItemDefinitionSO definition,
            ICombatant user,
            ICombatant target)
        {
            if (target == null || target.Side != Side.Allies || target.HP <= 0 ||
                !IsCurrentRosterMember(session, target) || !session.TryGetCombatState(target, out _))
            {
                return false;
            }

            return definition.TargetRule == CombatItemTargetRule.Self
                ? ReferenceEquals(user, target)
                : definition.TargetRule == CombatItemTargetRule.SingleAlly;
        }

        private static bool CanApplyToAnyTarget(
            CombatSession session,
            CombatItemDefinitionSO definition,
            ICombatant user)
        {
            if (definition == null || !definition.HasConfiguredEffect)
                return false;

            if (definition.TargetRule == CombatItemTargetRule.Self)
                return session.TryGetCombatState(user, out CombatantCombatState selfState) &&
                       CanApplyEffect(selfState, definition);

            IReadOnlyList<ICombatant> allies = session.GetSide(Side.Allies);
            for (int i = 0; i < allies.Count; i++)
            {
                ICombatant target = allies[i];
                if (IsValidTarget(session, definition, user, target) &&
                    session.TryGetCombatState(target, out CombatantCombatState state) &&
                    CanApplyEffect(state, definition))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanApplyEffect(CombatantCombatState targetState, CombatItemDefinitionSO definition)
        {
            if (targetState == null || definition == null || !definition.HasConfiguredEffect)
                return false;

            return definition.EffectKind == CombatItemEffectKind.RestoreHp
                ? targetState.CanRestoreHp(definition.HpRecovery)
                : targetState.CanApplyStatus(definition.AppliedStatus);
        }

        private static bool IsCurrentRosterMember(CombatSession session, ICombatant combatant)
        {
            if (session == null || combatant == null)
                return false;

            IReadOnlyList<ICombatant> roster = session.GetSide(combatant.Side);
            for (int i = 0; i < roster.Count; i++)
            {
                if (ReferenceEquals(roster[i], combatant))
                    return true;
            }

            return false;
        }
    }
}
