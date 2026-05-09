// Assets/GAME/Scripts/UI/BattleTransitionController.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Core;
using Game.Battle;
using Game.UI;

namespace Game.Battle
{
    public sealed class BattleTransitionController : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private ScreenFader fader;

        [Header("Mode")]
        [SerializeField] private bool loadBattleSceneSingle = false;

        public static BattleTransitionRequest LastEncounterRequest { get; private set; }

        private void OnEnable()
        {
            // Legacy: BattleTrigger2D를 아직 쓰는 오브젝트가 있을 때만 처리.
            // FieldEnemy.OnBattleRequested는 더 이상 사용하지 않는다.
            BattleTrigger2D.OnBattleRequested += HandleBattleRequested;
        }

        private void OnDisable()
        {
            BattleTrigger2D.OnBattleRequested -= HandleBattleRequested;
        }

        private void HandleBattleRequested(BattleTransitionRequest req)
        {
            if (GameStateMachine.Instance == null)
                return;

            if (!GameStateMachine.Instance.Is(GameState.Exploration))
                return;

            StartCoroutine(Co_Transition(req));
        }

        private IEnumerator Co_Transition(BattleTransitionRequest req)
        {
            LastEncounterRequest = req;

            GameStateMachine.Instance.SetState(GameState.CombatTransition);

            if (fader != null)
                yield return fader.FadeOut(this);

            if (loadBattleSceneSingle && !string.IsNullOrEmpty(req.BattleSceneName))
            {
                AsyncOperation op = SceneManager.LoadSceneAsync(req.BattleSceneName, LoadSceneMode.Single);
                while (op != null && !op.isDone)
                    yield return null;
            }

            GameStateMachine.Instance.SetState(GameState.Combat);

            if (fader != null)
                yield return fader.FadeIn(this);

            Debug.Log($"[BattleTransitionController] Legacy transition handled. Advantage={req.Advantage}");
        }
    }
}