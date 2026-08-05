namespace Game.NonCombat.Progress
{
    public static class CharacterIdentity
    {
        public static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        public static bool IsValid(string value) => Normalize(value) != null;
    }
}
