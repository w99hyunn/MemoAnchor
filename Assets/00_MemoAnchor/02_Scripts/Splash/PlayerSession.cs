namespace MemoAnchor
{
    public static class PlayerSession
    {
        public static PlayerProfile Profile { get; private set; }

        public static void SetProfile(PlayerProfile profile)
        {
            Profile = profile;
        }

        public static void Clear()
        {
            Profile = default;
        }
    }
}
