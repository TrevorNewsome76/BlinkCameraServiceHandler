using BlinkCameraCall;
using BlinkCameraCall.Driver;

using BlinkCommon.Interfaces;
using Dependency;
using FluentAssertions;
using Shadow.Quack;

using Xunit.Abstractions;

namespace BlinkCameraCallUnitTests.Extensions;

[Collection("Sequential")]
public class UnitTest_AdapterExtension(ITestOutputHelper output)
{
    [Fact]
    public void Test_BlinkAdapterContructor()
    {
        //assign
        Shelf.Clear();

        //act
        var blinkAdapter = new BlinkAdapter(MockSettings.CreateSettings("blah", "blah"));

        //assert
        var transactions = Shelf.RetrieveInstance<IApiTransactions>();
        var apiDriver = Shelf.RetrieveInstance<IApiMethods>();
        var apiInterface = Shelf.RetrieveInstance<IApiTransactions>();

        //Results
        //output.WriteLine("Message: {0}", actualResult.Message);
        Assert.NotNull(apiDriver);
        Assert.NotNull(apiInterface);
        Assert.NotNull(transactions);
    }
}