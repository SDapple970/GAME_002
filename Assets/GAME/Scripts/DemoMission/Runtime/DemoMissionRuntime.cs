using System;
using UnityEngine;
using Game.DemoMission.Data;

namespace Game.DemoMission.Runtime
{
    public sealed class DemoMissionRuntime : MonoBehaviour
    {
        public static DemoMissionRuntime Instance { get; private set; }

        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private DemoMissionDefinitionSO currentMission;

        public DemoMissionDefinitionSO CurrentMission => currentMission;
        public int EnemyDefeatCount { get; private set; }
        public bool IsNpcRescued { get; private set; }

        public event Action OnMissionProgressChanged;
        public event Action OnMissionCompleted;

        private bool _completionRaised;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }

        public static DemoMissionRuntime GetOrCreate()
        {
            if (Instance != null)
                return Instance;

            DemoMissionRuntime runtime = FindFirstObjectByType<DemoMissionRuntime>();
            if (runtime != null)
                return runtime;

            GameObject go = new GameObject("DemoMissionRuntime");
            return go.AddComponent<DemoMissionRuntime>();
        }

        public void SetCurrentMission(DemoMissionDefinitionSO mission)
        {
            currentMission = mission;
            ResetMissionProgress();
        }

        public void ResetMissionProgress()
        {
            EnemyDefeatCount = 0;
            IsNpcRescued = false;
            _completionRaised = false;
            RaiseProgressChanged();
        }

        public void RegisterEnemyDefeated()
        {
            if (currentMission == null)
            {
                Debug.LogWarning("[DemoMissionRuntime] Enemy defeat ignored. Current mission is null.", this);
                return;
            }

            EnemyDefeatCount++;
            RaiseProgressChanged();
            TryRaiseCompleted();
        }

        public void RegisterNpcRescued()
        {
            if (currentMission == null)
            {
                Debug.LogWarning("[DemoMissionRuntime] NPC rescue ignored. Current mission is null.", this);
                return;
            }

            if (IsNpcRescued)
                return;

            IsNpcRescued = true;
            RaiseProgressChanged();
            TryRaiseCompleted();
        }

        public bool HasRequiredEnemyKills()
        {
            if (currentMission == null)
                return false;

            return EnemyDefeatCount >= Mathf.Max(0, currentMission.requiredEnemyKills);
        }

        public bool IsMissionComplete()
        {
            return currentMission != null && HasRequiredEnemyKills() && IsNpcRescued;
        }

        private void TryRaiseCompleted()
        {
            if (_completionRaised || !IsMissionComplete())
                return;

            _completionRaised = true;
            OnMissionCompleted?.Invoke();
        }

        private void RaiseProgressChanged()
        {
            OnMissionProgressChanged?.Invoke();
        }
    }
}
