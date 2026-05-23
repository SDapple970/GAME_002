// Assets/GAME/Scripts/Mission/Runtime/Data/MissionObjective.cs
using System;
using UnityEngine;

namespace Game.Mission.Data
{
    [Serializable]
    public sealed class MissionObjective
    {
        [SerializeField] private string objectiveId;
        [SerializeField] private string description;
        [SerializeField] private bool optional;

        public string ObjectiveId => objectiveId;
        public string Description => description;
        public bool Optional => optional;
    }
}
