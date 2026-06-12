using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.DemoMission.Data;
using Game.DemoMission.Runtime;

namespace Game.DemoMission.UI
{
    public sealed class CaseFileAcceptController : MonoBehaviour
    {
        [SerializeField] private Button acceptButton;
        [SerializeField] private DemoMissionRuntime missionRuntime;
        [SerializeField] private CaseFilePanel caseFilePanel;
        [SerializeField] private Animator stampAnimator;
        [SerializeField] private string stampTriggerName = "Stamp";
        [SerializeField] private float sceneLoadDelay = 0.35f;
        [SerializeField] private DemoMissionDefinitionSO missionDefinition;

        private bool _buttonBound;
        private bool _accepting;

        private void Awake()
        {
            if (missionRuntime == null)
                missionRuntime = DemoMissionRuntime.GetOrCreate();
            if (caseFilePanel == null)
                caseFilePanel = FindFirstObjectByType<CaseFilePanel>();
        }

        private void OnEnable()
        {
            BindButton();
        }

        private void OnDisable()
        {
            UnbindButton();
        }

        public void AcceptMission()
        {
            if (_accepting)
                return;

            DemoMissionDefinitionSO mission = ResolveMission();
            if (mission == null)
            {
                Debug.LogError("[CaseFileAcceptController] Mission definition is not assigned.", this);
                return;
            }

            if (string.IsNullOrWhiteSpace(mission.dungeonSceneName))
            {
                Debug.LogError("[CaseFileAcceptController] Dungeon scene name is empty.", mission);
                return;
            }

            StartCoroutine(Co_AcceptMission(mission));
        }

        private IEnumerator Co_AcceptMission(DemoMissionDefinitionSO mission)
        {
            _accepting = true;

            if (missionRuntime == null)
                missionRuntime = DemoMissionRuntime.GetOrCreate();

            missionRuntime.SetMission(mission);
            missionRuntime.ResetMissionProgress();

            if (stampAnimator != null && !string.IsNullOrWhiteSpace(stampTriggerName))
            {
                stampAnimator.SetTrigger(stampTriggerName);
                yield return new WaitForSeconds(Mathf.Max(0f, sceneLoadDelay));
            }

            SceneManager.LoadScene(mission.dungeonSceneName);
        }

        private DemoMissionDefinitionSO ResolveMission()
        {
            if (missionDefinition != null)
                return missionDefinition;

            return caseFilePanel != null ? caseFilePanel.ActiveMission : null;
        }

        private void BindButton()
        {
            if (_buttonBound)
                return;

            if (acceptButton != null)
                acceptButton.onClick.AddListener(AcceptMission);
            else
                Debug.LogWarning("[CaseFileAcceptController] Accept button is not assigned.", this);

            _buttonBound = true;
        }

        private void UnbindButton()
        {
            if (!_buttonBound)
                return;

            if (acceptButton != null)
                acceptButton.onClick.RemoveListener(AcceptMission);

            _buttonBound = false;
        }
    }
}
