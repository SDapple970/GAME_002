using Game.Interaction;
using Game.Quest;
using UnityEngine;

namespace Game.Story
{
    [CreateAssetMenu(menuName = "GAME/Story/Chapter Start Interaction Event", fileName = "ChapterStartInteractionEvent")]
    public sealed class ChapterStartInteractionEventSO : InteractionEventSO
    {
        [SerializeField] private CaseFileDataSO caseFile;
        [SerializeField] private bool useFirstUnlockedCaseFromBoard = true;
        [SerializeField] private int startStep;

        public override void Execute(InteractionContext context)
        {
            CaseFileDataSO selectedCase = ResolveCaseFile(context);
            if (selectedCase == null)
            {
                Debug.LogWarning("[ChapterStartInteractionEventSO] Case file is not assigned or no unlocked case exists.", context.Target);
                return;
            }

            if (!selectedCase.Unlocked)
            {
                Debug.Log($"[ChapterStartInteractionEventSO] Case is locked: {selectedCase.CaseTitle}", context.Target);
                context.Controller?.ShowTemporaryMessage("아직 확인할 수 없는 사건 파일이다.", 1.5f);
                return;
            }

            ChapterProgressManager chapterManager = ChapterProgressManager.Instance;
            if (chapterManager == null)
                chapterManager = Object.FindFirstObjectByType<ChapterProgressManager>();

            if (chapterManager != null)
            {
                chapterManager.StartChapter(selectedCase.ChapterId);
                chapterManager.SetStep(startStep);
            }
            else
            {
                Debug.LogWarning("[ChapterStartInteractionEventSO] ChapterProgressManager is missing.");
            }

            if (selectedCase.StartQuest != null)
            {
                QuestManager questManager = QuestManager.Instance != null ? QuestManager.Instance : Object.FindFirstObjectByType<QuestManager>();
                if (questManager != null)
                    questManager.StartQuest(selectedCase.StartQuest);
                else
                    Debug.LogWarning("[ChapterStartInteractionEventSO] QuestManager is missing.");
            }

            string title = string.IsNullOrEmpty(selectedCase.CaseTitle) ? selectedCase.ChapterId.ToString() : selectedCase.CaseTitle;
            Debug.Log($"[ChapterStartInteractionEventSO] Start case: {title}", context.Target);
            context.Controller?.ShowTemporaryMessage($"{title} 시작", 0.5f);

            SceneTravelService.TravelTo(selectedCase.TargetSceneName, selectedCase.TargetSpawnPointId);
        }

        private CaseFileDataSO ResolveCaseFile(InteractionContext context)
        {
            if (useFirstUnlockedCaseFromBoard && context.Target != null)
            {
                CaseBoard board = context.Target.GetComponent<CaseBoard>();
                if (board != null && board.TryGetFirstUnlockedCase(out CaseFileDataSO boardCase))
                    return boardCase;
            }

            return caseFile;
        }
    }
}
