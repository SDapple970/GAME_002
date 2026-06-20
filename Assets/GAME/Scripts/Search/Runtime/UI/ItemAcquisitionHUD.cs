using System;
using Game.Search;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Search.UI
{
    public sealed class ItemAcquisitionHUD : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private TMP_Text itemDescriptionText;
        [SerializeField] private Button yesButton;
        [SerializeField] private TMP_Text yesButtonText;
        [SerializeField] private Button noButton;
        [SerializeField] private TMP_Text noButtonText;
        [SerializeField] private KeyCode yesKey = KeyCode.Y;
        [SerializeField] private KeyCode noKey = KeyCode.N;

        private Action _onAccept;
        private Action _onReject;
        private bool _visible;

        private void Awake()
        {
            ResolveFallbackReferences();
            ResolveChildReferences();
            EnsureBackgrounds();
            Hide();
        }

        private void Update()
        {
            if (!_visible) return;

            if (UnityEngine.Input.GetKeyDown(yesKey))
            {
                Accept();
            }
            else if (UnityEngine.Input.GetKeyDown(noKey))
            {
                Reject();
            }
        }

        public void Show(SearchRewardProposal proposal, Action onAccept, Action onReject)
        {
            ResolveFallbackReferences();
            ClearListeners();

            _onAccept = onAccept;
            _onReject = onReject;
            _visible = true;

            if (itemNameText != null)
            {
                itemNameText.text = proposal != null ? proposal.RewardName : string.Empty;
            }

            if (itemDescriptionText != null)
            {
                itemDescriptionText.text = proposal != null ? proposal.Description : string.Empty;
            }

            if (itemIcon != null)
            {
                itemIcon.sprite = proposal != null ? proposal.Icon : null;
                itemIcon.enabled = proposal?.Icon != null;
            }

            if (yesButtonText != null)
            {
                yesButtonText.text = "Yes";
            }

            if (noButtonText != null)
            {
                noButtonText.text = "No";
            }

            if (yesButton != null)
            {
                yesButton.onClick.AddListener(Accept);
            }

            if (noButton != null)
            {
                noButton.onClick.AddListener(Reject);
            }

            if (root != null)
            {
                root.SetActive(true);
            }

            if (rootGroup != null)
            {
                rootGroup.alpha = 1f;
                rootGroup.interactable = true;
                rootGroup.blocksRaycasts = true;
            }
        }

        public void Hide()
        {
            ClearListeners();
            _onAccept = null;
            _onReject = null;
            _visible = false;

            if (rootGroup != null)
            {
                rootGroup.alpha = 0f;
                rootGroup.interactable = false;
                rootGroup.blocksRaycasts = false;
            }

            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private void Accept()
        {
            Action callback = _onAccept;
            Hide();
            callback?.Invoke();
        }

        private void Reject()
        {
            Action callback = _onReject;
            Hide();
            callback?.Invoke();
        }

        private void ClearListeners()
        {
            yesButton?.onClick.RemoveAllListeners();
            noButton?.onClick.RemoveAllListeners();
        }

        private void ResolveFallbackReferences()
        {
            if (root == null)
            {
                root = gameObject;
            }

            if (rootGroup == null)
            {
                rootGroup = GetComponent<CanvasGroup>();
            }

            if (rootGroup == null)
            {
                rootGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void ResolveChildReferences()
        {
            itemIcon ??= FindOrCreateImage("ItemPreviewPanel/ItemIcon");
            itemNameText ??= FindOrCreateText("ItemPreviewPanel/ItemNameText");
            itemDescriptionText ??= FindOrCreateText("ItemPreviewPanel/ItemDescriptionText");
            yesButton ??= FindOrCreateButton("YesButton");
            yesButtonText ??= FindOrCreateText("YesButton/Text");
            noButton ??= FindOrCreateButton("NoButton");
            noButtonText ??= FindOrCreateText("NoButton/Text");
        }

        private Image FindOrCreateImage(string path)
        {
            Transform child = transform.Find(path);
            return child != null ? child.GetComponent<Image>() ?? child.gameObject.AddComponent<Image>() : null;
        }

        private TMP_Text FindOrCreateText(string path)
        {
            Transform child = transform.Find(path);
            return child != null ? child.GetComponent<TMP_Text>() ?? child.gameObject.AddComponent<TextMeshProUGUI>() : null;
        }

        private Button FindOrCreateButton(string path)
        {
            Transform child = transform.Find(path);
            if (child == null) return null;

            if (child.GetComponent<Image>() == null)
            {
                Image image = child.gameObject.AddComponent<Image>();
                image.color = new Color(1f, 0.85f, 0.2f, 0.65f);
            }

            return child.GetComponent<Button>() ?? child.gameObject.AddComponent<Button>();
        }

        private void EnsureBackgrounds()
        {
            Image rootImage = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            rootImage.color = new Color(0.2f, 0.5f, 1f, 0.35f);

            SetImageColor("ItemPreviewPanel", new Color(1f, 0.9f, 0.15f, 0.45f));
            SetImageColor("YesButton", new Color(1f, 0.9f, 0.15f, 0.75f));
            SetImageColor("NoButton", new Color(1f, 0.9f, 0.15f, 0.75f));
        }

        private void SetImageColor(string path, Color color)
        {
            Transform child = transform.Find(path);
            if (child == null) return;

            Image image = child.GetComponent<Image>() ?? child.gameObject.AddComponent<Image>();
            image.color = color;
        }
    }
}
