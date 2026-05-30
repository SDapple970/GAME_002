using UnityEngine;

namespace Game.Search
{
    public sealed class SearchObjectVisualState2D : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Sprite defaultSprite;
        [SerializeField] private Sprite searchedSprite;
        [SerializeField] private GameObject defaultObject;
        [SerializeField] private GameObject searchedObject;
        [SerializeField] private Animator animator;
        [SerializeField] private string searchedTrigger = "Open";
        [SerializeField] private bool useAnimator;
        [SerializeField] private bool applyOnAwake = true;

        private bool _searched;

        public bool IsSearched => _searched;

        private void Awake()
        {
            ResolveFallbackReferences();

            if (applyOnAwake)
            {
                SetDefault();
            }
        }

        public void SetDefault()
        {
            _searched = false;

            if (targetRenderer != null && defaultSprite != null)
            {
                targetRenderer.sprite = defaultSprite;
            }

            if (defaultObject != null)
            {
                defaultObject.SetActive(true);
            }

            if (searchedObject != null)
            {
                searchedObject.SetActive(false);
            }
        }

        public void SetSearched()
        {
            _searched = true;

            if (targetRenderer != null && searchedSprite != null)
            {
                targetRenderer.sprite = searchedSprite;
            }

            if (defaultObject != null)
            {
                defaultObject.SetActive(false);
            }

            if (searchedObject != null)
            {
                searchedObject.SetActive(true);
            }

            if (useAnimator && animator != null && !string.IsNullOrEmpty(searchedTrigger))
            {
                animator.SetTrigger(searchedTrigger);
            }
        }

        private void ResolveFallbackReferences()
        {
            if (targetRenderer != null) return;

            targetRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }
}
