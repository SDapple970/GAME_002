using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// MP3 화면의 스크롤 리스트에 들어갈 개별 보상 항목(한 줄)입니다.
    /// </summary>
    public sealed class RewardItemUI : MonoBehaviour
    {
        [Tooltip("초록색으로 표시될 보상 이름 텍스트")]
        [SerializeField] private Text rewardText;

        [Tooltip("이 항목을 클릭(선택)할 수 있게 해주는 버튼 컴포넌트")]
        [SerializeField] private Button button;

        // 선택되었을 때 부모(RewardUIPanel)에게 알려주기 위한 콜백 이벤트
        private Action<string> _onSelectedCallback;
        private string _rewardData; // 실제 지급할 보상 데이터 (지금은 string으로 대체)

        /// <summary>
        /// 리스트를 생성할 때 데이터를 채워넣는 초기화 메서드
        /// </summary>
        public void Setup(string rewardName, Action<string> onSelected)
        {
            _rewardData = rewardName;
            _onSelectedCallback = onSelected;

            if (rewardText != null)
            {
                rewardText.text = rewardName;
                // MP3 감성에 맞게 텍스트 색상을 초록색으로 강제 세팅해도 좋습니다.
                rewardText.color = Color.green;
            }

            // 기존 리스너 초기화 후 새로 연결 (오류 방지)
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClickItem);
        }

        private void OnClickItem()
        {
            // 버튼이 눌리면 콜백 실행
            _onSelectedCallback?.Invoke(_rewardData);
        }
    }
}