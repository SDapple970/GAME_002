using System;

namespace Game.Combat.Model
{
    public readonly struct CombatClashSideInput
    {
        public CombatantId ActorId { get; }
        public SkillId SkillId { get; }
        public int ClashPower { get; }
        public int ClashModifier { get; }
        public int Speed { get; }
        public bool IsCurrentAttackOwner { get; }

        public CombatClashSideInput(
            CombatantId actorId,
            SkillId skillId,
            int clashPower,
            int speed,
            bool isCurrentAttackOwner)
            : this(actorId, skillId, clashPower, 0, speed, isCurrentAttackOwner)
        {
        }

        public CombatClashSideInput(
            CombatantId actorId,
            SkillId skillId,
            int clashPower,
            int clashModifier,
            int speed,
            bool isCurrentAttackOwner)
        {
            if (clashPower < 0)
                throw new ArgumentOutOfRangeException(nameof(clashPower));

            ActorId = actorId;
            SkillId = skillId;
            ClashPower = clashPower;
            ClashModifier = clashModifier;
            Speed = speed;
            IsCurrentAttackOwner = isCurrentAttackOwner;
        }
    }
}
