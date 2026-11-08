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
}
