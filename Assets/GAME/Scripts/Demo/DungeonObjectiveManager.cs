using System;
using Game.Quest;
using Game.UI;
using UnityEngine;

namespace Game.Demo
{
    public sealed class DungeonObjectiveManager : MonoBehaviour
    {
        [SerializeField] private int requiredMonsterKills = 3;
        [SerializeField] private int currentMonsterKills;
        [SerializeField] private bool npcRescued;
        [SerializeField] private DemoEndController demoEndController;
        [SerializeField] private QuestId questId = QuestId.BIC_Zone01;
        [SerializeField] private int stepOnAllMonstersKilled;
        [SerializeField] private int stepOnNpcRescued;
        [SerializeField] private bool showDemoEndOnComplete = true;

        private bool _allMonstersStepSet;
        private bool _npcRescueStepSet;
        private bool _completed;

        public event Action<int, int> OnKillCountChanged;
        public event Action OnNpcRescued;
        public event Action OnCompleted;

        public int RequiredMonsterKills => requiredMonsterKills;
        public int CurrentMonsterKills => currentMonsterKills;
        public bool NpcRescued => npcRescued;
        public bool IsCompleted => _completed;

        private void Awake()
        {
            if (demoEndController == null)
                demoEndController = FindFirstObjectByType<DemoEndController>();
        }

        public void RegisterMonsterKilled()
        {
            if (_completed)
                return;

            currentMonsterKills = Mathf.Min(currentMonsterKills + 1, Mathf.Max(0, requiredMonsterKills));
            OnKillCountChanged?.Invoke(currentMonsterKills, requiredMonsterKills);

            if (HasKilledRequiredMonsters() && !_allMonstersStepSet)
            {
                SetQuestStep(stepOnAllMonstersKilled);
                _allMonstersStepSet = true;
            }

            CheckComplete();
        }

        public void MarkNpcRescued()
        {
            if (npcRescued)
                return;

            npcRescued = true;
            OnNpcRescued?.Invoke();

            if (!_npcRescueStepSet)
            {
                SetQuestStep(stepOnNpcRescued);
                _npcRescueStepSet = true;
            }

            CheckComplete();
        }

        public void CheckComplete()
        {
            if (_completed || !HasKilledRequiredMonsters() || !npcRescued)
                return;

            _completed = true;
            OnCompleted?.Invoke();

            if (!showDemoEndOnComplete)
                return;

            if (demoEndController == null)
                demoEndController = FindFirstObjectByType<DemoEndController>();

            if (demoEndController != null)
                demoEndController.ShowDemoEnd();
            else
                Debug.Log("[DungeonObjectiveManager] Demo End", this);
        }

        private bool HasKilledRequiredMonsters()
        {
            return currentMonsterKills >= Mathf.Max(0, requiredMonsterKills);
        }

        private void SetQuestStep(int step)
        {
            if (questId == QuestId.None)
                return;

            QuestManager manager = QuestManager.Instance != null
                ? QuestManager.Instance
                : FindFirstObjectByType<QuestManager>();

            if (manager == null)
            {
                Debug.LogWarning("[DungeonObjectiveManager] QuestManager is missing.", this);
                return;
            }

            manager.SetStep(questId, step);
        }
    }
}
