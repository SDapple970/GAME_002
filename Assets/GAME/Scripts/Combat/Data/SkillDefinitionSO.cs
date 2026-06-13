using UnityEngine;
using UnityEngine.Serialization;
using Game.Combat.Model;

namespace Game.Combat.Data
{
    [CreateAssetMenu(menuName = "Game/Combat/Skill Definition")]
    public sealed class SkillDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int skillId;
        public string displayName;

        [Header("Costs / Tags")]
        public int inspirationCost;
        public SkillTag tag = SkillTag.Attack;
        public TargetingRule targeting = TargetingRule.SingleEnemy;
        public bool consumesTurn = true;

        [Header("Keywords")]
        public KeywordMask keywords = KeywordMask.None;

        [Header("Movement Presentation")]
        public SkillMovementMode movementMode = SkillMovementMode.None;
        public float desiredTargetDistance = 1.0f;
        public float moveSpeed = 6.0f;
        public float actionDelayAfterMove = 0.15f;

        public SkillMovementMode MovementMode => movementMode;
        public float DesiredTargetDistance => desiredTargetDistance;
        public float MoveSpeed => moveSpeed;
        public float ActionDelayAfterMove => actionDelayAfterMove;

        [Header("Animation")]
        [SerializeField] private string combatAnimationTrigger = "Attack";
        [SerializeField] private string fieldAnimationTrigger = "Attack";

        [Header("Field Use")]
        [SerializeField] private bool fieldUsable;
        [SerializeField] private float fieldCooldown = 0.35f;
        [FormerlySerializedAs("fieldHitTiming")]
        [SerializeField] private float fieldHitDelay = 0.12f;
        [SerializeField] private Vector2 fieldHitBoxSize = new Vector2(1.2f, 0.8f);
        [SerializeField] private Vector2 fieldHitBoxOffset = Vector2.zero;

        [Header("Presentation Assets")]
        [SerializeField] private GameObject castVfxPrefab;
        [SerializeField] private GameObject impactVfxPrefab;
        [SerializeField] private AudioClip castSfx;
        [SerializeField] private AudioClip impactSfx;

        public string CombatAnimationTrigger => combatAnimationTrigger;
        public string FieldAnimationTrigger => fieldAnimationTrigger;
        public bool FieldUsable => fieldUsable;
        public float FieldCooldown => fieldCooldown;
        public float FieldHitDelay => fieldHitDelay;
        public float FieldHitTiming => fieldHitDelay;
        public Vector2 FieldHitBoxSize => fieldHitBoxSize;
        public Vector2 FieldHitBoxOffset => fieldHitBoxOffset;
        public GameObject CastVfxPrefab => castVfxPrefab;
        public GameObject ImpactVfxPrefab => impactVfxPrefab;
        public AudioClip CastSfx => castSfx;
        public AudioClip ImpactSfx => impactSfx;

        [Header("MVP Numbers")]
        public int baseDamage = 1;
        public int baseStagger = 1;
        public int weaknessStaggerBonus = 3;
        public int speed = 5;

        private void OnValidate()
        {
            fieldCooldown = Mathf.Max(0f, fieldCooldown);
            fieldHitDelay = Mathf.Max(0f, fieldHitDelay);
            fieldHitBoxSize.x = Mathf.Max(0f, fieldHitBoxSize.x);
            fieldHitBoxSize.y = Mathf.Max(0f, fieldHitBoxSize.y);
        }
    }
}
