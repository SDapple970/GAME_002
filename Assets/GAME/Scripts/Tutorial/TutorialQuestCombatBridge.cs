using Game.Combat.Core;
using Game.Combat.Model;
using Game.Quest;
using UnityEngine;

namespace Game.Tutorial
{
    public sealed class TutorialQuestCombatBridge : MonoBehaviour
    {
        [SerializeField] private CombatEntryPoint entryPoint;
        [SerializeField] private QuestId questId = QuestId.TutorialPermit;
        [SerializeField] private int stepOnWin;
        [SerializeField] private bool completeQuestOnWin;
        [SerializeField] private bool onlyWhenWin = true;

        private bool _subscribed;

        private void Awake()
        {
            AutoBindReferences();
        }

        private void OnEnable()
        {
            AutoBindReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void AutoBindReferences()
        {
            if (entryPoint == null)
                entryPoint = FindFirstObjectByType<CombatEntryPoint>();
        }

        private void Subscribe()
        {
            if (_subscribed || entryPoint == null)
                return;

            entryPoint.OnCombatEnded += HandleCombatEnded;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || entryPoint == null)
            {
                _subscribed = false;
                return;
            }

            entryPoint.OnCombatEnded -= HandleCombatEnded;
            _subscribed = false;
        }

        private void HandleCombatEnded(CombatResult result)
        {
            if (result == null)
                return;

            if (onlyWhenWin && !result.IsWin)
                return;

            QuestManager manager = QuestManager.Instance != null ? QuestManager.Instance : FindFirstObjectByType<QuestManager>();
            if (manager == null)
            {
                Debug.LogWarning("[TutorialQuestCombatBridge] QuestManager is missing.", this);
                return;
            }

            if (!manager.IsActiveQuest(questId))
            {
                Debug.LogWarning($"[TutorialQuestCombatBridge] Active quest mismatch or missing. questId={questId}", this);
                return;
            }

            if (completeQuestOnWin)
                manager.CompleteQuest(questId);
            else
                manager.SetStep(questId, stepOnWin);
        }
    }
}
