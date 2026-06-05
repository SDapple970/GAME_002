using Game.Combat.Core;
using Game.Quest;
using Game.Story;
using UnityEngine;

namespace Game.Tutorial
{
    public sealed class TutorialSceneInstaller : MonoBehaviour
    {
        [SerializeField] private CombatEntryPoint combatEntryPoint;
        [SerializeField] private QuestManager questManager;
        [SerializeField] private ChapterProgressManager chapterProgressManager;
        [SerializeField] private SceneTravelService sceneTravelService;
        [SerializeField] private bool logOnStart = true;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            ResolveReferences();

            if (!logOnStart)
                return;

            Debug.Log(
                $"[TutorialSceneInstaller] " +
                $"CombatEntryPoint={(combatEntryPoint != null ? combatEntryPoint.name : "NULL")}, " +
                $"QuestManager={(questManager != null ? questManager.name : "NULL")}, " +
                $"ChapterProgressManager={(chapterProgressManager != null ? chapterProgressManager.name : "NULL")}, " +
                $"SceneTravelService={(sceneTravelService != null ? sceneTravelService.name : "NULL")}",
                this
            );
        }

        private void ResolveReferences()
        {
            if (combatEntryPoint == null)
                combatEntryPoint = FindFirstObjectByType<CombatEntryPoint>();

            if (questManager == null)
                questManager = QuestManager.Instance != null ? QuestManager.Instance : FindFirstObjectByType<QuestManager>();

            if (chapterProgressManager == null)
                chapterProgressManager = ChapterProgressManager.Instance != null ? ChapterProgressManager.Instance : FindFirstObjectByType<ChapterProgressManager>();

            if (sceneTravelService == null)
                sceneTravelService = SceneTravelService.Instance != null ? SceneTravelService.Instance : FindFirstObjectByType<SceneTravelService>();
        }
    }
}
