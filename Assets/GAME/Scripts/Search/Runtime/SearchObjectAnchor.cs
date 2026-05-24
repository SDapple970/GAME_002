using UnityEngine;

namespace Game.Search
{
    public sealed class SearchObjectAnchor : MonoBehaviour
    {
        [SerializeField] private Transform uiAnchor;
        [SerializeField] private Vector3 fallbackOffset = new(0f, 1.4f, 0f);

        public Vector3 GetWorldPosition()
        {
            if (uiAnchor != null)
            {
                return uiAnchor.position;
            }

            return transform.position + fallbackOffset;
        }
    }
}
