// 위치: GAME/Scripts/Combat/UI/CombatWidgetManager.cs
using System.Collections.Generic;
using UnityEngine;
using Game.Combat.Core;
using Game.Combat.Model;

namespace Game.Combat.UI
{
    public sealed class CombatWidgetManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatEntryPoint entryPoint;
        [SerializeField] private RectTransform widgetContainer; // 캔버스 내 빈 오브젝트
        [SerializeField] private CombatantWidget widgetPrefab;  // 방금 만든 위젯 프리팹

        private readonly List<CombatantWidget> _spawnedWidgets = new();

        private void OnEnable()
        {
            if (entryPoint != null)
            {
                entryPoint.OnCombatStarted += HandleCombatStarted;
                entryPoint.OnCombatEnded += HandleCombatEnded;
            }
        }

        private void OnDisable()
        {
            if (entryPoint != null)
            {
                entryPoint.OnCombatStarted -= HandleCombatStarted;
                entryPoint.OnCombatEnded -= HandleCombatEnded;
            }
        }

        private void HandleCombatStarted(CombatSession session)
        {
            ClearWidgets();

            // 아군 위젯 생성
            foreach (var ally in session.Allies)
            {
                CreateWidget(ally);
            }

            // 적군 위젯 생성
            foreach (var enemy in session.Enemies)
            {
                CreateWidget(enemy);
            }
        }

        private void HandleCombatEnded(CombatResult result)
        {
            ClearWidgets();
        }

        private void CreateWidget(ICombatant combatant)
        {
            if (widgetPrefab == null || widgetContainer == null) return;

            var widgetInstance = Instantiate(widgetPrefab, widgetContainer);
            widgetInstance.Bind(combatant);
            _spawnedWidgets.Add(widgetInstance);
        }

        private void ClearWidgets()
        {
            foreach (var w in _spawnedWidgets)
            {
                if (w != null) Destroy(w.gameObject);
            }
            _spawnedWidgets.Clear();
        }
    }
}