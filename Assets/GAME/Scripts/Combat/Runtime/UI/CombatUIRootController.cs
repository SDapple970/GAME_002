// Assets/GAME/Scripts/Combat/Runtime/UI/CombatUIRootController.cs
using UnityEngine;
using Game.Combat.Core;
using Game.Combat.Model;

namespace Game.Combat.UI
{
    [DisallowMultipleComponent]
    public sealed class CombatUIRootController : MonoBehaviour
    {
        [Header("Bind")]
        [SerializeField] private CombatEntryPoint entryPoint;

        [Header("Combat UI Roots")]
        [SerializeField] private GameObject combatHudRoot;
        [SerializeField] private GameObject planningPanel;
        [SerializeField] private GameObject widgetContainer;

        [Header("Optional UI Roots")]
        [SerializeField] private GameObject overworldCanvas;
        [SerializeField] private GameObject rewardCanvas;

        [Header("Initial State")]
        [SerializeField] private bool hideCombatHudOnAwake = true;
        [SerializeField] private bool autoBindOnAwake = true;

        private void Awake()
        {
            if (autoBindOnAwake)
                AutoBindReferences();

            if (hideCombatHudOnAwake)
                SetCombatVisible(false);
        }

        private void OnEnable()
        {
            if (autoBindOnAwake)
                AutoBindReferences();

            if (entryPoint != null)
            {
                entryPoint.OnCombatStarted -= HandleCombatStarted;
                entryPoint.OnCombatEnded -= HandleCombatEnded;

                entryPoint.OnCombatStarted += HandleCombatStarted;
                entryPoint.OnCombatEnded += HandleCombatEnded;
            }
            else
            {
                Debug.LogError("[CombatUIRootController] CombatEntryPoint가 연결되지 않았습니다.", this);
            }

            Debug.Log($"[CombatUIRootController] Enabled. EntryPoint={(entryPoint != null ? entryPoint.name : "NULL")}", this);
        }

        private void OnDisable()
        {
            if (entryPoint != null)
            {
                entryPoint.OnCombatStarted -= HandleCombatStarted;
                entryPoint.OnCombatEnded -= HandleCombatEnded;
            }
        }

        private void AutoBindReferences()
        {
            if (entryPoint == null)
                entryPoint = FindFirstObjectByType<CombatEntryPoint>();

            if (combatHudRoot == null)
            {
                Transform t = transform.Find("CombatHUD");
                if (t != null)
                    combatHudRoot = t.gameObject;
            }

            if (planningPanel == null && combatHudRoot != null)
            {
                Transform t = combatHudRoot.transform.Find("Panel_Planning");
                if (t != null)
                    planningPanel = t.gameObject;
            }

            if (widgetContainer == null)
            {
                Transform t = transform.Find("WidgetContainer");
                if (t != null)
                    widgetContainer = t.gameObject;
            }
        }

        private void HandleCombatStarted(CombatSession session)
        {
            Debug.Log("[CombatUIRootController] HandleCombatStarted received.", this);

            SetCombatVisible(true);

            Debug.Log(
                $"[CombatUIRootController] Combat UI shown. " +
                $"CombatHUD={(combatHudRoot != null ? combatHudRoot.activeSelf.ToString() : "NULL")}, " +
                $"PlanningPanel={(planningPanel != null ? planningPanel.activeSelf.ToString() : "NULL")}, " +
                $"WidgetContainer={(widgetContainer != null ? widgetContainer.activeSelf.ToString() : "NULL")}"
            );
        }

        private void HandleCombatEnded(CombatResult result)
        {
            SetCombatVisible(false);
            Debug.Log("[CombatUIRootController] Combat UI hidden.");
        }

        private void SetCombatVisible(bool visible)
        {
            if (combatHudRoot != null)
                combatHudRoot.SetActive(visible);

            if (widgetContainer != null)
                widgetContainer.SetActive(visible);

            // Planning 패널은 CombatPlanningHUD가 Planning 페이즈에서 다시 켠다.
            if (planningPanel != null)
                planningPanel.SetActive(false);

            if (overworldCanvas != null)
                overworldCanvas.SetActive(!visible);

            if (rewardCanvas != null && visible)
                rewardCanvas.SetActive(false);
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Show Combat UI")]
        private void DebugShowCombatUI()
        {
            SetCombatVisible(true);
        }

        [ContextMenu("Debug/Hide Combat UI")]
        private void DebugHideCombatUI()
        {
            SetCombatVisible(false);
        }
#endif
    }
}