using UnityEngine;

namespace Game.NonCombat.Dialogue
{
    [CreateAssetMenu(menuName = "GAME/NonCombat/Dialogue Node", fileName = "DialogueNode")]
    public sealed class DialogueNodeSO : ScriptableObject
    {
        [SerializeField] private string speakerName;
        [TextArea(3, 8)]
        [SerializeField] private string bodyText;
        [SerializeField] private DialogueChoice[] choices;
        [SerializeField] private DialogueNodeSO nextNodeWhenNoChoice;

        public string SpeakerName => speakerName;
        public string BodyText => bodyText;
        public DialogueChoice[] Choices => choices;
        public DialogueNodeSO NextNodeWhenNoChoice => nextNodeWhenNoChoice;
    }
}
