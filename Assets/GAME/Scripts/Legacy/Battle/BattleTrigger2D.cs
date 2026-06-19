// Scripts/Battle/BattleTrigger2D.cs
using System;
using UnityEngine;

namespace Game.Battle
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class BattleTrigger2D : MonoBehaviour
    {
        public static event Action<BattleTransitionRequest> OnBattleRequested;

        [SerializeField] private string battleSceneName = "Battle"; // 네 프로젝트 씬명으로 바꾸기
        [SerializeField] private bool oneShot = true;

        private bool _used;

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_used && oneShot) return;
            if (!other.CompareTag("Player")) return;

            _used = true;
            OnBattleRequested?.Invoke(new BattleTransitionRequest(transform.position, battleSceneName));
        }
    }
}
