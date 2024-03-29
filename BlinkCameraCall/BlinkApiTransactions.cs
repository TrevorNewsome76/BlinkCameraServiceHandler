﻿using BlinkCommon.Extensions;
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

    private IBlinkSettings BlinkSettings { get; } = settings;

    public bool SetAccessToken(string accessToken) =>
        ApiDriver.SetAccessToken(accessToken);

    public ILoginResponse? AuthLogin()
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("email",
                BlinkSettings.Email),
            new KeyValuePair<string, string>("password",
                BlinkSettings.Password)
        };

        var result =
            ApiDriver
                .Post($"{BlinkSettings?.BaseUrl ?? string.Empty}/api/v5/account/login",
                    parameters);

        return result?.Deserialize<ILoginResponse>() ?? Duck.Implement<ILoginResponse>();
    }

    public ILoginResponse? AuthLogin(string username, string password)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("email",
                username),
            new KeyValuePair<string, string>("password",
                password)
        };

        var result =
            ApiDriver
                .Post($"{BlinkSettings?.BaseUrl ?? string.Empty}/api/v5/account/login",
                    parameters);

        return result?.Deserialize<ILoginResponse>() ?? Duck.Implement<ILoginResponse>();
    }

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
}