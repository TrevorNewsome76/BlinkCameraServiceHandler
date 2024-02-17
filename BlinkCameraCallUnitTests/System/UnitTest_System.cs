using BlinkCommon.Interfaces;
using BlinkCommon.Interfaces.Auth;
using BlinkCommon.Interfaces.System;
using Dependency;
using FluentAssertions;
using Shadow.Quack;
using Xunit.Abstractions;

namespace BlinkCameraCallUnitTests.System
{
    [Collection("Sequential")]
    public class UnitTest_System(ITestOutputHelper output)
    {
        private static IApiTransactions? BlinkAdapter => Shelf.RetrieveInstance<IApiTransactions>();

        [Fact]
        public void Test_SuccessfulHomeScreenCommand()
        {
            //assign
            var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
            ISessionDetails sessionDetails = MockData.CreateValidLoggedInSessionDetails();

            var expected = MockData.CreateValidHomeScreenResponse();

            //act
            var actualResult = BlinkAdapter?.SystemGetHomeScreen(sessionDetails.Account)
                               ?? Duck.Implement<IGetHomeScreenResponse>(new());

            //assert
            actualResult.Should().BeEquivalentTo(expected);

            //Results
            //output.WriteLine("Message: {0}", actualResult.Message);
        }
    }
}