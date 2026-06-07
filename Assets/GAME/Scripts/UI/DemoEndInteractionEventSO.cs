using Game.Interaction;
using UnityEngine;

namespace Game.UI
{
    [CreateAssetMenu(menuName = "GAME/UI/Demo End Interaction Event", fileName = "DemoEndInteractionEvent")]
    public sealed class DemoEndInteractionEventSO : InteractionEventSO
    {
        public override void Execute(InteractionContext context)
        {
            DemoEndController controller = Object.FindFirstObjectByType<DemoEndController>();
            if (controller == null)
            {
                Debug.Log("[DemoEndInteractionEventSO] Demo End", context.Target);
                return;
            }

            controller.ShowDemoEnd();
        }
    }
}
