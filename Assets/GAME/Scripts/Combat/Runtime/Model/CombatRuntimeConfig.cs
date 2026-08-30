namespace Game.Combat.Model
{
    public readonly struct CombatRuntimeConfig
    {
        // Batch 10 compatibility default: neutral until authored combat data supplies real values.
        public static CombatRuntimeConfig Compatibility { get; } = new CombatRuntimeConfig(0, 0, 0, 0);

        public int MaxMp { get; }
        public int InitialMp { get; }
        public int MaxPosture { get; }
        public int InitialPosture { get; }
        public float MpRecoveryPerSecond { get; }
        public float PressureMax { get; }
        public float PressurePerSecond { get; }

        public CombatRuntimeConfig(int maxMp, int initialMp, int maxPosture, int initialPosture)
            : this(maxMp, initialMp, maxPosture, initialPosture, 0f, 0f, 0f)
        {
        }

        public CombatRuntimeConfig(
            int maxMp,
            int initialMp,
            int maxPosture,
            int initialPosture,
            float mpRecoveryPerSecond,
            float pressureMax,
            float pressurePerSecond)
        {
            MaxMp = maxMp < 0 ? 0 : maxMp;
            InitialMp = Clamp(initialMp, MaxMp);
            MaxPosture = maxPosture < 0 ? 0 : maxPosture;
            InitialPosture = Clamp(initialPosture, MaxPosture);
            MpRecoveryPerSecond = NormalizeNonNegative(mpRecoveryPerSecond);
            PressureMax = NormalizeNonNegative(pressureMax);
            PressurePerSecond = NormalizeNonNegative(pressurePerSecond);
        }

        private static float NormalizeNonNegative(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? 0f : value;
        }

        private static int Clamp(int value, int max)
        {
            if (value < 0)
                return 0;

            return value > max ? max : value;
        }
    }
}
