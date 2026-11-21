namespace Rovyn
{
    public static class Names
    {
        // "Name#1234" -> "Name". UGS appends the #number to keep player names unique.
        public static string Display(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "";
            int hash = fullName.LastIndexOf('#');
            return hash > 0 ? fullName.Substring(0, hash) : fullName;
        }

        // "Name#1234" -> "1234", or "" when there is no suffix.
        public static string Tag(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "";
            int hash = fullName.LastIndexOf('#');
            return hash > 0 ? fullName.Substring(hash + 1) : "";
        }

        // Fallback label when we only know a player ID.
        public static string FromId(string playerId) =>
            string.IsNullOrEmpty(playerId) ? "Unknown" : "User-" + playerId.Substring(0, System.Math.Min(6, playerId.Length));
    }
}
