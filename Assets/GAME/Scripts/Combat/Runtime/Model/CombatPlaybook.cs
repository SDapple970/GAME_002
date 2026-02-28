using System.Collections.Generic;
using Game.Combat.Model;

namespace Game.Combat.Data
{
    // 비주얼 담당자가 읽을 대본의 기본 형태
    public abstract class PlaybookEvent
    {
        public string LogMessage; // 디버깅용 로그
    }

    // 1. 일방 공격 (합이 발생하지 않음)
    public sealed class Event_Unopposed : PlaybookEvent
    {
        public ICombatant Actor;
        public ICombatant Target;
        public ISkill Skill;

        public bool IsCancelled; // 실행 전 기절/사망으로 취소되었는가?
        public bool LackOfInspiration; // 영감 부족으로 취소되었는가?

        public int DamageDealt;
        public int StaggerDealt;
        public bool HitWeakness;
    }

    // 2. 합 (Clash) 발생
    public sealed class Event_Clash : PlaybookEvent
    {
        public ICombatant ActorA;
        public ISkill SkillA;
        public int PowerA;

        public ICombatant ActorB;
        public ISkill SkillB;
        public int PowerB;

        public ICombatant Winner; // 무승부면 null
        public ICombatant Loser;

        public int DamageDealtToLoser;
        public int StaggerDealtToLoser;
        public bool HitWeakness;
    }

    // 3. 유틸리티/버프 스킬 (타겟이 없거나 환경 스킬 등)
    public sealed class Event_Utility : PlaybookEvent
    {
        public ICombatant Actor;
        public ISkill Skill;
        public bool IsCancelled;
    }
}