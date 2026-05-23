// Assets/GAME/Scripts/Story/Runtime/StorySpeakerAnchor.cs
using UnityEngine;

namespace Game.Story
{
    public sealed class StorySpeakerAnchor : MonoBehaviour
    {
        [SerializeField] private Transform bubbleAnchor;
        [SerializeField] private Vector3 fallbackOffset = new(0f, 1.8f, 0f);

        public Vector3 GetBubbleWorldPosition()
        {
            return bubbleAnchor != null ? bubbleAnchor.position : transform.position + fallbackOffset;
        }
    }
}
