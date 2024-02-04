using BlinkCommon.Interfaces;
using Dependency;
using FluentAssertions;
using Shadow.Quack;
using System.Runtime;

using Xunit.Abstractions;

namespace BlinkCameraCallUnitTests.Auth
{
    [Collection("Sequential")]
    public class UnitTest_Login(ITestOutputHelper output)
    {
        private static IApiTransactions? BlinkAdapter => Shelf.RetrieveInstance<IApiTransactions>();

        [Fact]
        public void Test_SuccessfulLogin()
        {
            //assign
            var mockAdapter = new MockAdapter(MockSettings.CreateSettings());

            var expected = MockData.AuthLoginCorrectResponse();

            //act
           var actualResult = BlinkAdapter?.AuthLogin(
               MockSettings.CreateSettings().Email, MockSettings.CreateSettings().Password) 
                              ?? Duck.Implement<ILoginResponse>(new());

            //assert
            actualResult.Should().BeEquivalentTo(expected);

            //Results
            output.WriteLine("Message: {0}", actualResult.Message);
        }

        [Fact]
        public void Test_FailedLogin()
        {
            //assign
            var expected = MockData.AuthLoginFailedResponse();

            //act
            var actualResult = BlinkAdapter?.AuthLogin("blah", "blah") 
                               ?? Duck.Implement<ILoginResponse>(new());

            //assert
            actualResult.Should().BeEquivalentTo(expected);

            //Results
            output.WriteLine("Message: {0}", actualResult.Message);
        }

        [Fact]
        public void Test_SetAccessTokenToNulAndFail()
        {
            // assign

            // act
            var actualResult = BlinkAdapter?.SetAccessToken(null!);

            // assert
            actualResult.Should().BeFalse();
        }

        [Fact]
        public void Test_SetAccessTokenToValueAndPass()
        {
            // assign

            // act
            var actualResult = BlinkAdapter?.SetAccessToken("BU8fOjaF4E5POf4WTRm5wA");

            // assert
            actualResult.Should().BeTrue();
        }
    }
}