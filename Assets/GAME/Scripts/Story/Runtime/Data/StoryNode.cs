// Assets/GAME/Scripts/Story/Runtime/Data/StoryNode.cs
using System.Collections.Generic;
using UnityEngine;

namespace Game.Story.Data
{
    [System.Serializable]
    public sealed class StoryNode
    {
        [SerializeField] private string nodeId;
        [SerializeField] private string speakerName;
        [SerializeField] private Sprite portrait;
        [SerializeField, TextArea(2, 6)] private string body;
        [SerializeField] private List<StoryChoice> choices = new();
        [SerializeField] private bool useTimedChoices;
        [SerializeField] private float choiceTimeLimitSeconds = 5f;
        [SerializeField] private int timeoutChoiceIndex = 0;
        [SerializeField] private string timeoutNodeId;
        [SerializeField] private bool hideBubbleAfterChoice = false;
        [SerializeField] private string nextNodeId;
        [SerializeField] private List<StoryEffect> effects = new();
        [SerializeField] private bool endEvent;

        public string NodeId => nodeId;
        public string SpeakerName => speakerName;
        public Sprite Portrait => portrait;
        public string Body => body;
        public IReadOnlyList<StoryChoice> Choices => choices;
        public bool UseTimedChoices => useTimedChoices;
        public float ChoiceTimeLimitSeconds => choiceTimeLimitSeconds;
        public int TimeoutChoiceIndex => timeoutChoiceIndex;
        public string TimeoutNodeId => timeoutNodeId;
        public bool HideBubbleAfterChoice => hideBubbleAfterChoice;
        public string NextNodeId => nextNodeId;
        public IReadOnlyList<StoryEffect> Effects => effects;
        public bool EndEvent => endEvent;
    }
}
