// Assets/GAME/Scripts/Story/Runtime/UI/WorldDialogueBubble.cs
using TMPro;
using UnityEngine;

namespace Game.Story.UI
{
    public sealed class WorldDialogueBubble : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private RectTransform bubbleRect;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Vector2 screenOffset = new(0f, 32f);

        private StorySpeakerAnchor _target;
        private bool _shown;

        private void Awake()
        {
            Hide();
        }

        private void LateUpdate()
        {
            if (!_shown || bubbleRect == null) return;

            if (_target == null)
            {
                bubbleRect.position = new Vector3(Screen.width * 0.5f + screenOffset.x, Screen.height * 0.72f + screenOffset.y, 0f);
                SetVisible(true);
                return;
            }

            Camera cameraToUse = ResolveCamera();
            if (cameraToUse == null) return;

            Vector3 screenPosition = cameraToUse.WorldToScreenPoint(_target.GetBubbleWorldPosition());
            if (screenPosition.z < 0f)
            {
                SetVisible(false);
                return;
            }

            bubbleRect.position = new Vector3(screenPosition.x + screenOffset.x, screenPosition.y + screenOffset.y, 0f);
            SetVisible(true);
        }

        public void Show(StorySpeakerAnchor target, string body)
        {
            _shown = true;
            SetTarget(target);
            SetText(body);
            SetVisible(true);
            LateUpdate();
        }

        public void Hide()
        {
            _shown = false;
            SetVisible(false);
        }

        public void SetText(string body)
        {
            if (bodyText != null)
            {
                bodyText.text = body ?? string.Empty;
            }
        }

        public void SetTarget(StorySpeakerAnchor target)
        {
            _target = target;
        }

        private Camera ResolveCamera()
        {
            if (worldCamera != null) return worldCamera;

            worldCamera = Camera.main;
            return worldCamera;
        }

        private void SetVisible(bool visible)
        {
            if (rootGroup != null)
            {
                rootGroup.alpha = visible ? 1f : 0f;
                rootGroup.interactable = visible;
                rootGroup.blocksRaycasts = visible;
            }

            if (root != null && root != gameObject && root.activeSelf != visible)
            {
                root.SetActive(visible);
            }
        }
    }
}
