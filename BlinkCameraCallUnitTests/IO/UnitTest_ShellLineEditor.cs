using BlinkCameraCall.IO;
using FluentAssertions;

namespace BlinkCameraCallUnitTests.IO;

public class UnitTest_ShellLineEditor
{
    [Fact]
    public void Test_ReadLine_TypingThenEnter_ReturnsTypedString()
    {
        //assign
        var fake = new FakeConsoleIO([Keys.Char('c'), Keys.Char('d'), Keys.Enter]);
        var editor = new ShellLineEditor(fake);

        //act
        var actual = editor.ReadLine("> ");

        //assert
        actual.Should().Be("cd");
    }

    [Fact]
    public void Test_ReadLine_BackspaceAtStart_IsNoOp()
    {
        //assign
        var fake = new FakeConsoleIO([Keys.Backspace, Keys.Backspace, Keys.Backspace, Keys.Char('x'), Keys.Enter]);
        var editor = new ShellLineEditor(fake);

        //act
        var actual = editor.ReadLine("> ");

        //assert
        actual.Should().Be("x");
    }

    [Fact]
    public void Test_ReadLine_Enter_EmptyBuffer_ReturnsEmptyAndDoesNotTouchHistory()
    {
        //assign
        var fake = new FakeConsoleIO([Keys.Enter, Keys.Up, Keys.Enter]);
        var editor = new ShellLineEditor(fake);

        //act
        var firstResult = editor.ReadLine("> ");
        var secondResult = editor.ReadLine("> ");

        //assert
        firstResult.Should().Be("");
        secondResult.Should().Be("");
    }

    [Fact]
    public void Test_ReadLine_LeftArrow_InsertsAtMovedCursor()
    {
        //assign
        var fake = new FakeConsoleIO([Keys.Char('a'), Keys.Char('b'), Keys.Char('c'), Keys.Left, Keys.Left, Keys.Char('X'), Keys.Enter]);
        var editor = new ShellLineEditor(fake);

        //act
        var actual = editor.ReadLine("> ");

        //assert
        actual.Should().Be("aXbc");
    }

    [Fact]
    public void Test_ReadLine_HomeAndEnd_MoveCursorToBufferEnds()
    {
        //assign
        var fake = new FakeConsoleIO([Keys.Char('a'), Keys.Char('b'), Keys.Char('c'), Keys.Home, Keys.Char('X'), Keys.End, Keys.Char('Y'), Keys.Enter]);
        var editor = new ShellLineEditor(fake);

        //act
        var actual = editor.ReadLine("> ");

        //assert
        actual.Should().Be("XabcY");
    }

    [Fact]
    public void Test_ReadLine_Delete_RemovesCharAtCursor()
    {
        //assign
        var fake = new FakeConsoleIO([Keys.Char('a'), Keys.Char('b'), Keys.Char('c'), Keys.Home, Keys.Delete, Keys.Enter]);
        var editor = new ShellLineEditor(fake);

        //act
        var actual = editor.ReadLine("> ");

        //assert
        actual.Should().Be("bc");
    }

    [Fact]
    public void Test_ReadLine_CursorNeverMovesBeforePromptEnd()
    {
        //assign
        var fake = new FakeConsoleIO([
            Keys.Left, Keys.Left, Keys.Backspace, Keys.Left, Keys.Char('a'),
            Keys.Left, Keys.Left, Keys.Left, Keys.Backspace, Keys.Enter
        ]);
        var editor = new ShellLineEditor(fake);

        //act
        editor.ReadLine("> ");

        //assert
        var (promptEndLeft, promptEndTop) = fake.CursorPositionsSet[0];
        fake.CursorPositionsSet
            .Where(p => p.Top == promptEndTop)
            .Should().OnlyContain(p => p.Left >= promptEndLeft);
    }

    [Fact]
    public void Test_ReadLine_UpArrow_RecallsPreviousEntry_DiscardingDraft()
    {
        //assign
        var fake = new FakeConsoleIO([
            Keys.Char('a'), Keys.Char('b'), Keys.Char('c'), Keys.Enter,
            Keys.Char('x'), Keys.Char('y'), Keys.Up, Keys.Enter
        ]);
        var editor = new ShellLineEditor(fake);

        //act
        var firstResult = editor.ReadLine("> ");
        var secondResult = editor.ReadLine("> ");

        //assert
        firstResult.Should().Be("abc");
        secondResult.Should().Be("abc");
    }

    [Fact]
    public void Test_ReadLine_DownArrow_PastNewest_ReturnsToEmpty()
    {
        //assign
        var fake = new FakeConsoleIO([
            Keys.Char('a'), Keys.Char('b'), Keys.Char('c'), Keys.Enter,
            Keys.Up, Keys.Down, Keys.Enter
        ]);
        var editor = new ShellLineEditor(fake);

        //act
        editor.ReadLine("> ");
        var secondResult = editor.ReadLine("> ");

        //assert
        secondResult.Should().Be("");
    }

    [Fact]
    public void Test_ReadLine_DownArrow_PastLongerEntry_ErasesWholeOldLineOnScreen()
    {
        //assign
        var fake = new FakeConsoleIO([
            Keys.Char('a'), Keys.Char('b'), Keys.Char('c'), Keys.Char('d'), Keys.Char('e'), Keys.Char('f'), Keys.Enter,
            Keys.Up, Keys.Down, Keys.Enter
        ]);
        var editor = new ShellLineEditor(fake);

        //act
        editor.ReadLine("> ");
        var writesBeforeSecondCall = fake.WrittenText.Count;
        var secondResult = editor.ReadLine("> ");

        //assert
        secondResult.Should().Be("");
        var writesInSecondCall = fake.WrittenText.Skip(writesBeforeSecondCall).ToList();
        // writesInSecondCall: [0] prompt, [1] Up redraw ("abcdef" + pad), [2] Down redraw (back to empty)
        var downRedraw = writesInSecondCall[2];
        downRedraw.Trim().Should().BeEmpty("the Down redraw must contain nothing but erasing spaces");
        downRedraw.Length.Should().BeGreaterThanOrEqualTo("abcdef".Length,
            "it must blank out every column the 6-char history entry occupied, not just one trailing space");
    }

    [Fact]
    public void Test_ReadLine_ConsecutiveDuplicateEntry_NotAppendedAgain()
    {
        //assign
        var fake = new FakeConsoleIO([
            Keys.Char('a'), Keys.Enter,
            Keys.Char('a'), Keys.Enter,
            Keys.Char('b'), Keys.Enter,
            Keys.Char('b'), Keys.Enter,
            Keys.Up, Keys.Up, Keys.Enter
        ]);
        var editor = new ShellLineEditor(fake);

        //act
        editor.ReadLine("> ");
        editor.ReadLine("> ");
        editor.ReadLine("> ");
        editor.ReadLine("> ");
        var fifthResult = editor.ReadLine("> ");

        //assert
        fifthResult.Should().Be("a");
    }

    [Fact]
    public void Test_NotifyExternal_WhileIdle_JustWritesTheMessage()
    {
        //assign
        var fake = new FakeConsoleIO([]);
        var editor = new ShellLineEditor(fake);

        //act
        editor.NotifyExternal("[ALERT] Front door bell");

        //assert
        fake.WrittenText.Should().ContainSingle().Which.Should().Be("[ALERT] Front door bell" + Environment.NewLine);
    }

    [Fact]
    public void Test_NotifyExternal_DuringInProgressLine_PrintsAlertAndPreservesTypedText()
    {
        //assign
        var fake = new BlockingFakeConsoleIO();
        var editor = new ShellLineEditor(fake);
        var timeout = TimeSpan.FromSeconds(5);

        fake.Enqueue(Keys.Char('c'));
        fake.Enqueue(Keys.Char('d'));

        var readLineTask = Task.Run(() => editor.ReadLine("> "));

        //act
        fake.WaitForReadKeyCalls(3, timeout); // 'c' dispatched, 'd' dispatched, now idle waiting for a 3rd key
        editor.NotifyExternal("[ALERT] Front door bell");
        fake.Enqueue(Keys.Char('x'));
        fake.Enqueue(Keys.Enter);
        var result = readLineTask.Wait(timeout) ? readLineTask.Result : throw new TimeoutException("ReadLine never returned.");

        //assert
        result.Should().Be("cdx");
        var alertIndex = fake.WrittenText.FindIndex(t => t.Contains("[ALERT] Front door bell"));
        alertIndex.Should().BeGreaterThan(-1);
        fake.WrittenText.Skip(alertIndex + 1).Should().ContainInOrder("> ", "cd");
    }
}
