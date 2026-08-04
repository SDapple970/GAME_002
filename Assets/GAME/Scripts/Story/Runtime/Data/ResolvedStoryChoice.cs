namespace Game.Story.Data
{
    public readonly struct ResolvedStoryChoice
    {
        public StoryChoice Choice { get; }
        public int AuthoredIndex { get; }
        public int VisibleIndex { get; }
        public string ChoiceId { get; }
        public bool IsEnabled { get; }
        public string DisabledReason { get; }

        public ResolvedStoryChoice(
            StoryChoice choice,
            int authoredIndex,
            int visibleIndex,
            bool isEnabled)
            : this(choice, authoredIndex, visibleIndex, isEnabled, choice?.ChoiceId)
        {
        }

        public ResolvedStoryChoice(
            StoryChoice choice,
            int authoredIndex,
            int visibleIndex,
            bool isEnabled,
            string effectiveChoiceId)
        {
            Choice = choice;
            AuthoredIndex = authoredIndex;
            VisibleIndex = visibleIndex;
            ChoiceId = string.IsNullOrWhiteSpace(effectiveChoiceId)
                ? null
                : effectiveChoiceId.Trim();
            IsEnabled = isEnabled;
            DisabledReason = isEnabled ? null : choice?.DisabledReason;
        }
    }
}
