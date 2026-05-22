// Assets/GAME/Scripts/Story/Runtime/Data/DialogueLine.cs
using UnityEngine;

namespace Game.Story.Data
{
    [System.Serializable]
    public sealed class DialogueLine
    {
        [SerializeField] private string speakerName;
        [SerializeField, TextArea(2, 5)] private string bodyText;
        [SerializeField] private string portraitKey;
        [SerializeField] private string optionalFlagToSet;
        [SerializeField] private bool optionalFlagValue = true;

        public string SpeakerName => speakerName;
        public string BodyText => bodyText;
        public string PortraitKey => portraitKey;
        public string OptionalFlagToSet => optionalFlagToSet;
        public bool OptionalFlagValue => optionalFlagValue;
    }
}
