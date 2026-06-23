namespace MemoAnchor
{
    public readonly struct PlayerProfile
    {
        public readonly string Name;
        public readonly string Email;
        public readonly string CompanyName;

        public PlayerProfile(string name, string email, string companyName)
        {
            Name = name;
            Email = email;
            CompanyName = companyName;
        }
    }
}
