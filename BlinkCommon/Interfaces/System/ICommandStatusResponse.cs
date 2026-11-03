namespace BlinkCommon.Interfaces.System;

public interface ICommandStatusResponse
{
    long Id { get; }
    long Network_Id { get; }
    bool Complete { get; }
    int Status_Code { get; }
}
