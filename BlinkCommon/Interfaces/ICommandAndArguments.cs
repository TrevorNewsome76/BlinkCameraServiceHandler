namespace BlinkCommon.Interfaces;

public interface ICommandAndArguments
{
    string Command { get; }
    string[] Arguments { get; }
    string ErrorMessage { get; set; }
}