using Game.Core;
using Game.Daily;
using Game.NonCombat.Save;
using UnityEngine;

namespace Game.Office
{
    public sealed class OfficeFlowController : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer
    {
        [SerializeField] private DailyFlowController dailyFlowController;
        [SerializeField] private MissionSelectFlow missionSelectFlow;
        [SerializeField] private SceneFlowController sceneFlowController;
        [SerializeField] private bool enterOfficeOnEnable;
        [SerializeField] private bool loadMissionSceneOnSelection = true;

        private string _selectedMissionId;
        private MissionSelectResult _selectedMission;
        private bool _missingMissionSelectFlowWarned;
        private bool _invalidMissionWarned;
        private bool _missingTargetFieldSceneWarned;
        private bool _missingSceneFlowControllerWarned;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (missionSelectFlow != null)
                missionSelectFlow.OnMissionSelected += ReceiveSelectedMission;

            if (enterOfficeOnEnable)
                EnterOffice();
        }

        private void OnDisable()
        {
            if (missionSelectFlow != null)
                missionSelectFlow.OnMissionSelected -= ReceiveSelectedMission;
        }

        public void EnterOffice()
        {
            ResolveReferences();
            dailyFlowController?.EnterOffice();
        }

        public void RequestMissionSelection()
        {
            ResolveReferences();
            dailyFlowController?.EnterMissionSelect();

            if (missionSelectFlow == null)
            {
                WarnMissingMissionSelectFlow();
                return;
            }

            missionSelectFlow.RequestMissionSelection();
        }

        public void SelectMission(string missionId)
        {
            ResolveReferences();
            if (missionSelectFlow == null)
            {
                WarnMissingMissionSelectFlow();
                return;
            }

            missionSelectFlow.SelectMission(missionId);
        }

        public void ReceiveSelectedMission(MissionSelectResult result)
        {
            if (result == null || !result.success || string.IsNullOrWhiteSpace(result.missionId))
            {
                WarnInvalidMissionId(result != null ? result.missionId : null);
                return;
            }

            _selectedMission = result;
            _selectedMissionId = result.missionId;

            ResolveReferences();
            dailyFlowController?.EnterMission();

            if (loadMissionSceneOnSelection)
                EnterSelectedMissionField();
        }

        public void EnterSelectedMissionField()
        {
            if (_selectedMission == null)
            {
                WarnInvalidMissionId(_selectedMissionId);
                return;
            }

            if (string.IsNullOrWhiteSpace(_selectedMission.targetFieldSceneName))
            {
                WarnMissingTargetFieldScene(_selectedMission.missionId);
                return;
            }

            ResolveReferences();
            if (sceneFlowController == null)
            {
                WarnMissingSceneFlowController(_selectedMission.targetFieldSceneName);
                return;
            }

            sceneFlowController.LoadScene(_selectedMission.targetFieldSceneName);
        }

        public void CaptureSaveData(GameSaveData saveData)
        {
            if (saveData == null)
                return;

            saveData.futureDaily ??= new FutureDailySaveData();
            saveData.futureDaily.selectedMissionId = _selectedMissionId;
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            _selectedMissionId = saveData?.futureDaily?.selectedMissionId;
        }

        private void ResolveReferences()
        {
            if (dailyFlowController == null)
                dailyFlowController = FindFirstObjectByType<DailyFlowController>();

            if (missionSelectFlow == null)
                missionSelectFlow = FindFirstObjectByType<MissionSelectFlow>(FindObjectsInactive.Include);

            if (sceneFlowController == null)
                sceneFlowController = SceneFlowController.Instance != null
                    ? SceneFlowController.Instance
                    : FindFirstObjectByType<SceneFlowController>();
        }

        private void WarnMissingMissionSelectFlow()
        {
            if (_missingMissionSelectFlowWarned)
                return;

            _missingMissionSelectFlowWarned = true;
            Debug.LogWarning("[OfficeFlowController] MissionSelectFlow is missing. Mission selection cannot be requested.", this);
        }

        private void WarnInvalidMissionId(string missionId)
        {
            if (_invalidMissionWarned)
                return;

            _invalidMissionWarned = true;
            Debug.LogWarning($"[OfficeFlowController] Invalid mission selection ignored. missionId={missionId}", this);
        }

        private void WarnMissingTargetFieldScene(string missionId)
        {
            if (_missingTargetFieldSceneWarned)
                return;

            _missingTargetFieldSceneWarned = true;
            Debug.LogWarning($"[OfficeFlowController] Selected mission is missing a target field scene. missionId={missionId}", this);
        }

        private void WarnMissingSceneFlowController(string sceneName)
        {
            if (_missingSceneFlowControllerWarned)
                return;

            _missingSceneFlowControllerWarned = true;
            Debug.LogWarning($"[OfficeFlowController] SceneFlowController is missing. Cannot load mission scene. sceneName={sceneName}", this);
        }
    }
}
