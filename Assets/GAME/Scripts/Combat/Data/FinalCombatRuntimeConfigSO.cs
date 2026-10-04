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
