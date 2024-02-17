namespace BlinkCommon.Interfaces.System;

public interface IHomeScreenAccount
{
    long Id { get; }
    bool Email_Verified { get; }
    bool Email_Verification_Required { get; }
    bool Amazon_Account_Linked { get; }
}