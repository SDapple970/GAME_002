using UnityEngine;

namespace Game.Interaction
{
    [CreateAssetMenu(menuName = "GAME/Interaction/Object State Change Event", fileName = "ObjectStateChangeEvent")]
    public sealed class ObjectStateChangeEventSO : InteractionEventSO
    {
        [Header("Sprite")]
        [SerializeField] private Sprite openedSprite;
        [SerializeField] private string targetRendererChildName;
        [SerializeField] private SpriteRenderer targetRendererOverride;

        [Header("Optional Objects")]
        [SerializeField] private GameObject[] activateObjects;
        [SerializeField] private GameObject[] deactivateObjects;

        public override bool SupportsProductionExecution => true;

        public override void Execute(InteractionContext context)
        {
            SpriteRenderer renderer = targetRendererOverride;
            if (renderer == null)
                renderer = ResolveRenderer(context.Target);

            if (renderer != null && openedSprite != null)
                renderer.sprite = openedSprite;

            SetObjectsActive(activateObjects, true);
            SetObjectsActive(deactivateObjects, false);
        }

        public override InteractionEventResult ExecuteProduction(InteractionExecutionContext context)
        {
            Execute(context.LegacyContext);
            bool changed = openedSprite != null ||
                           (activateObjects != null && activateObjects.Length > 0) ||
                           (deactivateObjects != null && deactivateObjects.Length > 0);
            return changed
                ? InteractionEventResult.AcceptedResult(true, false)
                : InteractionEventResult.NoEffect();
        }

        private SpriteRenderer ResolveRenderer(InteractableObject target)
        {
            if (target == null)
                return null;

            if (!string.IsNullOrEmpty(targetRendererChildName))
            {
                Transform child = target.transform.Find(targetRendererChildName);
                if (child != null && child.TryGetComponent(out SpriteRenderer namedRenderer))
                    return namedRenderer;
            }

            return target.GetComponentInChildren<SpriteRenderer>();
        }

        private static void SetObjectsActive(GameObject[] objects, bool active)
        {
            if (objects == null)
                return;

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                    objects[i].SetActive(active);
            }
        }
    }
}
