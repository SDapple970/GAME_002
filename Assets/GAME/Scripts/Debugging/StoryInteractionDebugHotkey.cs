// Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionDebugHotkey.cs
using UnityEngine;

namespace Game.Story.Interaction
{
    public sealed class StoryInteractionDebugHotkey : MonoBehaviour
    {
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        private void Update()
        {
            if (!Input.GetKeyDown(interactKey)) return;

            if (StoryInteractionController.Instance != null)
            {
                StoryInteractionController.Instance.RefreshCurrentTarget();
                StoryInteractionController.Instance.TryInteractCurrent();
            }
        }
    }
}
