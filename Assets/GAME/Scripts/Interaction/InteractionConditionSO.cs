using UnityEngine;

namespace Game.Interaction
{
    public abstract class InteractionConditionSO : ScriptableObject
    {
        public abstract bool IsMet(InteractionContext context, out string blockedReason);
    }
}
