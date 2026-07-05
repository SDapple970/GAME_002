using System.Collections.Generic;
using UnityEngine;

namespace Game.Office
{
    [CreateAssetMenu(menuName = "GAME/Office/Mission Board Definition", fileName = "MissionBoardDefinition")]
    public sealed class MissionBoardDefinitionSO : ScriptableObject
    {
        [SerializeField] private List<MissionEntry> missions = new();

        public IReadOnlyList<MissionEntry> Missions => missions;
    }
}
