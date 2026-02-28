// Scripts/UI/ScreenFader.cs
using System.Collections;
using UnityEngine;

namespace Game.UI
{
    public sealed class ScreenFader : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float fadeDuration = 0.35f;

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponentInChildren<CanvasGroup>(true);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
        }

        public Coroutine FadeOut(MonoBehaviour runner) => runner.StartCoroutine(FadeTo(1f));
        public Coroutine FadeIn(MonoBehaviour runner) => runner.StartCoroutine(FadeTo(0f));

        private IEnumerator FadeTo(float target)
        {
            if (canvasGroup == null) yield break;

            canvasGroup.blocksRaycasts = true;

            float start = canvasGroup.alpha;
            float t = 0f;

            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(start, target, t / fadeDuration);
                canvasGroup.alpha = a;
                yield return null;
            }

            canvasGroup.alpha = target;
            canvasGroup.blocksRaycasts = target > 0.01f;
        }
    }
}
