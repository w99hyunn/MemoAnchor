namespace MemoAnchor
{
    public readonly struct SplashAuthCompletion
    {
        public readonly bool IsExistingMember;
        public readonly PlayerProfile Profile;

        public SplashAuthCompletion(bool isExistingMember, PlayerProfile profile)
        {
            IsExistingMember = isExistingMember;
            Profile = profile;
        }
    }
}
