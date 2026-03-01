using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Game.Combat.Core;
using Game.Combat.Model;

namespace Game.Combat.UI
{
    public sealed class CombatLogHUD : MonoBehaviour
    {
        [SerializeField] private CombatEntryPoint entryPoint;
        [SerializeField] private Text logText; // Legacy Text
        [SerializeField] private int maxLines = 12;

        private CombatSession _session;
        private int _lastEventCount = -1;
        private int _lastTurnIndex = -1;

        private void OnEnable()
        {
            if (entryPoint != null)
            {
                entryPoint.OnCombatStarted += OnStarted;
                entryPoint.OnCombatEnded += OnEnded; // 추가
            }
        }

        private void OnDisable()
        {
            if (entryPoint != null)
            {
                entryPoint.OnCombatStarted -= OnStarted;
                entryPoint.OnCombatEnded -= OnEnded; // 추가
            }
        }

        private void OnEnded(CombatResult result) // <- 매개변수 추가!
        {
            _session = null;
            if (logText != null) logText.text = "";
        }

        private void OnStarted(CombatSession session)
        {
            _session = session;
            _lastEventCount = -1;
            _lastTurnIndex = -1;
            Refresh();
        }

        private void Update()
        {
            if (_session == null || entryPoint == null || entryPoint.ActiveStateMachine == null) return;

            if (_session.TurnIndex != _lastTurnIndex)
            {
                _lastTurnIndex = _session.TurnIndex;
                _lastEventCount = -1;
                Refresh();
                return;
            }

            int count = _session.CurrentTurn?.Events?.Count ?? 0;
            if (count != _lastEventCount)
            {
                _lastEventCount = count;
                Refresh();
            }
        }

        private void Refresh()
        {
            if (logText == null || _session?.CurrentTurn?.Events == null) return;

            var sb = new StringBuilder();
            sb.AppendLine($"Turn {_session.TurnIndex} / Phase {entryPoint.ActiveStateMachine.Phase}");

            int start = Mathf.Max(0, _session.CurrentTurn.Events.Count - maxLines);
            for (int i = start; i < _session.CurrentTurn.Events.Count; i++)
                sb.AppendLine(_session.CurrentTurn.Events[i].ToString());

            logText.text = sb.ToString();
        }
    }
}