using BlinkCameraCall.Driver;
using BlinkCommon.Extensions;
using BlinkCommon.Interfaces;
using BlinkCommon.Interfaces.Auth;
using BlinkCommon.Interfaces.System;
using Shadow.Quack;
using Shadow.Quack.Json;

using Dependency;
using FluentAssertions.Equivalency;

using Shadow.Quack;

using System.Security.Principal;

namespace BlinkCameraCall;

public class ApiTransactions(IBlinkSettings settings) : IApiTransactions
{
    private static IApiMethods ApiDriver => Shelf.RetrieveInstance<IApiMethods>();

    private static IOAuthLogin OAuthLogin => Shelf.RetrieveInstance<IOAuthLogin>();

    private IBlinkSettings BlinkSettings { get; } = settings;

    private string _pendingUsername = settings.Email;
    private string _pendingPassword = settings.Password;

    public bool SetAccessToken(string accessToken) =>
        ApiDriver.SetAccessToken(accessToken);

    public ILoginResponse? AuthLogin() =>
        AuthLogin(BlinkSettings.Email, BlinkSettings.Password);

    public ILoginResponse? AuthLogin(string username, string password)
    {
        _pendingUsername = username;
        _pendingPassword = password;
        return MapLoginResult(OAuthLogin.Login(username, password, BlinkSettings.HardwareId), username, password);
    }

    public ILoginResponse? AuthCompleteTwoFactorLogin(string code) =>
        MapLoginResult(OAuthLogin.CompleteTwoFactor(code), _pendingUsername, _pendingPassword);

    private static ILoginResponse MapLoginResult(OAuthLoginResult result, string username, string password) =>
        Duck.Implement<ILoginResponse>(new
        {
            Account = Duck.Implement<IAuthAccount>(new
            {
                Account_Id = result.AccountId,
                Client_Id = result.ClientId,
                Tier = result.Tier,
            }),
            Auth = Duck.Implement<IAuth>(new
            {
                Token = result.AccessToken,
                UserName = username,
                Password = password,
            }),
            Phone = Duck.Implement<IAuthPhone>(new()),
            Verification = Duck.Implement<IAuthAuthVerification>(new
            {
                Phone = Duck.Implement<IVerifyPhone>(new { Required = result.TwoFactorRequired }),
                Email = Duck.Implement<IVerifyEmail>(new()),
            }),
            Force_Password_Reset = false,
            Allow_Pin_Resend_Seconds = 0,
            Message = result.Message,
            Code = result.Success ? 200 : 400,
        });

    public ILogoutResponse? AuthLogout(IAuthAccount account)
    {
        var result =
            ApiDriver?
                .Post($"{BlinkSettings?.BaseUrl ?? string.Empty}/api/v4/account/{account.Account_Id}/client/{account.Client_Id}/logout") ?? string.Empty;

        return result.Deserialize<ILogoutResponse>();
    }

    public IVerifyPinResponse? AuthVerifyPin(IAuthAccount account, string pinCode)
    {
        var baseString = $"https://rest-{account.Tier}.immedia-semi.com/api/v4/account/{account.Account_Id}/client/{account.Client_Id}/pin/verify";
        var serializedPin = "{\"pin\":\"" +pinCode + "\"}";
        var result = ApiDriver?.Post(baseString, serializedPin) ?? string.Empty;
        return result.Deserialize<IVerifyPinResponse>();
    }

    public IGetHomeScreenResponse? SystemGetHomeScreen(IAuthAccount account)
    {
        var baseString = $"https://rest-{account.Tier}.immedia-semi.com/api/v3/accounts/{account.Account_Id}/homescreen";
        var result = ApiDriver?.Get(baseString) ?? string.Empty;
        return Duck.Implement<IGetHomeScreenResponse>((JsonProxy)result);
    }

    public byte[]? DownloadBytes(string url) =>
        ApiDriver?.GetBytes(url);

    public ICommandStatusResponse? SystemTriggerCameraSnapshot(IAuthAccount account, long networkId, long cameraId, TimeSpan? pollInterval = null, int maxRetries = 120) =>
        TriggerSnapshot($"https://rest-{account.Tier}.immedia-semi.com/network/{networkId}/camera/{cameraId}/thumbnail", networkId, account.Tier, pollInterval, maxRetries);

    public ICommandStatusResponse? SystemTriggerDoorbellSnapshot(IAuthAccount account, long networkId, long doorbellId, TimeSpan? pollInterval = null, int maxRetries = 120) =>
        TriggerSnapshot($"https://rest-{account.Tier}.immedia-semi.com/api/v1/accounts/{account.Account_Id}/networks/{networkId}/doorbells/{doorbellId}/thumbnail", networkId, account.Tier, pollInterval, maxRetries);

    public IMediaChangedResponse? SystemGetRecentMedia(IAuthAccount account, DateTime sinceUtc)
    {
        var since = sinceUtc.ToString("yyyy-MM-ddTHH:mm:ss+00:00");
        var url = $"https://rest-{account.Tier}.immedia-semi.com/api/v1/accounts/{account.Account_Id}/media/changed?since={since}&page=1";
        var result = ApiDriver?.Get(url) ?? string.Empty;
        return result.Deserialize<IMediaChangedResponse>();
    }

    private static ICommandStatusResponse? TriggerSnapshot(string triggerUrl, long networkId, string tier, TimeSpan? pollInterval, int maxRetries)
    {
        if (ApiDriver is null)
            return null;

        var triggerResult = (ApiDriver.Post(triggerUrl) ?? string.Empty).Deserialize<ICommandStatusResponse>();
        var commandUrl = $"https://rest-{tier}.immedia-semi.com/network/{networkId}/command/{triggerResult.Id}";

        return CommandPolling.WaitForCommandComplete(ApiDriver, commandUrl, pollInterval, maxRetries);
    }
}