namespace BlinkCameraCall.IO;

public class SystemConsoleIO : IConsoleIO
{
    public int CursorLeft
    {
        get => Console.CursorLeft;
        set => Console.CursorLeft = value;
    }

    public int CursorTop
    {
        get => Console.CursorTop;
        set => Console.CursorTop = value;
    }

    public int WindowWidth => Console.WindowWidth;

    public ConsoleKeyInfo ReadKey() => Console.ReadKey(intercept: true);

    public bool KeyAvailable => Console.KeyAvailable;

    public void Write(string text) => Console.Write(text);
}
