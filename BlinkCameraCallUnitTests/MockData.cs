using BlinkCommon.Interfaces;
using Shadow.Quack;

namespace BlinkCameraCallUnitTests;

public static class MockData
{
    public static ILoginResponse AuthLoginCorrectResponse() =>
        Duck.Implement<ILoginResponse>(new
        {
            Account = ValidAccount(),
            Auth = ValidAuth(),
            Phone = ValidPhone(),
            Verification = ValidVerification(),
            Lockout_time_Remaining = 0,
            Force_Password_Reset = false,
            Allow_Pin_Resend_Seconds = 90
        });

    public static ILoginResponse AuthLoginFailedResponse() =>
        Duck.Implement<ILoginResponse>(new
        {
            Lockout_time_Remaining = 0,
            Force_Password_Reset = false,
            Allow_Pin_Resend_Seconds = 90,
            Message = "Invalid credentials",
            Code = 200,
        });

    public static ILogoutResponse AuthLogoutCorrectResponse() =>
        Duck.Implement<ILogoutResponse>(new
        {
            Message = "logout"
        });
    
    public static ILogoutResponse AuthLogoutIncorrectResponse() =>
        Duck.Implement<ILogoutResponse>(new
        {
            Message = "logout"
        });

    public static IAccount ValidAccount() =>
        Duck.Implement<IAccount>(new
        {
            Account_Id = 134166,
            User_Id = 234313,
            Client_Id = 1486061,
            Client_Trusted = false,
            New_Account = false,
            Tier = "007",
            Region = "eu",
            Account_Verification_Required = false,
            Phone_Verification_Required = false,
            Client_Verification_Required = true,
            Require_Trust_Client_Device = true,
            Country_Required = false,
            Verification_Channel = "phone",
            User = ValidUser(),
            Amazon_Account_Linked = true,
            Braze_External_Id = "6425018aca0a95c037c59a7b02a5ef006a094fc6150797440012235f44dedade"
        });

    public static IUser ValidUser() =>
        Duck.Implement<IUser>(new
        {
            User_Id = 238452,
            Country = "GB"
        });

    public static IAuth ValidAuth() =>
        Duck.Implement<IAuth>(new
        {
            Token = "BU8fOjaF4E5POf4WTRm5wA"
        });

    public static IPhone ValidPhone() =>
        Duck.Implement<IPhone>(new
        {
            Number = "+44*******4612",
            Last_4_Digits = "4612",
            Country_Calling_Code = "44",
            Valid = true
        });

    public static IVerification ValidVerification() =>
        Duck.Implement<IVerification>(new
        {
            Email = ValidVerifyEmail(),
            Phone = ValidVerifyPhone()
        });

    public static IVerifyEmail ValidVerifyEmail() =>
        Duck.Implement<IVerifyEmail>(new
        {
            Required = false
        });

    public static IVerifyPhone ValidVerifyPhone() =>
        Duck.Implement<IVerifyPhone>(new
        {
            Required = false,
            Channel = "sms"
        });

    public static IAuthPinResponse AuthCorrectPinResponse() =>
        Duck.Implement<IAuthPinResponse>(new
        {
            Message = "(1626) Client has been successfully verified"
        });

    public static IAuthPinResponse AuthIncorrectPinResponse() =>
        Duck.Implement<IAuthPinResponse>(new
        {
            Message = "(1621) Invalid PIN"
        });

    public static ISessionDetails CreateValidLoggedInSessionDetails() => 
        Duck.Implement<ISessionDetails>(new
        {
            Account = ValidAccount(),
            Auth = ValidAuth(),
            Phone = ValidPhone(),
            Verification = ValidVerification(),
            Lockout_Time_Remaining = 9,
            Force_Password_Reset = true,
            Allow_Pin_Resend_Seconds = 8,
            LoggedInStatus = true,
        });

    public static ISessionDetails CreateValidLoggedOutSessionDetails() =>
        Duck.Implement<ISessionDetails>(new
        {
            Account = ValidAccount(),
            Auth = ValidAuth(),
            Phone = ValidPhone(),
            Verification = ValidVerification(),
            Lockout_Time_Remaining = 9,
            Force_Password_Reset = true,
            Allow_Pin_Resend_Seconds = 8,
            LoggedInStatus = false,
        });

    public static ISessionDetails CreateEmptySessionDetails() =>
        Duck.Implement<ISessionDetails>(new
        {
            Account = Duck.Implement<IAccount>(new()),
            Auth = Duck.Implement<IAuth>(new()),
            Phone = Duck.Implement<IPhone>(new()),
            Verification = Duck.Implement<IVerification>(new()),
            Force_Password_Reset = false,
            Allow_Pin_Resend_Seconds = 0,
            LoggedInStatus = false,
        });
}
