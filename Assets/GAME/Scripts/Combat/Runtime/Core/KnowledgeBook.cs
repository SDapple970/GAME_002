using System.Collections.Generic;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    /// <summary>
    /// 전투 중 "알고 있는 정보" 저장소 (MVP: 약점 공개 여부만)
    /// </summary>
    public sealed class KnowledgeBook
    {
        private readonly HashSet<CombatantId> _revealedWeakness = new();

        public void RevealWeakness(CombatantId targetId) => _revealedWeakness.Add(targetId);

        public bool IsWeaknessRevealed(CombatantId targetId) => _revealedWeakness.Contains(targetId);

        public void Clear()
        {
            _revealedWeakness.Clear();
        }
    }
}