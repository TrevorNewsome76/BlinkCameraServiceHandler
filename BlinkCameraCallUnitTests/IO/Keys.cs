namespace BlinkCameraCallUnitTests.IO;

/// <summary>
/// Builds <see cref="ConsoleKeyInfo"/> values for scripting <see cref="FakeConsoleIO"/>.
/// </summary>
public static class Keys
{
    public static ConsoleKeyInfo Char(char c) => new(c, ConsoleKey.NoName, false, false, false);

    public static ConsoleKeyInfo Backspace => new('\0', ConsoleKey.Backspace, false, false, false);

    public static ConsoleKeyInfo Delete => new('\0', ConsoleKey.Delete, false, false, false);

    public static ConsoleKeyInfo Left => new('\0', ConsoleKey.LeftArrow, false, false, false);

    public static ConsoleKeyInfo Right => new('\0', ConsoleKey.RightArrow, false, false, false);

    public static ConsoleKeyInfo Home => new('\0', ConsoleKey.Home, false, false, false);

    public static ConsoleKeyInfo End => new('\0', ConsoleKey.End, false, false, false);

    public static ConsoleKeyInfo Up => new('\0', ConsoleKey.UpArrow, false, false, false);

    public static ConsoleKeyInfo Down => new('\0', ConsoleKey.DownArrow, false, false, false);

    public static ConsoleKeyInfo Enter => new('\0', ConsoleKey.Enter, false, false, false);
}
