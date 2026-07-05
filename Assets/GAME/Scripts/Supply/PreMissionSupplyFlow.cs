using System;
using Game.Office;
using UnityEngine;

namespace Game.Supply
{
    public sealed class PreMissionSupplyFlow : MonoBehaviour
    {
        [SerializeField] private SupplyLoadoutService supplyLoadoutService;

        private MissionSelectResult _selectedMission;
        private bool _supplyConfirmedWithoutMissionWarned;

        public event Action<MissionSelectResult> OnMissionSelectedForSupply;
        public event Action<SupplyLoadout> OnSupplyLoadoutChanged;
        public event Action OnSupplyCompleted;

        private void Awake()
        {
            ResolveReferences();
        }

        public void SetSelectedMission(MissionSelectResult mission)
        {
            if (mission == null || !mission.success || string.IsNullOrWhiteSpace(mission.missionId))
            {
                _selectedMission = null;
                return;
            }

            _selectedMission = mission;
            OnMissionSelectedForSupply?.Invoke(_selectedMission);
        }

        public void SetSelectedMissionId(string missionId)
        {
            if (string.IsNullOrWhiteSpace(missionId))
            {
                _selectedMission = null;
                return;
            }

            _selectedMission = new MissionSelectResult
            {
                success = true,
                missionId = missionId
            };
            OnMissionSelectedForSupply?.Invoke(_selectedMission);
        }

        public void AddSupplyItem(string itemId, int count = 1)
        {
            ResolveReferences();
            supplyLoadoutService?.AddItem(itemId, count);
            PublishLoadoutChanged();
        }

        public void RemoveSupplyItem(string itemId, int count = 1)
        {
            ResolveReferences();
            supplyLoadoutService?.RemoveItem(itemId, count);
            PublishLoadoutChanged();
        }

        public void ClearSupplyLoadout()
        {
            ResolveReferences();
            supplyLoadoutService?.ClearLoadout();
            PublishLoadoutChanged();
        }

        public bool CompleteSupplyStep()
        {
            if (_selectedMission == null || string.IsNullOrWhiteSpace(_selectedMission.missionId))
            {
                WarnSupplyConfirmedWithoutMission();
                return false;
            }

            OnSupplyCompleted?.Invoke();
            return true;
        }

        private void PublishLoadoutChanged()
        {
            ResolveReferences();
            OnSupplyLoadoutChanged?.Invoke(supplyLoadoutService != null
                ? supplyLoadoutService.GetSnapshot()
                : new SupplyLoadout());
        }

        private void ResolveReferences()
        {
            if (supplyLoadoutService == null)
                supplyLoadoutService = FindFirstObjectByType<SupplyLoadoutService>(FindObjectsInactive.Include);
        }

        private void WarnSupplyConfirmedWithoutMission()
        {
            if (_supplyConfirmedWithoutMissionWarned)
                return;

            _supplyConfirmedWithoutMissionWarned = true;
            Debug.LogWarning("[PreMissionSupplyFlow] Supply confirmed with no selected mission.", this);
        }
    }
}
