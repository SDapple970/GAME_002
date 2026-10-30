using Game.Combat.Model;
using UnityEngine;

namespace Game.Combat.Data
{
    /// <summary>
    /// Authored, immutable-at-runtime configuration for explicitly opted-in FinalExchange combat.
    /// Session state such as current MP, posture, and pressure remains in CombatSession.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FinalCombatRuntimeConfig_V1",
        menuName = "GAME/Combat/Final Combat Runtime Config")]
    public sealed class FinalCombatRuntimeConfigSO : ScriptableObject
    {
        [Header("MP")]
        [SerializeField, Min(0)] private int maxMp = 3;
        [SerializeField, Min(0)] private int initialMp = 2;
        [SerializeField, Min(0f)] private float mpRecoveryPerSecond = 1f;

        [Header("Posture")]
        [SerializeField, Min(0)] private int maxPosture = 3;
        [SerializeField, Min(0)] private int initialPosture;

        [Header("Standoff Pressure")]
        [SerializeField, Min(0f)] private float pressureMax = 1f;
        [SerializeField, Min(0f)] private float pressurePerSecond = 0.25f;

        [Header("Mental V1")]
        [SerializeField, Min(0)] private int maxMental = 100;
        [SerializeField, Min(0)] private int initialMental = 100;
        [SerializeField, Min(0)] private int clashMentalLoss = 10;
        [SerializeField, Min(0)] private int damageMentalLoss = 5;
        [SerializeField, Min(0)] private int stunMentalLoss = 20;
        [SerializeField, Min(0)] private int panicThreshold;
        [SerializeField, Min(0)] private int overcomeRecoveryMental = 35;

        [Header("Production Hand / BP (explicit opt-in; authored values required)")]
        [SerializeField] private bool enableProductionHandBp;
        [SerializeField, Min(0)] private int normalHandCapacity;
        [SerializeField, Min(0)] private int clashHandCapacity;
        [SerializeField] private int handSeed;
        [SerializeField] private HandCardRule[] handCardRules = System.Array.Empty<HandCardRule>();
        [SerializeField, Min(0)] private int bpMaximum;
        [SerializeField, Min(0)] private int bpRoundCyclesPerGain;
        [SerializeField, Min(0)] private int bpMaximumInvestment;
        [SerializeField] private SkillDefinitionSO[] ultimateSkills = System.Array.Empty<SkillDefinitionSO>();
        [Tooltip("Only enemies with one of these authored source keys use a hand. Empty means no enemy hands.")]
        [SerializeField] private string[] enemyHandSourceKeys = System.Array.Empty<string>();

        [Header("Production Clash (required when Production Hand / BP is enabled)")]
        [SerializeField, Min(0)] private int clashAttackerDurability;
        [SerializeField, Min(0)] private int clashResponderDurability;
        [SerializeField, Min(0)] private int clashMaxComparisons;
        [SerializeField, Min(0f)] private float clashDecisionDurationSeconds;
        [SerializeField] private ClashFinalSkillPolicy clashFinalSkillPolicy;
        [SerializeField] private ClashTimeoutAttackPolicy clashTimeoutAttackPolicy;

        public enum ClashFinalSkillPolicy { Unspecified = 0, LastCommittedWinningSkill = 1 }
        public enum ClashTimeoutAttackPolicy { Unspecified = 0, FirstAvailableSkill = 1 }

        [System.Serializable]
        public sealed class HandCardRule
        {
            [SerializeField] private SkillDefinitionSO skill;
            [SerializeField] private CombatCardKind kind;
            [SerializeField, Min(0)] private int copies;
            public SkillDefinitionSO Skill => skill;
            public CombatCardKind Kind => kind;
            public int Copies => copies;
        }

        public bool EnableProductionHandBp => enableProductionHandBp;
        public System.Collections.Generic.IReadOnlyList<string> EnemyHandSourceKeys => enemyHandSourceKeys ?? System.Array.Empty<string>();
        public int ClashAttackerDurability => clashAttackerDurability;
        public int ClashResponderDurability => clashResponderDurability;
        public int ClashMaxComparisons => clashMaxComparisons;
        public float ClashDecisionDurationSeconds => clashDecisionDurationSeconds;
        public ClashFinalSkillPolicy FinalSkillPolicy => clashFinalSkillPolicy;
        public ClashTimeoutAttackPolicy TimeoutAttackPolicy => clashTimeoutAttackPolicy;
        public int NormalHandCapacity => normalHandCapacity;
        public int ClashHandCapacity => clashHandCapacity;
        public int HandSeed => handSeed;
        public System.Collections.Generic.IReadOnlyList<HandCardRule> HandCardRules => handCardRules ?? System.Array.Empty<HandCardRule>();
        public int BpMaximum => bpMaximum;
        public int BpRoundCyclesPerGain => bpRoundCyclesPerGain;
        public int BpMaximumInvestment => bpMaximumInvestment;
        public System.Collections.Generic.IReadOnlyList<SkillDefinitionSO> UltimateSkills => ultimateSkills ?? System.Array.Empty<SkillDefinitionSO>();

        public int MaxMp => maxMp;
        public int InitialMp => initialMp;
        public float MpRecoveryPerSecond => mpRecoveryPerSecond;
        public int MaxPosture => maxPosture;
        public int InitialPosture => initialPosture;
        public float PressureMax => pressureMax;
        public float PressurePerSecond => pressurePerSecond;
        public int MaxMental => maxMental;
        public int InitialMental => initialMental;
        public int ClashMentalLoss => clashMentalLoss;
        public int DamageMentalLoss => damageMentalLoss;
        public int StunMentalLoss => stunMentalLoss;
        public int PanicThreshold => panicThreshold;
        public int OvercomeRecoveryMental => overcomeRecoveryMental;

        public CombatRuntimeConfig CreateRuntimeConfig()
        {
            return new CombatRuntimeConfig(
                maxMp,
                initialMp,
                maxPosture,
                initialPosture,
                mpRecoveryPerSecond,
                pressureMax,
                pressurePerSecond,
                maxMental,
                initialMental,
                clashMentalLoss,
                damageMentalLoss,
                stunMentalLoss,
                panicThreshold,
                overcomeRecoveryMental);
        }

        public bool SupportsFinalExchangeSkills()
        {
            CombatRuntimeConfig config = CreateRuntimeConfig();
            return config.MaxMp >= 2 && config.InitialMp >= 2 &&
                   config.PressureMax > 0f && config.PressurePerSecond > 0f;
        }
    }
}
