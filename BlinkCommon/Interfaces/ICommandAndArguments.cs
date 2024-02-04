namespace BlinkCommon.Interfaces;

public interface ICommandAndArguments
{
    string Command { get; }
    string[] Arguments { get; }
    string Message { get; set; }
}