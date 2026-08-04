using UnityEngine;

namespace Game.Interaction
{
    public interface IInteractionVisualState
    {
        void ApplyConsumedState(bool consumed);
    }

    public sealed class InteractionVisualStateAdapter : MonoBehaviour, IInteractionVisualState
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Sprite availableSprite;
        [SerializeField] private Sprite consumedSprite;
        [SerializeField] private GameObject[] showWhenConsumed;
        [SerializeField] private GameObject[] hideWhenConsumed;
        [SerializeField] private Collider2D promptCollider;

        public void ApplyConsumedState(bool consumed)
        {
            if (targetRenderer != null)
            {
                Sprite sprite = consumed ? consumedSprite : availableSprite;
                if (sprite != null)
                    targetRenderer.sprite = sprite;
            }

            SetActive(showWhenConsumed, consumed);
            SetActive(hideWhenConsumed, !consumed);
            if (promptCollider != null)
                promptCollider.enabled = !consumed;
        }

        private static void SetActive(GameObject[] objects, bool active)
        {
            if (objects == null)
                return;

            for (int i = 0; i < objects.Length; i++)
                if (objects[i] != null)
                    objects[i].SetActive(active);
        }
    }
}
