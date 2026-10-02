namespace Game.Combat.Model
{
    /// <summary>
    /// Authored values consumed only by the FinalExchange flow.
    /// Legacy planning continues to use ISkill.InspirationCost.
    /// </summary>
    public interface IFinalCombatSkillStats
    {
        int FinalMpCost { get; }
        int ClashPower { get; }
    }
}
