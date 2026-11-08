namespace BlinkCommon.Interfaces;

public interface ICommandAndArguments
{
    string Command { get; }
    string[] Arguments { get; }
    bool BareDownload { get; }
    string Message { get; set; }
}