using BlinkCameraCall.IO;
using BlinkCameraCallUnitTests.IO;
using FluentAssertions;
using Xunit;

namespace BlinkCameraCallUnitTests.IO;

public class UnitTest_ProgressSpinner
{
    [Fact]
    public void Test_Start_WritesTheMessageOnce()
    {
        var fake = new FakeConsoleIO([]);
        var spinner = new ProgressSpinner(fake);

        spinner.Start("Sending instructions, please wait...");

        fake.WrittenText.Should().Contain(t => t.Contains("Sending instructions, please wait..."));
    }

    [Fact]
    public void Test_Tick_WhileActive_WritesAnotherFrame()
    {
        var fake = new FakeConsoleIO([]);
        var spinner = new ProgressSpinner(fake);
        spinner.Start("Working...");
        var writesAfterStart = fake.WrittenText.Count;

        spinner.Tick();

        fake.WrittenText.Count.Should().BeGreaterThan(writesAfterStart);
    }

    [Fact]
    public void Test_Tick_WhileNotStarted_IsNoOp()
    {
        var fake = new FakeConsoleIO([]);
        var spinner = new ProgressSpinner(fake);

        spinner.Tick();

        fake.WrittenText.Should().BeEmpty();
    }

    [Fact]
    public void Test_WriteLine_WhileActive_PrintsLineAndKeepsAnimating()
    {
        var fake = new FakeConsoleIO([]);
        var spinner = new ProgressSpinner(fake);
        spinner.Start("Working...");

        spinner.WriteLine("Camera 'Pergoda': armed.");

        fake.WrittenText.Should().Contain(t => t.Contains("Camera 'Pergoda': armed."));
        // Spinner should still be active afterward — proven by Tick() producing more output.
        var writesBeforeTick = fake.WrittenText.Count;
        spinner.Tick();
        fake.WrittenText.Count.Should().BeGreaterThan(writesBeforeTick);
    }

    [Fact]
    public void Test_WriteLine_AfterStop_DoesNotResurrectAnimation()
    {
        var fake = new FakeConsoleIO([]);
        var spinner = new ProgressSpinner(fake);
        spinner.Start("Working...");
        spinner.Stop();

        spinner.WriteLine("Done.");
        var writesAfterWriteLine = fake.WrittenText.Count;
        spinner.Tick();

        fake.WrittenText.Count.Should().Be(writesAfterWriteLine, "Tick() after Stop() must still be a no-op even if WriteLine was called in between");
    }

    [Fact]
    public void Test_Stop_ThenTick_IsNoOp()
    {
        var fake = new FakeConsoleIO([]);
        var spinner = new ProgressSpinner(fake);
        spinner.Start("Working...");
        spinner.Stop();
        var writesAfterStop = fake.WrittenText.Count;

        spinner.Tick();

        fake.WrittenText.Count.Should().Be(writesAfterStop);
    }
}
