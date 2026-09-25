using System.Collections;
using Game.Core;
using UnityEngine;

namespace Game.Quest
{
    /// <summary>
    /// Starts an authored Dungeon quest only for a fresh scene entry. A save restore is
    /// observed before its snapshot is applied and is never converted into gameplay start.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonQuestStartAdapter : MonoBehaviour
    {
        [SerializeField] private QuestRuntime questRuntime;
        [SerializeField] private QuestDefinitionSO questDefinition;

        private SaveLoadService _saveLoadService;
        private bool _restoreObserved;
        private bool _startAttempted;

        private void Awake()
        {
            ResolveReferences();
            _restoreObserved = _saveLoadService != null &&
                               _saveLoadService.CurrentOperationState != SaveLoadService.OperationState.Idle;
        }

        private void Start()
        {
            StartCoroutine(StartAfterSceneInitialization());
        }

        private IEnumerator StartAfterSceneInitialization()
        {
            // Let the scene bootstrap apply its initial state. This intentionally does
            // not reuse the D1-05 Story adapter or its static bootstrap subscription.
            yield return null;
            TryStartFreshQuest();
        }

        private void TryStartFreshQuest()
        {
            if (_startAttempted || _restoreObserved)
                return;

            ResolveReferences();
            if (questRuntime == null || questDefinition == null ||
                (_saveLoadService != null &&
                 _saveLoadService.CurrentOperationState != SaveLoadService.OperationState.Idle))
            {
                return;
            }

            _startAttempted = true;
            if (questRuntime.GetQuestStatus(questDefinition.QuestId) != QuestStatus.Inactive)
                return;

            questRuntime.StartQuest(questDefinition);
        }

        private void ResolveReferences()
        {
            if (questRuntime == null)
                questRuntime = FindFirstObjectByType<QuestRuntime>();
            if (_saveLoadService == null)
                _saveLoadService = SaveLoadService.Instance != null
                    ? SaveLoadService.Instance
                    : FindFirstObjectByType<SaveLoadService>();
        }
    }
}
