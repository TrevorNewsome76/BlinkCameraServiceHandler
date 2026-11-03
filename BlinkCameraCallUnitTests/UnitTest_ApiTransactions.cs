using BlinkCameraCall;
using FluentAssertions;
using Xunit;

namespace BlinkCameraCallUnitTests;

public class UnitTest_ApiTransactions
{
    [Fact]
    public void Test_BuildLocalStorageClipUrl_BuildsExactTemplate()
    {
        var transactions = new ApiTransactions(MockSettings.CreateSettings());
        var account = MockData.ValidAccount();

        var url = transactions.BuildLocalStorageClipUrl(account, 263178, 390959, 700, 1);

        url.Should().Be("https://rest-007.immedia-semi.com/api/v1/accounts/134166/networks/263178/sync_modules/390959/local_storage/manifest/700/clip/request/1");
    }
}
