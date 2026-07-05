using Game.Core;
using Game.Daily;
using Game.NonCombat.Save;
using Game.UI;
using UnityEngine;

namespace Game.Office
{
    public sealed class OfficeFlowController : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer
    {
        [SerializeField] private DailyFlowController dailyFlowController;
        [SerializeField] private MissionSelectFlow missionSelectFlow;
        [SerializeField] private MissionSelectPanel missionSelectPanel;
        [SerializeField] private SceneFlowController sceneFlowController;
        [SerializeField] private bool enterOfficeOnEnable;
        [SerializeField] private bool loadMissionSceneOnSelection = true;
        [SerializeField] private bool waitForSupplyBeforeFieldLoad;

        private string _selectedMissionId;
        private string _selectedTargetFieldSceneName;
        private string _selectedTargetSpawnPointId;
        private MissionSelectResult _selectedMission;
        private bool _selectedMissionFieldLoadStarted;
        private bool _missingMissionSelectFlowWarned;
        private bool _invalidMissionWarned;
        private bool _missingTargetFieldSceneWarned;
        private bool _missingSceneFlowControllerWarned;
        private bool _unsupportedSpawnPointWarned;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (missionSelectFlow != null)
                missionSelectFlow.OnMissionSelected += ReceiveSelectedMission;

            if (missionSelectPanel != null)
                missionSelectPanel.OnMissionSelected += SelectMission;

            if (enterOfficeOnEnable)
                EnterOffice();
        }

        private void OnDisable()
        {
            if (missionSelectFlow != null)
                missionSelectFlow.OnMissionSelected -= ReceiveSelectedMission;

            if (missionSelectPanel != null)
                missionSelectPanel.OnMissionSelected -= SelectMission;
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

        public void OpenMissionSelectPanel()
        {
            ResolveReferences();
            dailyFlowController?.EnterMissionSelect();

            if (missionSelectFlow == null)
            {
                WarnMissingMissionSelectFlow();
                return;
            }

            if (missionSelectPanel != null)
                missionSelectPanel.Show(missionSelectFlow.GetAvailableMissions());
            else
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

            bool sameSelection = _selectedMission != null && _selectedMissionId == result.missionId;
            if (sameSelection && _selectedMissionFieldLoadStarted)
                return;

            _selectedMission = result;
            _selectedMissionId = result.missionId;
            _selectedTargetFieldSceneName = result.targetFieldSceneName;
            _selectedTargetSpawnPointId = result.targetSpawnPointId;
            _selectedMissionFieldLoadStarted = false;

            ResolveReferences();

            if (loadMissionSceneOnSelection && !waitForSupplyBeforeFieldLoad)
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

            if (!string.IsNullOrWhiteSpace(_selectedMission.targetSpawnPointId))
                WarnSpawnPointUnsupported(_selectedMission.targetSpawnPointId);

            dailyFlowController?.EnterMission();
            _selectedMissionFieldLoadStarted = true;
            sceneFlowController.LoadScene(_selectedMission.targetFieldSceneName);
        }

        public void CaptureSaveData(GameSaveData saveData)
        {
            if (saveData == null)
                return;

            saveData.futureDaily ??= new FutureDailySaveData();
            saveData.futureDaily.selectedMissionId = _selectedMissionId;
            saveData.futureDaily.selectedMissionTargetFieldSceneName = _selectedTargetFieldSceneName;
            saveData.futureDaily.selectedMissionTargetSpawnPointId = _selectedTargetSpawnPointId;
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            _selectedMissionId = saveData?.futureDaily?.selectedMissionId;
            _selectedTargetFieldSceneName = saveData?.futureDaily?.selectedMissionTargetFieldSceneName;
            _selectedTargetSpawnPointId = saveData?.futureDaily?.selectedMissionTargetSpawnPointId;

            if (!string.IsNullOrWhiteSpace(_selectedMissionId))
            {
                _selectedMission = new MissionSelectResult
                {
                    success = true,
                    missionId = _selectedMissionId,
                    targetFieldSceneName = _selectedTargetFieldSceneName,
                    targetSpawnPointId = _selectedTargetSpawnPointId
                };
                _selectedMissionFieldLoadStarted = false;
            }
            else
            {
                _selectedMission = null;
                _selectedMissionFieldLoadStarted = false;
            }
        }

        private void ResolveReferences()
        {
            if (dailyFlowController == null)
                dailyFlowController = FindFirstObjectByType<DailyFlowController>();

            if (missionSelectFlow == null)
                missionSelectFlow = FindFirstObjectByType<MissionSelectFlow>(FindObjectsInactive.Include);

            if (missionSelectPanel == null)
                missionSelectPanel = FindFirstObjectByType<MissionSelectPanel>(FindObjectsInactive.Include);

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

        private void WarnSpawnPointUnsupported(string spawnPointId)
        {
            if (_unsupportedSpawnPointWarned)
                return;

            _unsupportedSpawnPointWarned = true;
            Debug.LogWarning($"[OfficeFlowController] Selected mission has a spawn point id, but SceneFlowController does not support spawn routing yet. spawnPointId={spawnPointId}", this);
        }
    }
}
