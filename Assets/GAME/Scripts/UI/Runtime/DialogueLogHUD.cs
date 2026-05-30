using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class DialogueLogHUD : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text[] lineTexts;
        [SerializeField] private int maxLines = 4;

        private readonly Queue<string> _lines = new();

        private void Awake()
        {
            ResolveRoot();
            ResolveLineTexts();
            EnsureBackground(new Color(1f, 0.15f, 0.12f, 0.35f));
            Clear();
            Hide();
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

        public void Clear()
        {
            _lines.Clear();
            Refresh();
        }

        public void PushLine(string speaker, string body)
        {
            string line = string.IsNullOrEmpty(speaker)
                ? body
                : $"{speaker}: {body}";

            if (string.IsNullOrEmpty(line)) return;

            int limit = Mathf.Max(1, maxLines);
            while (_lines.Count >= limit)
            {
                _lines.Dequeue();
            }

            _lines.Enqueue(line);
            Refresh();
            Show();
        }

        private void Refresh()
        {
            if (lineTexts == null) return;

            string[] visibleLines = _lines.ToArray();
            for (int i = 0; i < lineTexts.Length; i++)
            {
                if (lineTexts[i] == null) continue;

                lineTexts[i].text = i < visibleLines.Length ? visibleLines[i] : string.Empty;
            }
        }

        private void ResolveRoot()
        {
            if (root == null)
            {
                root = gameObject;
            }
        }

        private void ResolveLineTexts()
        {
            if (lineTexts != null && lineTexts.Length > 0) return;

            int count = Mathf.Max(1, maxLines);
            lineTexts = new TMP_Text[count];
            for (int i = 0; i < count; i++)
            {
                Transform child = transform.Find($"DialogueLine_{i}");
                if (child == null) continue;

                lineTexts[i] = child.GetComponent<TMP_Text>() ?? child.gameObject.AddComponent<TextMeshProUGUI>();
            }
        }

        private void EnsureBackground(Color color)
        {
            Image image = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            image.color = color;
        }
    }
}
