// GAME_002/Assets/GAME/Scripts/Combat/Model/CombatResult.cs
using System.Collections.Generic;
using System;

namespace Game.Combat.Model
{
    /// <summary>Immutable value result for an ally that had a persistent Character ID at combat entry.</summary>
    public sealed class CombatAllyParticipantResult
    {
        public string CharacterId { get; }
        public int CombatantId { get; }
        public int RemainingHp { get; }
        public bool IsAlive => RemainingHp > 0;

        internal CombatAllyParticipantResult(string characterId, int combatantId, int remainingHp)
        {
            CharacterId = characterId;
            CombatantId = combatantId;
            RemainingHp = remainingHp;
        }
    }

    /// <summary>
    /// 전투 종료 결과를 다른 시스템(UI, 보상, 필드 복귀)로 전달하는 데이터.
    /// </summary>
    public sealed class CombatResult
    {
        public string CompletionId { get; internal set; }
        public string SkillAcquisitionRecipientCharacterId { get; internal set; }
        public string ProgressionTargetCharacterId { get; internal set; }
        public bool IsPartySkillAcquisitionEligible { get; internal set; }
        public bool RequiresExplicitExperienceRecipients { get; internal set; }
        public IReadOnlyList<CombatDefeatedEnemyRecord> DefeatedEnemySources { get; internal set; } =
            Array.AsReadOnly(Array.Empty<CombatDefeatedEnemyRecord>());
        public IReadOnlyList<CombatAllyParticipantResult> AllyParticipants { get; internal set; } =
            Array.AsReadOnly(Array.Empty<CombatAllyParticipantResult>());
        public bool IsWin;
        public CombatEndReason EndReason;

        public int TotalExp;
        public int TotalGold;

        public bool EscapeSucceeded;

        public readonly List<int> DefeatedEnemyIds = new();
        public readonly List<int> SurvivedAllyIds = new();
        public readonly Dictionary<int, int> RemainingHpByCombatantId = new();
    }
}
