using System.Collections.Generic;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Model;
using Game.Core;
using Game.Interaction;
using Game.Quest;
using UnityEngine;

namespace Game.Tutorial
{
    [CreateAssetMenu(menuName = "GAME/Tutorial/Battle Start Interaction Event", fileName = "TutorialBattleStartInteractionEvent")]
    public sealed class TutorialBattleStartInteractionEventSO : InteractionEventSO
    {
        [SerializeField] private CombatEntryPoint entryPoint;
        [SerializeField] private List<GameObject> allies = new();
        [SerializeField] private List<GameObject> enemies = new();
        [SerializeField] private GameObject enemyObject;
        [SerializeField] private StartReason startReason = StartReason.PlayerFirstHit;
        [SerializeField] private Side initiativeSide = Side.Allies;
        [SerializeField] private OpeningEffectSO openingEffectOrNull;
        [SerializeField] private QuestId questToAdvance = QuestId.TutorialPermit;
        [SerializeField] private bool advanceQuestOnStart = true;
        [SerializeField] private bool advanceByOne = false;
        [SerializeField] private int setStepOnStart;

        public override void Execute(InteractionContext context)
        {
            if (GameStateMachine.Instance != null && !GameStateMachine.Instance.Is(GameState.Exploration))
                return;

            CombatEntryPoint resolvedEntryPoint = entryPoint != null ? entryPoint : Object.FindFirstObjectByType<CombatEntryPoint>();
            if (resolvedEntryPoint == null)
            {
                Debug.LogWarning("[TutorialBattleStartInteractionEventSO] CombatEntryPoint is missing.", context.Target);
                return;
            }

            List<GameObject> resolvedAllies = BuildAllies(context);
            List<GameObject> resolvedEnemies = BuildEnemies(context);

            if (resolvedAllies.Count == 0 || resolvedEnemies.Count == 0)
            {
                Debug.LogWarning(
                    $"[TutorialBattleStartInteractionEventSO] Combat start failed. " +
                    $"allies={resolvedAllies.Count}, enemies={resolvedEnemies.Count}",
                    context.Target
                );
                return;
            }

            CombatStartRequest request = new CombatStartRequest(
                startReason,
                initiativeSide,
                0,
                -1,
                openingEffectOrNull
            );
            request.AllyFieldObjects.AddRange(resolvedAllies);
            request.EnemyFieldObjects.AddRange(resolvedEnemies);

            bool started = resolvedEntryPoint.StartCombat(request);

            if (!started)
                return;

            if (advanceQuestOnStart)
                AdvanceQuest(context);
        }

        private List<GameObject> BuildAllies(InteractionContext context)
        {
            List<GameObject> result = new List<GameObject>();
            AddValidObjects(result, allies);

            if (result.Count == 0)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    result.Add(player);
                else if (context.Interactor != null)
                    result.Add(context.Interactor);
            }

            return result;
        }

        private List<GameObject> BuildEnemies(InteractionContext context)
        {
            List<GameObject> result = new List<GameObject>();
            AddValidObjects(result, enemies);

            if (result.Count == 0 && enemyObject != null && enemyObject.activeInHierarchy)
                result.Add(enemyObject);

            return result;
        }

        private void AdvanceQuest(InteractionContext context)
        {
            QuestManager manager = QuestManager.Instance != null ? QuestManager.Instance : Object.FindFirstObjectByType<QuestManager>();
            if (manager == null)
            {
                Debug.LogWarning("[TutorialBattleStartInteractionEventSO] QuestManager is missing.", context.Target);
                return;
            }

            if (!manager.IsActiveQuest(questToAdvance))
            {
                Debug.LogWarning($"[TutorialBattleStartInteractionEventSO] Active quest mismatch or missing. questId={questToAdvance}", context.Target);
                return;
            }

            if (advanceByOne)
                manager.AdvanceStep(questToAdvance);
            else
                manager.SetStep(questToAdvance, setStepOnStart);
        }

        private static void AddValidObjects(List<GameObject> target, List<GameObject> source)
        {
            if (source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                GameObject candidate = source[i];
                if (candidate != null && candidate.activeInHierarchy && !target.Contains(candidate))
                    target.Add(candidate);
            }
        }
    }
}
