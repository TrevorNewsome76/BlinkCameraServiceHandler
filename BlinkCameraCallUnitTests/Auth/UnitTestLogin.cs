using BlinkCommon.Interfaces;
using Dependency;
using FluentAssertions;
using Shadow.Quack;
using Xunit.Abstractions;

namespace BlinkCameraCallUnitTests.Auth
{
    [Collection("Sequential")]
    public class UnitTestLogin
    {
        private readonly ITestOutputHelper output;
        private static IBlinkApiTransactions? BlinkAdapter => Shelf.RetrieveInstance<IBlinkApiTransactions>();

        public UnitTestLogin(ITestOutputHelper output)
        {

            this.output = output;
        }

        [Fact]
        public void Test_SuccessfulLogin()
        {
            // assign
            MockAdapter.Initialize(MockSettings.CreateSettings());

            var expected = MockData.AuthLoginCorrectResponse();
            
            // act
            var actualResult = BlinkAdapter?.AuthLogin() ?? Duck.Implement<ILoginResponse>(new());

            // assert
            actualResult.Should().BeEquivalentTo(expected);

            // Results
            output.WriteLine("Message: {0}", actualResult.Message);
        }

        [Fact]
        public void Test_FailedLogin()
        {
            // assign
            MockAdapter.Initialize(MockSettings.CreateSettings("blah", "blah"));
            var expected = MockData.AuthLoginFailedResponse();

            // act
            var actualResult = BlinkAdapter?.AuthLogin() ?? Duck.Implement<ILoginResponse>(new());

            // assert
            actualResult.Should().BeEquivalentTo(expected);

            // Results
            output.WriteLine("Message: {0}", actualResult.Message);
        }
    }
}