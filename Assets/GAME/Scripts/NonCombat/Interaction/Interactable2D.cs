using Game.Core;
using Game.NonCombat.Dialogue;
using UnityEngine;

namespace Game.NonCombat.Interaction
{
    public sealed class Interactable2D : MonoBehaviour, INonCombatInteractable
    {
        [SerializeField] private DialogueNodeSO dialogueNode;
        [SerializeField] private DialogueController dialogueController;

        public bool CanInteract
        {
            get
            {
                if (dialogueNode == null) return false;
                return GameStateMachine.Instance == null || GameStateMachine.Instance.Is(GameState.Exploration);
            }
        }

        public void Interact()
        {
            if (!CanInteract) return;

            DialogueController controller = dialogueController != null ? dialogueController : DialogueController.Instance;
            if (controller == null)
            {
                Debug.LogWarning("[Interactable2D] DialogueController is missing.", this);
                return;
            }

            controller.StartDialogue(dialogueNode);
        }
    }
}
