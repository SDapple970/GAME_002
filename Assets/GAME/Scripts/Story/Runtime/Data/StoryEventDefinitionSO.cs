// Assets/GAME/Scripts/Story/Runtime/Data/StoryEventDefinitionSO.cs
using System.Collections.Generic;
using UnityEngine;

namespace Game.Story.Data
{
    [CreateAssetMenu(menuName = "GAME/Story/Story Event")]
    public sealed class StoryEventDefinitionSO : ScriptableObject
    {
        [SerializeField] private string eventId;
        [SerializeField] private string startNodeId = "start";
        [SerializeField] private List<StoryNode> nodes = new();

        public string EventId => eventId;
        public string StartNodeId => startNodeId;
        public IReadOnlyList<StoryNode> Nodes => nodes;

        public StoryNode GetNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || nodes == null) return null;

            foreach (StoryNode node in nodes)
            {
                if (node != null && node.NodeId == nodeId)
                {
                    return node;
                }
            }

            return null;
        }
    }
}
