using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class MissionTrackerHUD : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text missionTitleText;
        [SerializeField] private TMP_Text missionObjectiveText;

        private void Awake()
        {
            ResolveRoot();
            missionTitleText ??= FindOrCreateText("MissionTitleText");
            missionObjectiveText ??= FindOrCreateText("MissionObjectiveText");
            EnsureBackground(new Color(1f, 0.35f, 0.7f, 0.35f));
        }

        public void SetMission(string title, string objective)
        {
            if (missionTitleText != null)
            {
                missionTitleText.text = string.IsNullOrEmpty(title) ? string.Empty : title;
            }

            if (missionObjectiveText != null)
            {
                missionObjectiveText.text = string.IsNullOrEmpty(objective) ? string.Empty : objective;
            }
        }

        public void Clear()
        {
            SetMission(string.Empty, string.Empty);
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
