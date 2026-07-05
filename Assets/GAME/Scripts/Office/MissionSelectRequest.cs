using System;

namespace Game.Office
{
    [Serializable]
    public sealed class MissionSelectRequest
    {
        public string preferredMissionId;
        public bool includeLockedMissions;
    }
}
