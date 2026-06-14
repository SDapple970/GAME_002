using UnityEngine;
using Game.Combat.Core;
using Game.Combat.Model;

namespace Game.UI
{
    public sealed class CombatUIDialogueBlocker : MonoBehaviour
    {
        [SerializeField] private CombatEntryPoint entryPoint;
        [SerializeField] private GameObject timedChoiceDialogueRoot;
        [SerializeField] private GameObject dialogueHudRoot;
        [SerializeField] private CanvasGroup timedChoiceCanvasGroup;

        private bool _subscribed;
        private bool _missingEntryPointWarned;

        private void Awake()
        {
            AutoBindReferences();
        }

        private void OnEnable()
        {
            AutoBindReferences();
            SubscribeToEntryPoint();
        }

        private void OnDisable()
        {
            UnsubscribeFromEntryPoint();
        }

        private void AutoBindReferences()
        {
            if (entryPoint == null)
                entryPoint = FindFirstObjectByType<CombatEntryPoint>();
        }

        private void SubscribeToEntryPoint()
        {
            if (_subscribed)
                return;

            if (entryPoint == null)
            {
                WarnIfMissingEntryPoint();
                return;
            }

            entryPoint.OnCombatStarted += HandleCombatStarted;
            _subscribed = true;
        }

        private void UnsubscribeFromEntryPoint()
        {
            if (!_subscribed || entryPoint == null)
            {
                _subscribed = false;
                return;
            }

            entryPoint.OnCombatStarted -= HandleCombatStarted;
            _subscribed = false;
        }

        private void HandleCombatStarted(CombatSession session)
        {
            HideDialogueUi();
        }

        private void HideDialogueUi()
        {
            if (timedChoiceDialogueRoot != null)
                timedChoiceDialogueRoot.SetActive(false);

            if (dialogueHudRoot != null)
                dialogueHudRoot.SetActive(false);

            if (timedChoiceCanvasGroup == null)
                return;

            timedChoiceCanvasGroup.alpha = 0f;
            timedChoiceCanvasGroup.interactable = false;
            timedChoiceCanvasGroup.blocksRaycasts = false;
        }

        private void WarnIfMissingEntryPoint()
        {
            if (_missingEntryPointWarned)
                return;

            _missingEntryPointWarned = true;
            Debug.LogWarning("[CombatUIDialogueBlocker] CombatEntryPoint is missing. Dialogue UI will not be hidden automatically on combat start.", this);
        }
    }
}
