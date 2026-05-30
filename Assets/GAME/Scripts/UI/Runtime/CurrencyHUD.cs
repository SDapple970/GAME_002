using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class CurrencyHUD : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text currencyText;

        private void Awake()
        {
            ResolveRoot();
            currencyText ??= FindOrCreateText("CurrencyText");
            EnsureBackground(new Color(0.65f, 0.3f, 0.95f, 0.35f));
        }

        public void SetCurrency(int amount)
        {
            if (currencyText != null)
            {
                currencyText.text = amount.ToString();
            }
        }

        public void Show()
        {
            ResolveRoot();
            root?.SetActive(true);
        }

        public void Hide()
        {
            ResolveRoot();
            root?.SetActive(false);
        }

        private void ResolveRoot()
        {
            if (root == null)
            {
                root = gameObject;
            }
        }

        private TMP_Text FindOrCreateText(string childName)
        {
            Transform child = transform.Find(childName);
            if (child == null) return null;

            TMP_Text text = child.GetComponent<TMP_Text>();
            return text != null ? text : child.gameObject.AddComponent<TextMeshProUGUI>();
        }

        private void EnsureBackground(Color color)
        {
            Image image = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            image.color = color;
        }
    }
}
