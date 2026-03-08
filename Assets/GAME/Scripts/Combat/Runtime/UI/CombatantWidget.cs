// 위치: GAME/Scripts/Combat/UI/CombatantWidget.cs
using UnityEngine;
using UnityEngine.UI;
using Game.Combat.Model;
using Game.Combat.Adapters; // FieldCombatantAdapter 사용

namespace Game.Combat.UI
{
    public sealed class CombatantWidget : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Slider hpSlider;
        [SerializeField] private Slider staggerSlider;
        [SerializeField] private Text hpText;

        [Header("Settings")]
        [SerializeField] private Vector3 offset = new Vector3(0, 1.5f, 0); // 캐릭터 머리 위 오프셋

        private ICombatant _targetCombatant;
        private Transform _targetTransform;
        private Camera _mainCamera;

        public void Bind(ICombatant combatant)
        {
            _targetCombatant = combatant;
            _mainCamera = Camera.main;

            // 어댑터에서 실제 필드 게임 오브젝트의 Transform을 가져옴
            if (combatant is FieldCombatantAdapter adapter && adapter.FieldObject != null)
            {
                _targetTransform = adapter.FieldObject.transform;
            }

            RefreshUI();
        }

        private void LateUpdate()
        {
            if (_targetCombatant == null || _targetTransform == null) return;

            // 캐릭터가 죽었으면 UI 숨기기
            if (_targetCombatant.HP <= 0)
            {
                gameObject.SetActive(false);
                return;
            }

            // 캐릭터 머리 위를 따라다니도록 위치 갱신 (월드 캔버스인 경우)
            transform.position = _targetTransform.position + offset;

            // 매 프레임 UI 갱신 (MVP 단계이므로 단순 Update 처리)
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (hpSlider != null)
            {
                hpSlider.maxValue = _targetCombatant.MaxHP;
                hpSlider.value = _targetCombatant.HP;
            }

            if (staggerSlider != null)
            {
                staggerSlider.maxValue = _targetCombatant.StaggerMax;
                staggerSlider.value = _targetCombatant.Stagger;
            }

            if (hpText != null)
            {
                hpText.text = $"{_targetCombatant.HP} / {_targetCombatant.MaxHP}";
            }
        }
    }
}