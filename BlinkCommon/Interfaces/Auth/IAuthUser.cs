namespace BlinkCommon.Interfaces.Auth
{
    public interface IAuthUser
    {
        long User_Id { get; }
        string Country { get; }
    }
}