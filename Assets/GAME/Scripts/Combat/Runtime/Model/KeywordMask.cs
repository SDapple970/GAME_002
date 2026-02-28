using System;

namespace Game.Combat.Model
{
    [Flags]
    public enum KeywordMask
    {
        None = 0,
        Ice = 1 << 0, // 냉각(얼음)
        Fire = 1 << 1, // 과열(불)
        Dark = 1 << 2, // 노이즈(어둠)
        Elec = 1 << 3, // 전압(전기)
        Wind = 1 << 4, // 압력(바람)
        Earth = 1 << 5, // 누설(땅)
        Water = 1 << 6, // 하수(물)
    }
}
