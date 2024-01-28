using BlinkCommon.Interfaces;
using Dependency;

using FluentAssertions;
using Xunit.Abstractions;

namespace BlinkCameraCallUnitTests.Auth;

public class UnitTestPin
{
    [Collection("Sequential")]
    public class UnitTest_Pin
    {
        private readonly ITestOutputHelper output;
        private static IBlinkApiTransactions? BlinkAdapter
        {
            get { return Shelf.RetrieveInstance<IBlinkApiTransactions>(); }
        }

        public UnitTest_Pin(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void Test_SuccessfulPin()
        {
            // assign
            MockAdapter.Initialize(MockSettings.CreateSettings());
            var expected = MockData.AuthCorrectPinResponse();

            // act
            var actualResult = BlinkAdapter?
                .AuthVerifyPin("987654", "007", 123456, 7890, null!);
                               

            // assert
            actualResult.Should().BeEquivalentTo(expected);

            // Results
            output.WriteLine("ResponseMessage: {0}", actualResult?.Message ?? string.Empty);
        }

        [Fact]
        public void Test_FailedPin()
        {
            // assign
            MockAdapter.Initialize(MockSettings.CreateSettings());
            var expected = MockData.AuthIncorrectPinResponse();

            // act
            var actualResult = BlinkAdapter?
                .AuthVerifyPin("111111", "007", 123456, 7890, null!);


            // assert
            actualResult.Should().BeEquivalentTo(expected);

            // Results
            output.WriteLine("ResponseMessage: {0}", actualResult?.Message ?? string.Empty);
        }
    }
}