using Game.Combat.Actions;
using Game.Combat.Core;
using Game.Combat.Model;

namespace Game.Combat.Adapters
{
    /// <summary>
    /// 필드 연동 전 테스트용. 나중에 FieldPlayerAdapter/EnemyAdapter로 교체.
    /// </summary>
    public sealed class DummyCombatantFactory : ICombatantFactory
    {
        private readonly SkillBook _book;
        public DummyCombatantFactory(SkillBook book) => _book = book;

        public void PopulateCombatants(CombatSession session, CombatStartRequest req)
        {
            // 최소 1 아군 + 1 적을 생성해도 되지만,
            // 이미 CombatTestRunner가 별도로 있으니 여기선 “요청 수만큼” 생성
            int id = 1;
            foreach (var _ in req.AllyFieldObjects)
            {
                var a = new DummyCombatant(id++, Side.Allies, hp: 10, weakness: KeywordMask.None, staggerMax: 6);
                // 스킬북에 등록된 스킬을 모두 추가(간단)
                // (전투에서 실제 선택 가능한 스킬은 추후 캐릭터별로 구성)
                session.Allies.Add(a);
            }

            int eid = 100;
            foreach (var _ in req.EnemyFieldObjects)
            {
                var e = new DummyCombatant(eid++, Side.Enemies, hp: 12, weakness: KeywordMask.Fire, staggerMax: 8);
                session.Enemies.Add(e);
            }
        }
    }
}
