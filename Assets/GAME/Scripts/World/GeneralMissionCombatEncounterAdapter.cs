using Game.Combat.Integration;
using Game.NonCombat.Save;
using Game.Quest;
using UnityEngine;

namespace Game.World
{
    [RequireComponent(typeof(CombatEncounterGroup))]
    public sealed class GeneralMissionCombatEncounterAdapter : GeneralMissionBinding, ISaveDataConsumer
    {
        private CombatEncounterGroup _group;
        private string _runId;
        private int _attempt;
        private bool _restorePending;
        private bool _bound;

        protected override void OnEnable()
        {
            _group = GetComponent<CombatEncounterGroup>();
            _bound = _group.TryBindMissionOwner(this);
            if (_bound) { _group.OnEncounterCleared -= Cleared; _group.OnEncounterCleared += Cleared; }
            base.OnEnable();
        }
        protected override void OnDisable()
        {
            if (_group != null && _bound) _group.OnEncounterCleared -= Cleared;
            // Retain the scope while disabled: a global world save must never absorb a run clear.
            _bound = false;
            base.OnDisable();
        }
        protected override void ApplyState()
        {
            if (!_bound) return;
            if (Snapshot == null || Snapshot.Kind != GeneralMissionKind.Extermination ||
                !GeneralMissionSuppressor.HasId(Snapshot.SelectedTargetIds, _group.EncounterId))
            {
                _restorePending = !_group.TryApplyMissionSnapshot(this, true);
                _runId = null; _attempt = 0;
                return;
            }
            if (_runId == Snapshot.RunId && _attempt == Snapshot.Attempt && !_restorePending) return;
            bool cleared = GeneralMissionSuppressor.HasId(Snapshot.CompletedTargetIds, _group.EncounterId);
            _restorePending = !_group.TryApplyMissionSnapshot(this, cleared);
            if (!_restorePending) { _runId = Snapshot.RunId; _attempt = Snapshot.Attempt; }
        }
        private void LateUpdate() { if (_restorePending) ApplyState(); }
        public void RestoreSaveData(GameSaveData data) { _restorePending = true; Refresh(); }
        private void Cleared(CombatEncounterGroup group, string completionId)
        {
            if (_bound && group == _group && !string.IsNullOrWhiteSpace(completionId) && _runId != null)
                questRuntime.TryClearMissionEncounter(questId, _runId, _attempt, group.EncounterId);
        }
    }
}
