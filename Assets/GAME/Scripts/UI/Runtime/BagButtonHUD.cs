using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class BagButtonHUD : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button bagButton;

        public event Action OnBagRequested;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (bagButton != null)
            {
                bagButton.onClick.AddListener(HandleBagClicked);
            }
        }

        private void OnDisable()
        {
            if (bagButton != null)
            {
                bagButton.onClick.RemoveListener(HandleBagClicked);
            }
        }

        public void Show()
        {
            ResolveReferences();
            root?.SetActive(true);
        }

        public void Hide()
        {
            ResolveReferences();
            root?.SetActive(false);
        }

        private void HandleBagClicked()
        {
            OnBagRequested?.Invoke();
        }

        private void ResolveReferences()
        {
            if (root == null)
            {
                root = gameObject;
            }

            if (bagButton == null)
            {
                bagButton = GetComponentInChildren<Button>();
            }

            if (bagButton == null)
            {
                bagButton = gameObject.AddComponent<Button>();
            }

            if (GetComponent<Image>() == null)
            {
                Image image = gameObject.AddComponent<Image>();
                image.color = new Color(0.45f, 0.85f, 1f, 0.35f);
            }

            Transform label = transform.Find("Text");
            if (label != null && label.GetComponent<TMP_Text>() == null)
            {
                TMP_Text text = label.gameObject.AddComponent<TextMeshProUGUI>();
                text.text = "Bag";
            }
        }
    }
}
