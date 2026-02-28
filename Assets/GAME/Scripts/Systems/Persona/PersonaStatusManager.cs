using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Systems.Persona
{
    /// <summary>
    /// 플레이어의 일상 스테이터스(용기, 거짓말, 설득, 직감, 공감)를 관리하는 매니저
    /// 씬이 전환되어도 파괴되지 않으며 전역에서 접근 가능하도록 싱글톤으로 구성합니다.
    /// </summary>
    public sealed class PersonaStatusManager : MonoBehaviour
    {
        public static PersonaStatusManager Instance { get; private set; }

        // 스탯 변동 시 UI나 다른 시스템에 알리기 위한 이벤트
        public event Action<PersonaStat, int, int> OnStatLevelUp; // stat, oldLevel, newLevel
        public event Action<PersonaStat, int> OnStatXpGained;     // stat, currentXp

        [Header("Settings")]
        [SerializeField] private int maxLevel = 5; // 최고 레벨
        [SerializeField] private int baseXpRequired = 10; // 레벨업에 필요한 기본 경험치 배율

        // 데이터 저장소 (MVP: 런타임 딕셔너리로 관리, 추후 Save/Load 시스템과 연동)
        private Dictionary<PersonaStat, int> _statLevels = new();
        private Dictionary<PersonaStat, int> _statXp = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitStats();
        }

        private void InitStats()
        {
            // 모든 스탯을 1레벨, 0 경험치로 초기화
            foreach (PersonaStat stat in Enum.GetValues(typeof(PersonaStat)))
            {
                _statLevels[stat] = 1;
                _statXp[stat] = 0;
            }
        }

        /// <summary>
        /// 특정 스탯의 현재 레벨을 반환합니다. 대화 선택지 조건 검사 시 사용합니다.
        /// </summary>
        public int GetLevel(PersonaStat stat) => _statLevels[stat];

        /// <summary>
        /// 특정 스탯의 현재 경험치를 반환합니다.
        /// </summary>
        public int GetXp(PersonaStat stat) => _statXp[stat];

        /// <summary>
        /// 다음 레벨업에 필요한 경험치를 계산합니다. (예: 1->2는 10, 2->3은 20)
        /// </summary>
        public int GetRequiredXpForNextLevel(int currentLevel)
        {
            if (currentLevel >= maxLevel) return -1; // 만렙
            return currentLevel * baseXpRequired;
        }

        /// <summary>
        /// 일상 행동(신문 읽기, 대화 선택 등)을 통해 스탯 경험치를 획득합니다.
        /// </summary>
        public void AddXp(PersonaStat stat, int amount)
        {
            int currentLevel = _statLevels[stat];
            if (currentLevel >= maxLevel) return; // 이미 만렙이면 무시

            _statXp[stat] += amount;
            OnStatXpGained?.Invoke(stat, _statXp[stat]);

            Debug.Log($"[Persona] {stat} XP 획득: +{amount} (현재 {_statXp[stat]})");

            CheckLevelUp(stat);
        }

        private void CheckLevelUp(PersonaStat stat)
        {
            int currentLevel = _statLevels[stat];
            int requiredXp = GetRequiredXpForNextLevel(currentLevel);

            // 경험치가 요구치를 넘어섰다면 레벨업 처리 (다중 레벨업 가능성 고려)
            while (requiredXp != -1 && _statXp[stat] >= requiredXp)
            {
                _statXp[stat] -= requiredXp;
                _statLevels[stat]++;

                int newLevel = _statLevels[stat];
                Debug.Log($"<color=#00FF00>[Persona Level Up!] {stat} 스탯이 레벨 {newLevel}이(가) 되었습니다!</color>");

                OnStatLevelUp?.Invoke(stat, currentLevel, newLevel);

                currentLevel = newLevel;
                requiredXp = GetRequiredXpForNextLevel(currentLevel);
            }
        }

        // --- 디버그용 치트 ---
        [ContextMenu("Debug: Add 10 Courage XP")]
        private void DebugAddCourage() => AddXp(PersonaStat.Courage, 10);
    }
}