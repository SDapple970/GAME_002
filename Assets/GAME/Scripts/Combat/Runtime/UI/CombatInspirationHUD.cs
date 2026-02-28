using UnityEngine;
using UnityEngine.UI;
using Game.Combat.Core;
using Game.Combat.Model;

namespace Game.Combat.UI
{
    public sealed class CombatInspirationHUD : MonoBehaviour
    {
        [SerializeField] private CombatEntryPoint entryPoint;

        [Header("UI")]
        [SerializeField] private Slider inspirationSlider;
        [SerializeField] private Text inspirationText; // 없으면 비워도 됨 (TMP 쓰면 나중에 교체)

        private CombatSession _session;

        private void OnEnable()
        {
            if (entryPoint != null)
                entryPoint.OnCombatStarted += HandleCombatStarted;
        }

        private void OnDisable()
        {
            if (entryPoint != null)
                entryPoint.OnCombatStarted -= HandleCombatStarted;

            Unbind();
        }

        private void HandleCombatStarted(CombatSession session)
        {
            Unbind();
            _session = session;

            if (_session?.Inspiration != null)
            {
                _session.Inspiration.OnChanged += OnInspirationChanged;
                OnInspirationChanged(_session.Inspiration.Current, _session.Inspiration.Max);
            }
        }

        private void Unbind()
        {
            if (_session?.Inspiration != null)
                _session.Inspiration.OnChanged -= OnInspirationChanged;

            _session = null;
        }

        private void OnInspirationChanged(int current, int max)
        {
            if (inspirationSlider != null)
            {
                inspirationSlider.minValue = 0;
                inspirationSlider.maxValue = max;
                inspirationSlider.value = current; // UI는 value로 갱신 :contentReference[oaicite:1]{index=1}
            }

            if (inspirationText != null)
                inspirationText.text = $"Inspiration {current}/{max}";
        }
    }
}