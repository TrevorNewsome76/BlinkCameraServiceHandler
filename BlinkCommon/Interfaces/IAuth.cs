namespace BlinkCommon.Interfaces;

public interface IAuth
{
    string Token { get; }
    string UserName { get; }
    string Password { get; }
}