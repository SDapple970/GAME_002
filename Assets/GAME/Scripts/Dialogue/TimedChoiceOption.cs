using System;
using Game.Interaction;
using UnityEngine;

namespace Game.Dialogue
{
    [Serializable]
    public sealed class TimedChoiceOption
    {
        public string displayText;
        public InteractionEventSO[] afterSelectEvents;
    }
}
