using Game.Quest;
using UnityEngine;

namespace Game.World
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class GeneralMissionAreaTrigger : GeneralMissionBinding
    {
        public enum AreaRole { SurveyCell, RescueExit }
        [SerializeField] private AreaRole role;
        [SerializeField] private string areaId;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private GeneralMissionEscortActor escort;
        [Min(0.1f)] [SerializeField] private float escortExitDistance = 2f;
        protected override void OnTriggerEnter2D(Collider2D other) => Visit(other);
        private void OnTriggerStay2D(Collider2D other) => Visit(other);
        private void Visit(Collider2D other)
        {
            if (!AllowsFieldAction || !HasActiveRun || other == null ||
                !(other.CompareTag(playerTag) || (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(playerTag)))) return;
            if (role == AreaRole.SurveyCell)
                questRuntime.TryExploreMissionCell(questId, Snapshot.RunId, Snapshot.Attempt, areaId);
            else if (escort != null)
                escort.TryEscape(areaId, transform.position, escortExitDistance);
        }
    }
}
