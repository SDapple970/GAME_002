using Game.Combat.Core;
using Game.Combat.Data;
using Game.NonCombat.Inventory;
using UnityEngine;

namespace Game.Combat.Integration
{
    /// <summary>
    /// Optional scene integration point for FinalExchange item authoring. It keeps the
    /// InventoryService dependency outside Combat core and does not own item quantities.
    /// </summary>
    public sealed class CombatItemUseAdapter : MonoBehaviour
    {
        [SerializeField] private CombatFlowOrchestrator flowOrchestrator;
        [SerializeField] private InventoryService inventoryService;
        [SerializeField] private CombatItemCatalogSO combatItemCatalog;

        private CombatItemUseExecutor _executor;

        private void Awake()
        {
            if (flowOrchestrator == null)
                flowOrchestrator = FindFirstObjectByType<CombatFlowOrchestrator>();
            if (inventoryService == null)
                inventoryService = InventoryService.Instance;
        }

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            flowOrchestrator?.UnregisterCombatItemUseExecutor(_executor);
            _executor = null;
        }

        private void Bind()
        {
            if (flowOrchestrator == null)
                flowOrchestrator = FindFirstObjectByType<CombatFlowOrchestrator>();
            if (inventoryService == null)
                inventoryService = InventoryService.Instance;

            if (flowOrchestrator == null || inventoryService == null || combatItemCatalog == null)
                return;

            _executor = new CombatItemUseExecutor(inventoryService, combatItemCatalog);
            flowOrchestrator.RegisterCombatItemUseExecutor(_executor);
        }
    }
}
