using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class MapHUD : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Image mapImage;
        [SerializeField] private TMP_Text areaNameText;

        private void Awake()
        {
            ResolveRoot();
            ResolveReferences();
            EnsureBackground(new Color(1f, 0.55f, 0.15f, 0.35f));
        }

        public void SetAreaName(string areaName)
        {
            if (areaNameText != null)
            {
                areaNameText.text = string.IsNullOrEmpty(areaName) ? string.Empty : areaName;
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

        private void ResolveReferences()
        {
            if (mapImage == null)
            {
                Transform child = transform.Find("MapImage");
                if (child != null)
                {
                    mapImage = child.GetComponent<Image>() ?? child.gameObject.AddComponent<Image>();
                }
            }

            if (areaNameText == null)
            {
                Transform child = transform.Find("AreaNameText");
                if (child != null)
                {
                    areaNameText = child.GetComponent<TMP_Text>() ?? child.gameObject.AddComponent<TextMeshProUGUI>();
                }
            }
        }

        private void EnsureBackground(Color color)
        {
            Image image = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            image.color = color;
        }
    }
}
