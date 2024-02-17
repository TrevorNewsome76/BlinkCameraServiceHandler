namespace BlinkCommon.Interfaces.Auth;

public interface IAuth
{
    string Token { get; }
    string UserName { get; }
    string Password { get; }
}