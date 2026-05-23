// Assets/GAME/Scripts/Mission/Runtime/UI/MissionHUD.cs
using Game.Mission.Data;
using TMPro;
using UnityEngine;

namespace Game.Mission.UI
{
    public sealed class MissionHUD : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text missionTitleText;
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private MissionManager missionManager;

        private void Awake()
        {
            ResolveMissionManager();
            Refresh();
        }

        private void OnEnable()
        {
            ResolveMissionManager();
            if (missionManager != null)
            {
                missionManager.OnMissionStateChanged -= Refresh;
                missionManager.OnMissionStateChanged += Refresh;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (missionManager != null)
            {
                missionManager.OnMissionStateChanged -= Refresh;
            }
        }

        public void Refresh()
        {
            ResolveMissionManager();
            if (missionManager == null || missionManager.ActiveMissions == null || missionManager.ActiveMissions.Count == 0)
            {
                Hide();
                return;
            }

            MissionDefinitionSO mission = missionManager.ActiveMissions[0];
            if (mission == null)
            {
                Hide();
                return;
            }

            MissionObjective objective = GetFirstIncompleteRequiredObjective(mission);
            if (objective == null)
            {
                Hide();
                return;
            }

            if (missionTitleText != null)
            {
                missionTitleText.text = mission.Title ?? string.Empty;
            }
            else
            {
                Debug.LogWarning("[MissionHUD] Mission title text is not assigned.", this);
            }

            if (objectiveText != null)
            {
                objectiveText.text = objective.Description ?? string.Empty;
            }
            else
            {
                Debug.LogWarning("[MissionHUD] Objective text is not assigned.", this);
            }

            Show();
        }

        public void Show()
        {
            if (root != null)
            {
                root.SetActive(true);
            }
        }

        public void Hide()
        {
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private MissionObjective GetFirstIncompleteRequiredObjective(MissionDefinitionSO mission)
        {
            if (mission == null || mission.Objectives == null) return null;

            foreach (MissionObjective objective in mission.Objectives)
            {
                if (objective == null || objective.Optional) continue;
                if (!missionManager.IsObjectiveCompleted(mission.MissionId, objective.ObjectiveId))
                {
                    return objective;
                }
            }

            return null;
        }

        private void ResolveMissionManager()
        {
            if (missionManager != null) return;

            missionManager = MissionManager.Instance;
            if (missionManager != null) return;

#if UNITY_2023_1_OR_NEWER
            missionManager = FindFirstObjectByType<MissionManager>();
#else
            missionManager = FindObjectOfType<MissionManager>();
#endif
        }
    }
}
