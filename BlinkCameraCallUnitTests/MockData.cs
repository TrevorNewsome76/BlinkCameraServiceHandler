using BlinkCommon.Interfaces;
using BlinkCommon.Interfaces.Auth;
using BlinkCommon.Interfaces.System;
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
            Allow_Pin_Resend_Seconds = 0,
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

    public static IAuthAccount ValidAccount() =>
        Duck.Implement<IAuthAccount>(new
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

    public static IAuthUser ValidUser() =>
        Duck.Implement<IAuthUser>(new
        {
            User_Id = 238452,
            Country = "GB"
        });

    public static IAuth ValidAuth() =>
        Duck.Implement<IAuth>(new
        {
            Token = "BU8fOjaF4E5POf4WTRm5wA"
        });

    public static IAuthPhone ValidPhone() =>
        Duck.Implement<IAuthPhone>(new
        {
            Number = "+44*******4612",
            Last_4_Digits = "4612",
            Country_Calling_Code = "44",
            Valid = true
        });

    public static IAuthAuthVerification ValidVerification() =>
        Duck.Implement<IAuthAuthVerification>(new
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
            Account = Duck.Implement<IAuthAccount>(new()),
            Auth = Duck.Implement<IAuth>(new()),
            Phone = Duck.Implement<IAuthPhone>(new()),
            Verification = Duck.Implement<IAuthAuthVerification>(new()),
            Force_Password_Reset = false,
            Allow_Pin_Resend_Seconds = 0,
            LoggedInStatus = false,
        });

    public static IGetHomeScreenResponse CreateValidHomeScreenResponse() =>
        Duck.Implement<IGetHomeScreenResponse>(new
        {
            Account = CreateValidHomeScreenAccount(),
            Networks = CreateValidHomeScreenNetworks(),
            Sync_Modules = CreateValidHomeScreenSyncModules(),
            Cameras = CreateValidHomeScreenCameras(),
            Video_Stats = CreateValidHomeScreenVideoStats(),
            Doorbells = CreateValidHomeScreenDoorbells(),
            App_Updates = CreateValidHomeScreenAppUpdates(),
            Device_Limits = CreateValidHomeScreenDeviceLimits(),
            Whats_New = CreateValidHomeScreenWhatsNew(),
            Subscriptions = CreateValidHomeSubscriptions(),
            Entitlements = CreateValidHomeEntitlements(),
            Tiv_Lock_Enable = true,
            Tiv_LockStatus = CreateValidHomeTivLockStatus(),
            Accessories = CreateValidHomeAccessories(),
        });

    public static IHomeScreenAccount CreateValidHomeScreenAccount() =>
        Duck.Implement<IHomeScreenAccount>(new
        {
            Id = 239361,
            Email_Verified = true,
            Email_Verification_Required = true,
            Amazon_Account_Linked = true,
        });

    public static List<IHomeScreenNetwork> CreateValidHomeScreenNetworks() =>
        Duck.Implement<List<IHomeScreenNetwork>>(new[]
        {
            new
            {
                Id = 263178,
                Created_At = "2022-04-25T15:42:46+00:00",
                Updated_At = "2024-02-17T09:02:22+00:00",
                Name = "Home Front",
                Time_Zone = "Europe/London",
                Dst = true,
                Armed = false,
                Lv_Save = true

            }, 
            new
            {
                Id = 263310,
                Created_At = "2022-04-25T16:57:42+00:00",
                Updated_At = "2022-04-25T16:57:42+00:00",
                Name = "Home Back",
                Time_Zone = "Europe/London",
                Dst = true,
                Armed = false,
                Lv_Save = false
            }
        });

    public static List<IHomeScreenSyncModule> CreateValidHomeScreenSyncModules() =>
        Duck.Implement<List<IHomeScreenSyncModule>>(new[]
        {
            new
            {
                Id = 390959,
                Created_At = "2022-04-25T15:43:52+00:00",
                Updated_At = "2024-02-17T01:45:04+00:00",
                Onboarded = true,
                Status = "online",
                Name = "My Blink Sync Module",
                Serial = "G8T1V80120430891",
                Fw_Version = "16.0.8",
                Type = "sm2",
                Subtype = "billy",
                Last_Hb = "2024-02-17T14:14:50+00:00",
                Wifi_Strength = 5,
                Network_Id = 263178,
                Enable_Temp_Alerts = true,
                Local_Storage_Enabled = true,
                Local_Storage_Compatible = true,
                Local_Storage_Status = "active",
                Revision = "01"
            },
            new
            {
                Id = 391167,
                Created_At = "2022-04-25T16:57:46+00:00",
                Updated_At = "2022-04-27T06:48:21+00:00",
                Onboarded = true,
                Status = "offline",
                Name = "My Blink Sync Module",
                Serial = "G8T1LP0003113FGS",
                Fw_Version = "4.4.8",
                Type = "sm2",
                Subtype = "vinnie",
                Last_Hb = "2022-04-27T06:10:07+00:00",
                Wifi_Strength = 5,
                Network_Id = 263310,
                Enable_Temp_Alerts = true,
                Local_Storage_Enabled = false,
                Local_Storage_Compatible = true,
                Local_Storage_Status = "unavailable",
                Revision = "00"
            }
        });

    public static List<IHomeScreenCamera> CreateValidHomeScreenCameras() =>
        Duck.Implement<List<IHomeScreenCamera>>(new[]
        {
            new
            {
                Id = 321343,
                Created_At = "2022-04-25T16:09:04+00:00",
                Updated_At = "2024-02-17T14:01:21+00:00",
                Name = "Pergoda",
                Serial = "G8T1GJ0120430Q9H",
                Fw_Version = "10.64",
                Type = "catalina",
                Enabled = true,
                Thumbnail = "/api/v3/media/accounts/239361/networks/263178/catalina/321343/thumbnail/thumbnail.jpg?ts=1705051738&ext=",
                Status = "done",
                Battery = "ok",
                Usage_Rate = false,
                Network_Id = 263178,
                Signals = new {
                    Lfr = 3,
                    Wifi = 5,
                    Temp = 57,
                    Battery = 3
                },
                Local_Storage_Enabled = false,
                Local_Storage_Compatible = true,
                Snooze = false,
                Revision = "01",
                Color = "black"
            },
            new
            {
                Id = 321380,
                Created_At = "2022-04-25T16:30:43+00:00",
                Updated_At = "2024-02-17T11:46:21+00:00",
                Name = "Garage",
                Serial = "G8T1GJ0120430Q97",
                Fw_Version = "10.64",
                Type = "catalina",
                Enabled = true,
                Thumbnail = "/api/v3/media/accounts/239361/networks/263178/catalina/321380/thumbnail/thumbnail.jpg?ts=1705747766&ext=",
                Status = "done",
                Battery = "ok",
                Usage_Rate = false,
                Network_Id = 263178,
                Signals = new {
                    Lfr = 5,
                    Wifi = 5,
                    Temp = 57,
                    Battery = 3
                },
                Local_Storage_Enabled = false,
                Local_Storage_Compatible = true,
                Snooze = false,
                Revision = "01",
                Color = "black"
            }
        });

    public static IHomeScreenVideoStats CreateValidHomeScreenVideoStats() =>
        Duck.Implement<IHomeScreenVideoStats>(new
        {
            Storage = 0,
            Auto_Delete_Days = 30,
            Auto_Delete_Day_Options = new int[ 3, 7, 14, 30 ],
        });

    public static List<IHomeScreenDoorbells> CreateValidHomeScreenDoorbells() =>
        Duck.Implement<List<IHomeScreenDoorbells>>(new[]
        {
            new
            {
                Id = 5009,
                Created_at = "2022-06-09T14:06:19+00:00",
                Updated_at = "2024-02-17T12:41:27+00:00",
                Name = "Front door bell",
                Type = "lotus",
                Onboarded = true,
                Serial = "G8T1TV0220752BKL",
                Fw_version = "12.66",
                Enabled = true,
                Thumbnail = "/api/v3/media/accounts/239361/networks/263178/lotus/5009/thumbnail/thumbnail.jpg?ts=1705051747&ext=",
                Status = "done",
                Network_id = 263178,
                Battery = "ok",
                Doorbell_mode = "lfr",
                Changing_mode = false,
                Signals = new {
                    Lfr = 5,
                    Wifi = 5,
                    Battery = 3
                },
                Local_storage_enabled = false,
                Local_storage_compatible = false,
                Config_out_of_sync = false,
                Snooze = false,
                Revision = "02",
                Color = "black"
            }
        });

    public static IHomeScreenAppUpdates CreateValidHomeScreenAppUpdates() =>
        Duck.Implement<IHomeScreenAppUpdates>(new
        {
            Message = "An app update is required",
            Code = 105,
            Update_Available = true,
            Update_Required = true
        });

    public static IHomeScreenDeviceLimits CreateValidHomeScreenDeviceLimits() =>
        Duck.Implement<IHomeScreenDeviceLimits>(new
        {
            Camera = 10,
            Chime = 5,
            Doorbell = 10,
            Doorbell_Button = 2,
            Owl = 10,
            Siren = 5,
            Total_Devices = 20
        });

    public static IHomeScreenWhatsNew CreateValidHomeScreenWhatsNew() =>
        Duck.Implement<IHomeScreenWhatsNew>(new
        {
            updated_at = 20210208,
            url = "https://updates.blinkforhome.com/"
        });

    public static IHomeScreenSubscriptions CreateValidHomeSubscriptions() =>
        Duck.Implement<IHomeScreenSubscriptions>(new
        {
            Updated_At = "2023 - 11 - 07T11:10:00 + 00:00"
        });

    public static IHomeScreenEntitlements CreateValidHomeEntitlements() =>
        Duck.Implement<IHomeScreenEntitlements>(new
        {
            Updated_At = "2023 - 11 - 07T11:10:00 + 00:00"
        });

    public static IHomeScreenAccessories CreateValidHomeAccessories() =>
        Duck.Implement<IHomeScreenAccessories>(new
        {
            Storms = Array.Empty<string>(),
            Rosie = Array.Empty<string>(),
        });

    public static IHomeScreenTivLockStatus CreateValidHomeTivLockStatus() =>
        Duck.Implement<IHomeScreenTivLockStatus>(new
        {
            Locked = true
        });
}
