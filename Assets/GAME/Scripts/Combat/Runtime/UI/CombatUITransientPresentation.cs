using System.Collections;
using Game.Combat.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI
{
    /// <summary>A short-lived, non-interactive view. It never delays or completes combat requests.</summary>
    public sealed class CombatUITransientPresentation : MonoBehaviour
    {
        private float _duration;
        private Vector2 _drift;
        private bool _fadeIn;
        private bool _configured;
        private bool _started;

        private void OnEnable()
        {
            BeginAnimation();
        }

        private void OnDisable()
        {
            if (_started) Destroy(gameObject);
        }

        private void BeginAnimation()
        {
            if (!_configured || _started || !isActiveAndEnabled) return;
            _started = true;
            StartCoroutine(Animate((RectTransform)transform, GetComponent<CanvasGroup>(), _duration, _drift, _fadeIn));
        }

        internal static CombatUITransientPresentation Banner(RectTransform parent, Font font, string title, float duration)
        {
            GameObject view = new("StandoffPresentation", typeof(RectTransform), typeof(CanvasGroup));
            RectTransform rect = (RectTransform)view.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.2f, 0.5f);
            rect.anchorMax = new Vector2(0.8f, 0.7f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            AddTitle(rect, font, title);
            if (parent.Find("ReferenceFilmBed") != null)
            {
                rect.anchorMin = new Vector2(.14f, .65f);
                rect.anchorMax = new Vector2(.86f, 1f);
                Text label = rect.GetComponentInChildren<Text>();
                label.text = title.Replace("  #", "\n#");
                label.fontSize = label.resizeTextMaxSize = 172;
                label.fontStyle = FontStyle.Bold;
                label.rectTransform.localScale = new Vector3(2f, 1f, 1f);
                foreach (var (name, y, width) in new[] { ("TitleRule", .47f, 1f), ("IndexRule", .02f, .3f) })
                {
                    GameObject line = new(name, typeof(RectTransform), typeof(Image));
                    line.transform.SetParent(rect, false);
                    RectTransform lineRect = (RectTransform)line.transform;
                    lineRect.anchorMin = lineRect.anchorMax = new Vector2(.5f, y);
                    lineRect.sizeDelta = new Vector2(1430 * width, 6);
                    line.GetComponent<Image>().raycastTarget = false;
                }
            }
            return Play(rect, duration, new Vector2(0f, 18f), true);
        }

        internal static CombatUITransientPresentation Flash(RectTransform parent, Color color)
        {
            if (parent == null) return null;
            GameObject view = new("CombatStatusFlash", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            RectTransform rect = (RectTransform)view.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            view.GetComponent<Image>().color = color;
            return Play(rect, 0.22f, Vector2.zero, false);
        }

        internal static CombatUITransientPresentation Card(RectTransform card)
        {
            if (card.parent != null && card.parent.Find("ReferenceFilmBed") != null)
                return Play(card, 1.2f, Vector2.zero, false);
            return Play(card, 0.32f, new Vector2(0f, 90f), false);
        }

        internal static CombatUITransientPresentation Completion(RectTransform parent, Font font, CombatResult result)
        {
            // The existing Reward Canvas owns visibility, including completion-before-route ordering.
            GameObject view = new("CombatCompletionPresentation", typeof(RectTransform), typeof(CanvasGroup));
            view.transform.SetParent(parent, false);
            Stretch((RectTransform)view.transform);
            GameObject band = new("CompletionBand", typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)band.transform;
            rect.SetParent(view.transform, false);
            rect.anchorMin = new Vector2(0f, 0.72f);
            rect.anchorMax = new Vector2(1f, 0.92f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            band.GetComponent<Image>().color = new Color(0.03f, 0.05f, 0.1f, 0.85f);
            AddTitle(rect, font, result.EscapeSucceeded ? "ESCAPED" : result.IsWin ? "VICTORY" : "DEFEAT");
            return Play((RectTransform)view.transform, 0.55f, Vector2.zero, true);
        }

        private static CombatUITransientPresentation Play(RectTransform rect, float duration, Vector2 drift, bool fadeIn)
        {
            CanvasGroup group = rect.GetComponent<CanvasGroup>();
            if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = false;
            foreach (Graphic graphic in rect.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            foreach (Button button in rect.GetComponentsInChildren<Button>(true))
            {
                button.onClick.RemoveAllListeners();
                button.interactable = false;
            }
            CombatUITransientPresentation view = rect.gameObject.AddComponent<CombatUITransientPresentation>();
            view._duration = duration;
            view._drift = drift;
            view._fadeIn = fadeIn;
            view._configured = true;
            group.alpha = fadeIn ? 0f : 1f;
            view.BeginAnimation();
            return view;
        }

        private IEnumerator Animate(RectTransform rect, CanvasGroup group, float duration, Vector2 drift, bool fadeIn)
        {
            Vector2 origin = rect.anchoredPosition;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                group.alpha = fadeIn ? Mathf.Min(t / 0.15f, (1f - t) / 0.35f, 1f) : 1f - t;
                rect.anchoredPosition = origin + drift * t;
                yield return null;
            }
            Dismiss();
        }

        internal void Dismiss()
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private static void AddTitle(RectTransform parent, Font font, string title)
        {
            GameObject label = new("Title", typeof(RectTransform), typeof(Text));
            RectTransform rect = (RectTransform)label.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            Text text = label.GetComponent<Text>();
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = title;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 76;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 24;
            text.resizeTextMaxSize = 76;
            text.color = Color.white;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
