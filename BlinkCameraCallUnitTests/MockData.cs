using BlinkCommon.Interfaces;
using Shadow.Quack;

namespace BlinkCameraCallUnitTests;

public static class MockData
{
    public static ILoginResponse AuthLoginResponse() =>
        Duck.Implement<ILoginResponse>(new
        {
            Account = Account(),
            Auth = Auth(),
            Phone = Phone(),
            Verification = Verification(),
            Lockout_time_Remaining = 0,
            Force_Password_Reset = false,
            Allow_Pin_Resend_Seconds = 90
        });

    public static IAccount Account() =>
        Duck.Implement<IAccount>(new
        {
            Account_Id = 134166,
            User_Id = 234313,
            Client_Id = 1486061,
            Client_Trusted = false,
            New_Account = false,
            Tier = "e007",
            Region = "eu",
            Account_Verification_Required = false,
            Phone_Verification_Required = false,
            Client_Verification_Required = true,
            Require_Trust_Client_Device = true,
            Country_Required = false,
            Verification_Channel = "phone",
            User = User(),
            Amazon_Account_Linked = true,
            Braze_External_Id = "6425018aca0a95c037c59a7b02a5ef006a094fc6150797440012235f44dedade"
        });

    public static IUser User() =>
        Duck.Implement<IUser>(new
        {
            User_Id = 238452,
            Country = "GB"
        });

    public static IAuth Auth() =>
        Duck.Implement<IAuth>(new
        {
            Token = "BU8fOjaF4E5POf4WTRm5wA"
        });

    public static IPhone Phone() =>
        Duck.Implement<IPhone>(new
        {
            Number = "+44*******4612",
            Last_4_Digits = "4612",
            Country_Calling_Code = "44",
            Valid = true
        });

    public static IVerification Verification() =>
        Duck.Implement<IVerification>(new
        {
            Email = VerifyEmail(),
            Phone = VerifyPhone()
        });

    public static IVerifyEmail VerifyEmail() =>
        Duck.Implement<IVerifyEmail>(new
        {
            Required = false
        });

    public static IVerifyPhone VerifyPhone() =>
        Duck.Implement<IVerifyPhone>(new
        {
            Required = false,
            Channel = "sms"
        });

}
