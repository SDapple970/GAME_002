namespace Game.Interaction
{
    public static class InteractionIdentity
    {
        public static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        public static string ResolveActionId(InteractionEventSO interactionEvent, int authoredIndex)
        {
            string authored = interactionEvent != null ? interactionEvent.ActionId : null;
            return authored ?? $"event:{authoredIndex}";
        }
    }
}
