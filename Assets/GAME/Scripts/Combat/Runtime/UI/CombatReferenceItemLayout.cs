using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI
{
    /// <summary>Keeps item labels level while their scroll positions follow the reference diagonal.</summary>
    public sealed class CombatReferenceItemLayout : LayoutGroup
    {
        private const float Step = 126f;
        private float _lastScrollY = float.NaN;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            SetLayoutInputForAxis(980f, 980f, -1f, 0);
        }

        public override void CalculateLayoutInputVertical() =>
            SetLayoutInputForAxis(rectChildren.Count * Step + 80f, rectChildren.Count * Step + 80f, -1f, 1);

        public override void SetLayoutHorizontal()
        {
            for (int i = 0; i < rectChildren.Count; i++)
                SetChildAlongAxis(rectChildren[i], 0, 340f - i * 105f + rectTransform.anchoredPosition.y * 105f / Step, 890f);
        }

        public override void SetLayoutVertical()
        {
            for (int i = 0; i < rectChildren.Count; i++) SetChildAlongAxis(rectChildren[i], 1, 40f + i * Step, 104f);
        }

        private void LateUpdate()
        {
            if (Mathf.Approximately(_lastScrollY, rectTransform.anchoredPosition.y)) return;
            _lastScrollY = rectTransform.anchoredPosition.y;
            SetLayoutHorizontal();
        }
    }
}
