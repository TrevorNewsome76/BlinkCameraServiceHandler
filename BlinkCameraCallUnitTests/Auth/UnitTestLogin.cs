using BlinkCameraCall;
using BlinkCommon.Interfaces;
using FluentAssertions;
using Shadow.Quack;
using Xunit.Abstractions;

namespace BlinkCameraCallUnitTests.Auth
{

    [Collection("Sequential")]
    public class UnitTest_Login
    {
        private readonly ITestOutputHelper output;

        public UnitTest_Login(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void Test_SuccessfulLogin()
        {
            // assign
            MockAdapter.Initialize(MockSettings.CreateSettings());
            var expected = MockData.AuthLoginResponse();
            
            // act
            var actualResult = new BlinkApiTransactions(MockSettings.CreateSettings()).AuthLogin() ?? Duck.Implement<ILoginResponse>(new());

            // assert
            actualResult.Should().BeEquivalentTo(expected);

            // Results
            output.WriteLine("Token: {0}", actualResult.Auth.Token);
        }
    }
}