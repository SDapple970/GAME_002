using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class StatusHUD : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text hungerText;
        [SerializeField] private TMP_Text stressText;

        private void Awake()
        {
            ResolveRoot();
            ResolveTexts();
            EnsureBackground(new Color(0.2f, 0.9f, 0.35f, 0.35f));
        }

        public void SetHp(int current, int max)
        {
            if (hpText != null)
            {
                hpText.text = $"HP {current}/{max}";
            }
        }

        public void SetHunger(int value)
        {
            if (hungerText != null)
            {
                hungerText.text = $"Hunger {value}";
            }
        }

        public void SetStress(int value)
        {
            if (stressText != null)
            {
                stressText.text = $"Stress {value}";
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

        private void ResolveTexts()
        {
            hpText ??= FindOrCreateText("HPText");
            hungerText ??= FindOrCreateText("HungerText");
            stressText ??= FindOrCreateText("StressText");
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
