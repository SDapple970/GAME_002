using System.Collections.Generic;
using Game.Combat.Model;

namespace Game.Combat.Data
{
    public abstract class PlaybookEvent
    {
        public string LogMessage;
    }

    public sealed class Event_Unopposed : PlaybookEvent
    {
        public ICombatant Actor;
        public ICombatant Target;
        public ISkill Skill;
        public bool IsCancelled;
        public bool LackOfInspiration;
        public int DamageDealt;
        public int StaggerDealt;
        public bool HitWeakness;
    }

    public sealed class Event_Clash : PlaybookEvent
    {
        public ICombatant ActorA;
        public ISkill SkillA;
        public int PowerA;
        public ICombatant ActorB;
        public ISkill SkillB;
        public int PowerB;
        public ICombatant Winner;
        public ICombatant Loser;
        public int DamageDealtToLoser;
        public int StaggerDealtToLoser;
        public bool HitWeakness;
        public bool IsCancelled;
        public bool LackOfInspiration;
    }

    public sealed class Event_Utility : PlaybookEvent
    {
        public ICombatant Actor;
        public ISkill Skill;
        public bool IsCancelled;
    }

    public sealed class Event_Area : PlaybookEvent
    {
        public ICombatant Actor;
        public ISkill Skill;
        public readonly List<ICombatant> Targets = new();
        public readonly List<int> DamageDealt = new();
        public readonly List<int> StaggerDealt = new();
        public readonly List<bool> HitWeakness = new();
        public bool IsCancelled;
        public bool LackOfInspiration;
    }
}
