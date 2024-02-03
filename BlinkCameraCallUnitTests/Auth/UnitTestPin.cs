using BlinkCommon.Interfaces;
using Dependency;

using FluentAssertions;
using Xunit.Abstractions;

namespace BlinkCameraCallUnitTests.Auth;

public class UnitTestPin
{
    [Collection("Sequential")]
    public class UnitTest_Pin(ITestOutputHelper output)
    {
        private static IApiTransactions? MockAdapter => 
            Shelf.RetrieveInstance<IApiTransactions>();

        [Fact]
        public void Test_SuccessfulPin()
        {
            // assign
            var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
            ISessionDetails sessionDetails = MockData.CreateValidLoggedInSessionDetails();

            var expected = MockData.AuthCorrectPinResponse();

            // act
            var actualResult = MockAdapter?
                .AuthVerifyPin(sessionDetails.Account!, "987654");
                               

            // assert
            actualResult.Should().BeEquivalentTo(expected);

            // Results
            output.WriteLine("ResponseMessage: {0}", actualResult?.Message ?? string.Empty);
        }

        [Fact]
        public void Test_FailedPin()
        {
            // assign
            var mockAdapter =  new MockAdapter(MockSettings.CreateSettings());
            ISessionDetails sessionDetails = MockData.CreateValidLoggedInSessionDetails();

            var expected = MockData.AuthIncorrectPinResponse();

            // act
            var actualResult = MockAdapter?
                .AuthVerifyPin(sessionDetails.Account!, "11111");


            // assert
            actualResult.Should().BeEquivalentTo(expected);

            // Results
            output.WriteLine("ResponseMessage: {0}", actualResult?.Message ?? string.Empty);
        }
    }
}