using UnityEngine;

namespace Game.Interaction
{
    [CreateAssetMenu(menuName = "GAME/Interaction/Dialogue Event", fileName = "DialogueInteractionEvent")]
    public sealed class DialogueInteractionEventSO : InteractionEventSO
    {
        [SerializeField] private string[] lines;
        [SerializeField] private float secondsPerLine = 1.5f;
        [SerializeField] private bool logLines = true;

        public override void Execute(InteractionContext context)
        {
            if (lines == null || lines.Length == 0)
                return;

            if (logLines)
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!string.IsNullOrEmpty(lines[i]))
                        Debug.Log($"[Dialogue] {lines[i]}", context.Target);
                }
            }

            if (context.Controller != null)
                context.Controller.ShowMessageSequence(lines, secondsPerLine);
        }
    }
}
