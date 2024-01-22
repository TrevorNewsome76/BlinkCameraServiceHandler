namespace BlinkCommon.Interfaces;

public interface IAccount
{
    long Account_Id { get; }
    long User_Id { get; }
    long Client_Id { get; }
    bool Client_Trusted { get; }
    bool New_Account { get; }
    string Tier { get; }
    string Region { get; }
    bool Account_Verification_Required { get; }
    bool Phone_Verification_Required { get; }
    bool Client_Verification_Required { get; }
    bool Require_Trust_Client_Device { get; }
    bool Country_Required { get; }
    string Verification_Channel { get; }
    bool Amazon_Account_Linked { get; }
    string Braze_External_id { get; }
}