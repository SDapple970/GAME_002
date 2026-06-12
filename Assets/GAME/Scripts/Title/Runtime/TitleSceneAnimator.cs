using System.Collections;
using UnityEngine;

namespace GAME.Title
{
    public sealed class TitleSceneAnimator : MonoBehaviour
    {
        [SerializeField] private float hiddenY = -900f;
        [SerializeField] private float shownY = 0f;
        [SerializeField] private float paperInDuration = 0.7f;
        [SerializeField] private float paperSwitchDuration = 0.35f;
        [SerializeField] private float stampDuration = 0.25f;
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private float sceneLoadDelay = 0.2f;

        public float SceneLoadDelay => Mathf.Max(0f, sceneLoadDelay);

        public IEnumerator PlayPaperIn(RectTransform paperRoot, CanvasGroup rootGroup)
        {
            if (paperRoot == null || rootGroup == null)
                yield break;

            rootGroup.alpha = 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;

            Vector2 start = paperRoot.anchoredPosition;
            start.y = hiddenY;
            Vector2 end = start;
            end.y = shownY;
            paperRoot.anchoredPosition = start;

            float duration = Mathf.Max(0.001f, paperInDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);

                paperRoot.anchoredPosition = Vector2.Lerp(start, end, eased);
                rootGroup.alpha = eased;
                yield return null;
            }

            paperRoot.anchoredPosition = end;
            rootGroup.alpha = 1f;
            rootGroup.interactable = true;
            rootGroup.blocksRaycasts = true;
        }

        public IEnumerator SwitchPaper(CanvasGroup from, CanvasGroup to)
        {
            if (from == null || to == null)
                yield break;

            from.interactable = false;
            from.blocksRaycasts = false;
            to.interactable = false;
            to.blocksRaycasts = false;

            float duration = Mathf.Max(0.001f, paperSwitchDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);

                from.alpha = 1f - eased;
                to.alpha = eased;
                yield return null;
            }

            from.alpha = 0f;
            to.alpha = 1f;
            to.interactable = true;
            to.blocksRaycasts = true;
        }

        public IEnumerator PlayStamp(CanvasGroup stampGroup, RectTransform stampRect)
        {
            if (stampGroup == null)
                yield break;

            stampGroup.alpha = 1f;
            stampGroup.interactable = false;
            stampGroup.blocksRaycasts = false;

            if (stampRect == null)
                yield break;

            float duration = Mathf.Max(0.001f, stampDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale;

                if (t < 0.7f)
                    scale = Mathf.Lerp(2f, 0.9f, Mathf.SmoothStep(0f, 1f, t / 0.7f));
                else
                    scale = Mathf.Lerp(0.9f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.7f) / 0.3f));

                stampRect.localScale = Vector3.one * scale;
                yield return null;
            }

            stampRect.localScale = Vector3.one;
        }

        public IEnumerator PlayFade(CanvasGroup fadeGroup)
        {
            if (fadeGroup == null)
                yield break;

            fadeGroup.interactable = false;
            fadeGroup.blocksRaycasts = false;

            float duration = Mathf.Max(0.001f, fadeDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                fadeGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
                yield return null;
            }

            fadeGroup.alpha = 1f;
            fadeGroup.interactable = true;
            fadeGroup.blocksRaycasts = true;
        }
    }
}
