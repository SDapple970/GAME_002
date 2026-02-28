namespace Game.Combat.Model
{
    public enum Side
    {
        Allies,
        Enemies
    }

    public enum StartReason
    {
        PlayerFirstHit,
        PlayerGotHit,
        SpecialSkill
    }

    public enum SkillTag
    {
        Attack,
        Defense,
        Dodge,
        Utility,
        Inspect,   // 살펴보기(대상)
        ScanEnv    // 둘러보기(환경)
    }

    public enum TargetingRule
    {
        None,
        Self,
        SingleEnemy,
        SingleAlly,
        AnySingle,
        AllEnemies,
        AllAllies,
        Environment // 지물/지형
    }

    public enum Phase
    {
        EnterCombat,
        Planning,
        Resolution,
        EndTurn,
        ExitCombat
    }
}
