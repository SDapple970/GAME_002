// Scripts/Combat/Model/CombatResult.cs
namespace Game.Combat.Model
{
    /// <summary>
    /// 전투가 끝났을 때 보상 시스템이나 다른 시스템으로 전달할 결과 데이터입니다.
    /// </summary>
    public sealed class CombatResult
    {
        public bool IsWin;       // 승리 여부 (패배 시 보상 창 대신 게임 오버 창을 띄우기 위해 필요)
        public int TotalExp;     // 획득한 총 경험치
        public int TotalGold;    // 획득한 총 재화(골드)

        // 추후 전리품(Item), 해금된 스킬(SkillDefinitionSO) 목록 등을 리스트로 추가할 수 있습니다.
        // public List<ItemData> DroppedItems; 
    }
}