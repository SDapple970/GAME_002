using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Office
{
    public sealed class MissionSelectFlow : MonoBehaviour
    {
        [SerializeField] private MissionBoardDefinitionSO missionBoard;
        [SerializeField] private List<MissionEntry> missionEntries = new();

        private bool _invalidMissionWarned;
        private bool _duplicateMissionIdWarned;

        public event Action<IReadOnlyList<MissionEntry>> OnMissionListReady;
        public event Action<MissionSelectResult> OnMissionSelected;

        public IReadOnlyList<MissionEntry> GetAvailableMissions(bool includeLockedMissions = false)
        {
            List<MissionEntry> available = new();
            HashSet<string> seenMissionIds = new();
            AppendEntries(available, seenMissionIds, missionBoard != null ? missionBoard.Missions : null, includeLockedMissions);
            AppendEntries(available, seenMissionIds, missionEntries, includeLockedMissions);
            return available;
        }

        public void RequestMissionSelection()
        {
            RequestMissionSelection(new MissionSelectRequest());
        }

        public void RequestMissionSelection(MissionSelectRequest request)
        {
            bool includeLocked = request != null && request.includeLockedMissions;
            IReadOnlyList<MissionEntry> available = GetAvailableMissions(includeLocked);
            OnMissionListReady?.Invoke(available);

            if (request != null && !string.IsNullOrWhiteSpace(request.preferredMissionId))
                SelectMission(request.preferredMissionId);
        }

        public bool SelectMission(string missionId)
        {
            MissionEntry entry = FindMission(missionId, false);
            if (entry == null)
            {
                WarnInvalidMissionId(missionId);
                OnMissionSelected?.Invoke(new MissionSelectResult { success = false, missionId = missionId });
                return false;
            }

            OnMissionSelected?.Invoke(MissionSelectResult.FromEntry(entry));
            return true;
        }

        private MissionEntry FindMission(string missionId, bool includeLockedMissions)
        {
            if (string.IsNullOrWhiteSpace(missionId))
                return null;

            IReadOnlyList<MissionEntry> available = GetAvailableMissions(includeLockedMissions);
            for (int i = 0; i < available.Count; i++)
            {
                MissionEntry entry = available[i];
                if (entry != null && entry.MissionId == missionId)
                    return entry;
            }

            return null;
        }

        private void AppendEntries(
            List<MissionEntry> target,
            HashSet<string> seenMissionIds,
            IReadOnlyList<MissionEntry> source,
            bool includeLockedMissions)
        {
            if (target == null || seenMissionIds == null || source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                MissionEntry entry = source[i];
                if (entry == null)
                    continue;

                if (!includeLockedMissions && !entry.Unlocked)
                    continue;

                if (string.IsNullOrWhiteSpace(entry.MissionId))
                    continue;

                if (!seenMissionIds.Add(entry.MissionId))
                {
                    WarnDuplicateMissionId(entry.MissionId);
                    continue;
                }

                target.Add(entry);
            }
        }

        private void WarnInvalidMissionId(string missionId)
        {
            if (_invalidMissionWarned)
                return;

            _invalidMissionWarned = true;
            Debug.LogWarning($"[MissionSelectFlow] Invalid mission id selected. missionId={missionId}", this);
        }

        private void WarnDuplicateMissionId(string missionId)
        {
            if (_duplicateMissionIdWarned)
                return;

            _duplicateMissionIdWarned = true;
            Debug.LogWarning($"[MissionSelectFlow] Duplicate mission id ignored. Keeping first valid mission entry. missionId={missionId}", this);
        }
    }
}
