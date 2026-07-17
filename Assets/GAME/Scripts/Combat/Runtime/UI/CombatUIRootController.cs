// Assets/GAME/Scripts/Combat/Runtime/UI/CombatUIRootController.cs
using Game.Combat.Core;
using Game.Combat.Model;
using Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        [SerializeField] private CombatPlanningHUD planningHUD;

        [Header("Optional UI Roots")]
        [SerializeField] private GameObject overworldCanvas;
        [SerializeField] private GameObject rewardCanvas;

        [Header("Initial State")]
        [SerializeField] private bool hideCombatHudOnAwake = true;
        [SerializeField] private bool autoBindOnAwake = true;
        [SerializeField] private bool logPhaseChanges;

        private CombatEntryPoint _subscribedEntryPoint;
        private CombatStateMachine _activeStateMachine;
        private CombatSession _activeSession;
        private bool _canonicalGlobalRouting;
        private bool _missingEntryWarned;
        private Phase? _lastAppliedPhase;

        internal CombatSession ActiveSession => _activeSession;
        internal CombatStateMachine ActiveStateMachine => _activeStateMachine;
        internal bool PlanningVisible => planningPanel != null && planningPanel.activeSelf;
        internal bool WidgetsVisible => widgetContainer != null && widgetContainer.activeSelf;
        internal bool UsesCanonicalGlobalRouting => _canonicalGlobalRouting;

        private void Awake()
        {
            if (autoBindOnAwake)
                AutoBindReferences();

            ResolveRoutingMode();
            if (hideCombatHudOnAwake)
                HideInternalCombatUI();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            RebindAndRecover();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnsubscribeFromEntryPoint();
            UnsubscribeFromStateMachine();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RebindAndRecover();
        }

        private void RebindAndRecover()
        {
            if (autoBindOnAwake)
                AutoBindReferences();

            ResolveRoutingMode();
            SubscribeToEntryPoint();

            if (entryPoint != null && entryPoint.ActiveSession != null && entryPoint.ActiveStateMachine != null)
                BindActiveCombat(entryPoint.ActiveSession, entryPoint.ActiveStateMachine);
            else
                HideInternalCombatUI();
        }

        private void AutoBindReferences()
        {
            if (entryPoint == null)
                entryPoint = FindFirstObjectByType<CombatEntryPoint>(FindObjectsInactive.Include);

            if (planningHUD == null)
                planningHUD = GetComponentInChildren<CombatPlanningHUD>(true);

            if (combatHudRoot == null)
            {
                Transform candidate = transform.Find("CombatHUD");
                if (candidate != null)
                    combatHudRoot = candidate.gameObject;
            }

            if (planningPanel == null && combatHudRoot != null)
            {
                Transform candidate = combatHudRoot.transform.Find("Panel_Planning");
                if (candidate != null)
                    planningPanel = candidate.gameObject;
            }

            if (widgetContainer == null)
            {
                Transform candidate = transform.Find("WidgetContainer");
                if (candidate != null)
                    widgetContainer = candidate.gameObject;
            }
        }

        private void ResolveRoutingMode()
        {
            _canonicalGlobalRouting = FindFirstObjectByType<UIScreenRouter>(FindObjectsInactive.Include) != null &&
                                      FindFirstObjectByType<GameUIRootController>(FindObjectsInactive.Include) != null;
        }

        private void SubscribeToEntryPoint()
        {
            if (_subscribedEntryPoint == entryPoint)
                return;

            UnsubscribeFromEntryPoint();
            _subscribedEntryPoint = entryPoint;
            if (_subscribedEntryPoint == null)
            {
                if (!_missingEntryWarned)
                {
                    _missingEntryWarned = true;
                    Debug.LogWarning("[CombatUIRootController] CombatEntryPoint is missing. Assign it in the Inspector.", this);
                }
                return;
            }

            _subscribedEntryPoint.OnCombatStarted += HandleCombatStarted;
            _subscribedEntryPoint.OnCombatEnded += HandleCombatEnded;
        }

        private void UnsubscribeFromEntryPoint()
        {
            if (_subscribedEntryPoint != null)
            {
                _subscribedEntryPoint.OnCombatStarted -= HandleCombatStarted;
                _subscribedEntryPoint.OnCombatEnded -= HandleCombatEnded;
            }

            _subscribedEntryPoint = null;
        }

        private void SubscribeToStateMachine(CombatStateMachine stateMachine)
        {
            if (_activeStateMachine == stateMachine)
                return;

            UnsubscribeFromStateMachine();
            _activeStateMachine = stateMachine;
            if (_activeStateMachine != null)
                _activeStateMachine.OnPhaseChanged += HandlePhaseChanged;
        }

        private void UnsubscribeFromStateMachine()
        {
            if (_activeStateMachine != null)
                _activeStateMachine.OnPhaseChanged -= HandlePhaseChanged;

            _activeStateMachine = null;
        }

        private void HandleCombatStarted(CombatSession session)
        {
            CombatStateMachine stateMachine = entryPoint != null ? entryPoint.ActiveStateMachine : null;
            BindActiveCombat(session, stateMachine);
        }

        private void BindActiveCombat(CombatSession session, CombatStateMachine stateMachine)
        {
            _activeSession = session;
            SubscribeToStateMachine(stateMachine);
            planningHUD?.Bind(session);
            ApplyPhase(stateMachine != null ? stateMachine.Phase : Phase.EnterCombat);
        }

        private void HandlePhaseChanged(Phase previous, Phase next)
        {
            ApplyPhase(next);
        }

        internal void ApplyPhase(Phase phase)
        {
            bool showCombatContent = phase == Phase.Planning ||
                                     phase == Phase.Resolution ||
                                     phase == Phase.EndTurn;
            bool showPlanning = phase == Phase.Planning;

            SetInternalVisible(combatHudRoot, showCombatContent);
            SetInternalVisible(widgetContainer, showCombatContent);

            if (showPlanning)
            {
                if (planningHUD != null)
                    planningHUD.EnterPlanning(_activeSession != null ? _activeSession.CurrentTurn : null);
                else
                    SetInternalVisible(planningPanel, true);
            }
            else
            {
                if (planningHUD != null)
                    planningHUD.ExitPlanning();
                else
                    SetInternalVisible(planningPanel, false);
            }

            ApplyLegacyGlobalFallback(showCombatContent);

            if (_lastAppliedPhase != phase && logPhaseChanges)
            {
                int turnIndex = _activeSession != null ? _activeSession.TurnIndex : -1;
                Debug.Log($"[CombatUIRootController] Phase={phase}, TurnIndex={turnIndex}, Planning={PlanningVisible}, Widgets={WidgetsVisible}", this);
            }

            _lastAppliedPhase = phase;
        }

        private void HandleCombatEnded(CombatResult result)
        {
            HideInternalCombatUI();
            UnsubscribeFromStateMachine();
            _activeSession = null;
            _lastAppliedPhase = null;
        }

        private void HideInternalCombatUI()
        {
            SetInternalVisible(combatHudRoot, false);
            SetInternalVisible(widgetContainer, false);
            if (planningHUD != null)
                planningHUD.ExitPlanning();
            else
                SetInternalVisible(planningPanel, false);

            ApplyLegacyGlobalFallback(false);
        }

        private void ApplyLegacyGlobalFallback(bool combatVisible)
        {
            if (_canonicalGlobalRouting)
                return;

            if (overworldCanvas != null && overworldCanvas.activeSelf == combatVisible)
                overworldCanvas.SetActive(!combatVisible);

            if (rewardCanvas != null && combatVisible && rewardCanvas.activeSelf)
                rewardCanvas.SetActive(false);
        }

        private void SetInternalVisible(GameObject root, bool visible)
        {
            if (root == null || root == gameObject || root.activeSelf == visible)
                return;

            root.SetActive(visible);
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Show Combat UI")]
        private void DebugShowCombatUI()
        {
            ApplyPhase(Phase.Planning);
        }

        [ContextMenu("Debug/Hide Combat UI")]
        private void DebugHideCombatUI()
        {
            HideInternalCombatUI();
        }
#endif
    }
}
